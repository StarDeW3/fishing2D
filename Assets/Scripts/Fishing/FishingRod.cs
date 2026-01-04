using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(LineRenderer))]
public class FishingRod : MonoBehaviour
{
    private static Material sharedLineMaterial;

    private const int LINE_POINT_COUNT = 20;
    private bool lastHadHook = false;
    private bool lastCanMove = true;
    private Vector3[] linePoints;
    [Header("Ayarlar")]
    public Transform rodTip; // Oltanın ucu
    public GameObject hookPrefab; // Kanca prefabı
    public float castForce = 5f; // Fırlatma gücü
    [Tooltip("Fırlatma yönü (X: İleri, Y: Yukarı/Aşağı). Suya atmak için Y negatif olmalı.")]
    public Vector2 throwDirection = new Vector2(0.2f, -1f); // Varsayılan: Hafif ileri ve aşağı

    [Header("Kanca Limit")]
    public bool limitHookHorizontal = true;
    [Tooltip("Kancanın, olta ucuna göre sağ/sol maksimum gidebileceği mesafe (world units).")]
    public float hookMaxHorizontalOffset = 6f;

    [Tooltip("Kanca, atıldığı noktadan belirli bir yarıçapın dışına çıkamasın.")]
    public bool limitHookRadiusFromCastPoint = true;

    [Tooltip("Kancanın atıldığı noktadan maksimum uzaklığı (world units).")]
    public float hookMaxDistanceFromCastPoint = 8f;

    [Header("Limitler (Geliştirmeler)")]
    [Tooltip("Açıksa CastDistance geliştirmesi misina limitlerini büyütür.")]
    public bool scaleLineLimitsWithUpgrades = true;

    [Tooltip("Geliştirmeler aktifken, misina base mesafesi (metre).")]
    public float baseLineDistanceMeters = 8f;

    [Tooltip("CastDistance max level'de, misina max mesafesi (metre).")]
    public float maxLineDistanceMetersAtMaxCastLevel = 50f;

    [Tooltip("CastDistance (UpgradeType.CastDistance) değerinin, yatay ofsete world-unit karşılığı.")]
    public float castDistanceToHorizontalOffset = 1.5f;

    [Header("Misina Esneme (Smooth Limit)")]
    [Tooltip("Limitte kanca bir anda durmasın; bu kadar daha esneyebilsin (world units).")]
    public float lineElasticity = 2f;

    [Tooltip("Esneme bölgesinde içeri çeken yay kuvveti. Daha yüksek = daha sert.")]
    public float lineSpringStrength = 80f;

    [Tooltip("Esneme bölgesinde dışa doğru hız varsa onu sönümleme.")]
    public float lineSpringDamping = 12f;

    [Header("Misina Kopma")]
    public bool enableLineBreak = true;

    [Tooltip("Atıştan hemen sonra kopmayı engellemek için kısa süre tolerans (sn).")]
    public float lineBreakGraceTime = 0.6f;

    [Tooltip("Kopma eşiği (Hook içindeki tension 0..threshold). 1 = ~1sn tam zorlamada kopar.")]
    public float lineBreakThreshold = 1f;

    [Tooltip("Limitte dışarı itmeye çalışırken tension artış hızı (sn başına).")]
    public float lineBreakBuildRate = 1.25f;

    [Tooltip("Zorlamayı bırakınca tension azalış hızı (sn başına).")]
    public float lineBreakRecoverRate = 0.75f;

    [Tooltip("Açıksa LineStrength geliştirmesi kopmayı zorlaştırır.")]
    public bool scaleLineBreakWithUpgrades = true;

    [Header("Güçlü Atış (Space basılı tut)")]
    public bool enableChargedCast = true;

    [Tooltip("Space basılı tutunca dolan charge süresi (sn).")]
    public float chargedCastMaxHoldTime = 1.0f;

    [Tooltip("Charge doldukça atış kuvvet çarpanı 1..max.")]
    public float chargedCastMaxMultiplier = 1.75f;

    private GameObject currentHook;
    private LineRenderer lineRenderer;
    private bool isCasting = false;
    public bool isMiniGameActive = false; // Dışarıdan erişim için
    private CameraFollow cameraFollow;
    private WaveManager waveManager; // Cache
    private float nextCameraFollowLookupTime = 0f;

    private bool isChargingCast = false;
    private float chargedHoldTime = 0f;
    private float lastCastCharge01 = 0f;

    private float GetUpgradeValue(UpgradeType type)
    {
        if (UpgradeManager.instance == null) return 0f;
        return UpgradeManager.instance.GetValue(type);
    }

    private bool TryGetUpgradeDef(UpgradeType type, out UpgradeDef def)
    {
        def = null;
        if (UpgradeManager.instance == null) return false;
        if (UpgradeManager.instance.upgrades == null) return false;

        def = UpgradeManager.instance.upgrades.Find(u => u != null && u.type == type);
        return def != null;
    }

    public float GetMaxDistanceFromCastPoint()
    {
        // Important: Unity serializes fields; old multiplier values in scenes/prefabs can stick around.
        // So we compute distance deterministically from (base -> max at max level).

        float baseValue = Mathf.Max(0f, baseLineDistanceMeters);

        if (!scaleLineLimitsWithUpgrades)
            return Mathf.Max(0f, hookMaxDistanceFromCastPoint);

        float bonus = Mathf.Max(0f, GetUpgradeValue(UpgradeType.CastDistance));

        float maxAtMaxLevel = Mathf.Max(baseValue, maxLineDistanceMetersAtMaxCastLevel);
        float delta = maxAtMaxLevel - baseValue;

        float maxUpgradeValue = 0f;
        if (TryGetUpgradeDef(UpgradeType.CastDistance, out UpgradeDef def))
            maxUpgradeValue = Mathf.Max(0f, def.baseValue + (def.maxLevel * def.valuePerLevel));

        if (maxUpgradeValue <= 0.0001f)
            return baseValue;

        float scale = delta / maxUpgradeValue;
        return Mathf.Clamp(baseValue + (bonus * scale), baseValue, maxAtMaxLevel);
    }

    public float GetMaxHorizontalOffset()
    {
        float baseValue = Mathf.Max(0f, hookMaxHorizontalOffset);
        if (!scaleLineLimitsWithUpgrades) return baseValue;

        float bonus = Mathf.Max(0f, GetUpgradeValue(UpgradeType.CastDistance));
        return baseValue + (bonus * Mathf.Max(0f, castDistanceToHorizontalOffset));
    }

    public float GetLineBreakGraceTime()
    {
        return Mathf.Max(0f, lineBreakGraceTime);
    }

    public float GetLineBreakThreshold()
    {
        float baseValue = Mathf.Max(0.0001f, lineBreakThreshold);
        if (!scaleLineBreakWithUpgrades) return baseValue;

        // Upgrade value is % reduction (10, 20, ...). We translate that to a higher threshold.
        float percent = Mathf.Max(0f, GetUpgradeValue(UpgradeType.LineStrength));
        return baseValue * (1f + (percent / 100f));
    }

    public float GetLineBreakBuildRate()
    {
        float baseValue = Mathf.Max(0f, lineBreakBuildRate);
        if (!scaleLineBreakWithUpgrades) return baseValue;

        // Also reduce how fast tension builds.
        float percent = Mathf.Max(0f, GetUpgradeValue(UpgradeType.LineStrength));
        float multiplier = Mathf.Clamp01(1f - (percent / 100f));
        return baseValue * multiplier;
    }

    public float GetLineBreakRecoverRate()
    {
        return Mathf.Max(0f, lineBreakRecoverRate);
    }

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.enabled = false;

        // Line Renderer ayarları (ince bir ip gibi görünmesi için)
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        if (sharedLineMaterial == null)
            sharedLineMaterial = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.sharedMaterial = sharedLineMaterial;
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;

        if (Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow>();

        waveManager = WaveManager.instance;
    }

    void Update()
    {
        // Mini oyun durumunu kontrol et
        // Optimize: Cache the result to avoid multiple checks
        bool miniGameRunning = FishingMiniGame.instance != null && FishingMiniGame.instance.IsPlaying;
        isMiniGameActive = miniGameRunning;

        // Input alma (Sadece mini oyun yoksa)
        if (!isMiniGameActive)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                // Reel-in: kanca varken Space'e bas
                if (isCasting)
                {
                    if (kb.spaceKey.wasPressedThisFrame)
                        ReelIn();
                }
                else
                {
                    // Cast: Space basılı tut -> bırakınca atış (bonus charge)
                    if (kb.spaceKey.wasPressedThisFrame)
                    {
                        isChargingCast = true;
                        chargedHoldTime = 0f;
                        lastCastCharge01 = 0f;
                    }

                    if (isChargingCast)
                    {
                        if (enableChargedCast)
                            chargedHoldTime += Time.deltaTime;

                        float denom = Mathf.Max(0.01f, chargedCastMaxHoldTime);
                        lastCastCharge01 = enableChargedCast ? Mathf.Clamp01(chargedHoldTime / denom) : 0f;

                        if (kb.spaceKey.wasReleasedThisFrame)
                        {
                            CastLine(lastCastCharge01);
                            isChargingCast = false;
                            chargedHoldTime = 0f;
                        }
                    }
                }
            }
        }

        bool hasHook = currentHook != null;

        // Misina çizimi
        if (hasHook)
        {
            if (!lastHadHook)
            {
                lineRenderer.enabled = true;
                lineRenderer.positionCount = LINE_POINT_COUNT;
            }

            UpdateFishingLine();

            // Tekne hareketini durdur
            BoatController boat = BoatController.instance;
            if (boat != null && lastCanMove)
            {
                boat.canMove = false;
                lastCanMove = false;
            }
        }
        else
        {
            if (lastHadHook)
                lineRenderer.enabled = false;

            // Tekne hareketini serbest bırak (Mini oyun yoksa)
            BoatController boat = BoatController.instance;
            bool desiredCanMove = !isMiniGameActive;
            if (boat != null && lastCanMove != desiredCanMove)
            {
                boat.canMove = desiredCanMove;
                lastCanMove = desiredCanMove;
            }
        }

        lastHadHook = hasHook;
    }

    void UpdateFishingLine()
    {
        // Bezier Eğrisi ile ipin sarkmasını simüle et
        if (linePoints == null || linePoints.Length != LINE_POINT_COUNT)
            linePoints = new Vector3[LINE_POINT_COUNT];

        Vector3 p0 = rodTip.position;
        Vector3 p2 = currentHook.transform.position;

        // Orta kontrol noktası (Sarkma miktarı)
        float distance = Vector3.Distance(p0, p2);
        float sagAmount = distance * 0.3f;

        // Kanca suyun altındaysa sarkma azalır
        if (waveManager == null) waveManager = WaveManager.instance; // Lazy load if needed

        if (waveManager != null && p2.y < waveManager.GetWaveHeight(p2.x))
        {
            sagAmount *= 0.2f;
        }

        // Mini oyun sırasında ip gergin olsun (Balık çekiliyor)
        if (isMiniGameActive)
        {
            sagAmount *= 0.1f; // Çok az sarkma (Gergin ip)
        }

        Vector3 midPoint = (p0 + p2) / 2f;
        Vector3 p1 = midPoint + Vector3.down * sagAmount;

        for (int i = 0; i < LINE_POINT_COUNT; i++)
        {
            float t = i / (float)(LINE_POINT_COUNT - 1);
            // Optimize: Bezier calculation
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;

            Vector3 pos = uu * p0 + 2 * u * t * p1 + tt * p2;

            linePoints[i] = pos;
        }

        lineRenderer.SetPositions(linePoints);
    }

    void CastLine(float charge01 = 0f)
    {
        isCasting = true;
        // In case a new cast is triggered quickly, reset charging state.
        isChargingCast = false;
        chargedHoldTime = 0f;

        if (SoundManager.instance != null)
            SoundManager.instance.PlaySFX(SoundManager.instance.castSound, 1f, 0.1f); // Hafif pitch değişimi

        currentHook = Instantiate(hookPrefab, rodTip.position, Quaternion.identity);

        // Hook scriptine referans ata (Optimizasyon)
        Hook hookScript = currentHook.GetComponent<Hook>();
        if (hookScript != null)
        {
            hookScript.fishingRod = this;
            hookScript.SetCastOrigin(rodTip != null ? (Vector2)rodTip.position : (Vector2)currentHook.transform.position);
        }

        Rigidbody2D hookRb = currentHook.GetComponent<Rigidbody2D>();

        // Fırlatma yönü: Ayarlanan vektörü kullan
        Vector2 dir = throwDirection.normalized;

        // Eğer tekne sola dönükse (basit kontrol) yönü çevir
        if (transform.right.x < 0)
            dir.x *= -1;

        // Upgrade sisteminden mesafe/force bonusu al
        float finalForce = castForce;
        if (UpgradeManager.instance != null)
        {
            float bonus = Mathf.Max(0f, UpgradeManager.instance.GetValue(UpgradeType.CastDistance));
            finalForce = castForce + bonus;
        }

        // Başlangıçta çok uzağa gitmesin; üst sınır da olsun.
        float multiplier = 1f;
        if (enableChargedCast)
            multiplier = Mathf.Lerp(1f, Mathf.Max(1f, chargedCastMaxMultiplier), Mathf.Clamp01(charge01));

        finalForce *= multiplier;
        finalForce = Mathf.Clamp(finalForce, 2.5f, 16f);

        hookRb.AddForce(dir * finalForce, ForceMode2D.Impulse);

        // Kameraya kancayı bildir
        if (cameraFollow == null && Time.unscaledTime >= nextCameraFollowLookupTime)
        {
            nextCameraFollowLookupTime = Time.unscaledTime + 1f;
            Camera cam = Camera.main;
            if (cam != null)
                cameraFollow = cam.GetComponent<CameraFollow>();
        }

        if (cameraFollow != null) cameraFollow.secondaryTarget = currentHook.transform;
    }

    public void BreakLine()
    {
        // Called by Hook when tension exceeds threshold.
        if (!isCasting) return;

        if (GameManager.instance != null)
            GameManager.instance.ShowFeedback(LocalizationManager.T("feedback.lineBroke", "Misina koptu!"), Color.red);

        if (SoundManager.instance != null)
            SoundManager.instance.PlaySFX(SoundManager.instance.escapeSound, 1f, 0.1f);

        ResetFishingState();

        if (cameraFollow != null)
            cameraFollow.TriggerShake(0.5f, 0.4f);
    }

    public void ReelInSuccess(Fish fish)
    {
        if (fish == null)
        {
            ResetFishingState();
            return;
        }

        int payout = Mathf.RoundToInt(fish.scoreValue * UpgradeManager.BASE_FISH_SELL_MULTIPLIER);
        if (UpgradeManager.instance != null)
        {
            float bonusPercent = Mathf.Max(0f, UpgradeManager.instance.GetValue(UpgradeType.StorageCapacity));
            payout = Mathf.RoundToInt(payout * (1f + (bonusPercent / 100f)));
        }

        // Lifetime istatistiklerini güncelle (yakalanan balık sayısı ve toplam kazanç)
        if (UIManager.instance != null)
        {
            UIManager.instance.OnFishCaught(payout);
        }

        if (SoundManager.instance != null)
            SoundManager.instance.PlaySFX(SoundManager.instance.catchSound, 1f, 0.2f);

        if (GameManager.instance != null)
        {
            GameManager.instance.AddMoney(payout); // Score -> Money (+ storage bonus)

            if (QuestManager.instance != null)
                QuestManager.instance.ReportFishCaught(fish, payout);

            string fishName = !string.IsNullOrEmpty(fish.fishName) ? fish.fishName : LocalizationManager.T("fish.defaultName", "Balık");
            bool showRarity = SettingsManager.instance == null || SettingsManager.instance.ShowRarityOnCatch;
            string rarity = (showRarity && !string.IsNullOrEmpty(fish.rarityLabel)) ? $" ({fish.rarityLabel})" : "";

            string msg = LocalizationManager.Format(
                "feedback.caughtFmt",
                "{0}{1} yakaladın!\n+${2}",
                fishName,
                rarity,
                payout
            );
            GameManager.instance.ShowFeedback(msg);
        }

        if (fish != null) fish.Despawn();

        ResetFishingState();

        // Success specific shake
        if (cameraFollow != null)
            cameraFollow.TriggerShake(0.5f, 0.3f);
    }

    public void ReelInFail()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.ShowFeedback(LocalizationManager.T("feedback.escaped", "Balık kaçtı!"), Color.red);
        }

        if (SoundManager.instance != null)
            SoundManager.instance.PlaySFX(SoundManager.instance.escapeSound, 1f, 0.1f);

        ResetFishingState();

        // Fail specific shake
        if (cameraFollow != null)
            cameraFollow.TriggerShake(0.5f, 0.5f);
    }

    void ReelIn()
    {
        if (currentHook != null)
        {
            Hook hookScript = currentHook.GetComponent<Hook>();
            if (hookScript != null && hookScript.caughtFish != null)
            {
                ReelInSuccess(hookScript.caughtFish);
                return;
            }
        }
        ResetFishingState();
    }

    private void ResetFishingState()
    {
        if (cameraFollow != null) cameraFollow.secondaryTarget = null;

        if (currentHook != null) Destroy(currentHook);

        isCasting = false;

        if (BoatController.instance != null)
            BoatController.instance.canMove = true;

        lineRenderer.enabled = false;
    }

    [Header("Debug")]
    public bool showGizmos = true;

    void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        if (rodTip != null)
        {
            Gizmos.color = Color.magenta;
            Vector3 start = rodTip.position;

            // Fırlatma yönünü görselleştir
            Vector3 dir = throwDirection.normalized;
            // Eğer tekne sola dönükse (basit kontrol - editörde çalışmayabilir ama runtime'da fikir verir)
            if (transform.right.x < 0) dir.x *= -1;

            Vector3 end = start + dir * 3f; // 3 birim uzunluğunda bir çizgi

            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(end, 0.1f); // Ok ucu niyetine

            // Kancanın sağ/sol limitini göster
            if (limitHookHorizontal)
            {
                float max = Mathf.Max(0f, GetMaxHorizontalOffset());
                Gizmos.color = new Color(0.2f, 1f, 1f, 0.9f);

                Vector3 left = new Vector3(start.x - max, start.y, start.z);
                Vector3 right = new Vector3(start.x + max, start.y, start.z);

                const float h = 6f;
                Gizmos.DrawLine(left + Vector3.up * h, left + Vector3.down * h);
                Gizmos.DrawLine(right + Vector3.up * h, right + Vector3.down * h);
                Gizmos.DrawLine(left, right);
            }

            // Kancanın cast-origin yarıçap limitini (approx: rod tip) göster
            if (limitHookRadiusFromCastPoint)
            {
                float r = Mathf.Max(0f, GetMaxDistanceFromCastPoint());
                if (r > 0.01f)
                {
                    Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.55f);
                    Vector3 o = new Vector3(start.x, start.y, 0f);
                    Gizmos.DrawWireSphere(o, r);

                    float e = Mathf.Max(0f, lineElasticity);
                    if (e > 0.01f)
                    {
                        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.25f);
                        Gizmos.DrawWireSphere(o, r + e);
                    }
                }
            }

#if UNITY_EDITOR
            Handles.color = new Color(1f, 1f, 1f, 0.9f);
            Handles.Label(start + Vector3.up * 0.35f, "Rod Tip");
#endif
        }
    }
}
