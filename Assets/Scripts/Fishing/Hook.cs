using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Hook : MonoBehaviour
{
    [Header("Kontrol")]
    public float underwaterMoveSpeed = 10f; // Su altı hareket hızı
    public float airMoveSpeed = 2f;         // Havada hareket hızı

    private Rigidbody2D rb;
    private Vector2 inputDir; // Input'u Update'de alıp FixedUpdate'de kullanmak için

    public Fish caughtFish; // Yakalanan balık
    public FishingRod fishingRod; // Referans (FishingRod tarafından atanır)
    private bool isBusy = false; // Minigame sırasında meşgul mü?

    // Görsel efekt için
    private Vector3 catchPosition;
    private Transform targetRodTip;
    private float visualProgress = 0f;

    // Cache
    private WaveManager waveManager;
    private CircleCollider2D triggerCollider;
    private float baseTriggerRadius = 0.5f;
    private bool lastWasUnderwater = false;
    private float lastSplashTime = -999f;
    private const float SplashCooldown = 0.25f;
    private static FishingRod cachedRod;
    private float nextWaveManagerSearchTime = 0f;

    private float initialAnchorX;

    // Cast-origin limit + line break
    private Vector2 castOrigin;
    private bool hasCastOrigin = false;
    private float spawnedAtUnscaledTime = 0f;
    private float lineTension = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        triggerCollider = GetComponent<CircleCollider2D>();
        if (triggerCollider != null)
            baseTriggerRadius = triggerCollider.radius;

        // Çarpışma algılama modunu iyileştir (Hızlı hareketlerde içinden geçmeyi önler)
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Collider'ı Trigger yap (Fiziksel çarpışma yerine olay tetiklesin)
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        initialAnchorX = transform.position.x;

        castOrigin = rb != null ? rb.position : (Vector2)transform.position;
        hasCastOrigin = true;
        spawnedAtUnscaledTime = Time.unscaledTime;
    }

    void Start()
    {
        // WaveManager'ı cache'le (Eğer sahnede varsa)
        waveManager = WaveManager.instance;

        // Fish Radar upgrade: make it easier to detect/hook fish.
        if (triggerCollider != null && UpgradeManager.instance != null)
        {
            float radiusBonus = Mathf.Max(0f, UpgradeManager.instance.GetValue(UpgradeType.FishRadar));
            triggerCollider.radius = baseTriggerRadius + radiusBonus;
        }
    }

    public void SetCastOrigin(Vector2 origin)
    {
        castOrigin = origin;
        hasCastOrigin = true;
    }

    void Update()
    {
        // 1. Input Okuma (Update içinde yapılmalı - daha akıcı tepki için)
        float moveX = 0f;
        float moveY = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX = -1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX = 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveY = 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveY = -1f;
        }

        // Keyboard input is digital (0/±1). Avoid sqrt in Vector2.normalized.
        if (moveX == 0f && moveY == 0f)
        {
            inputDir = Vector2.zero;
        }
        else if (moveX != 0f && moveY != 0f)
        {
            const float invSqrt2 = 0.70710678f;
            inputDir = new Vector2(moveX * invSqrt2, moveY * invSqrt2);
        }
        else
        {
            inputDir = new Vector2(moveX, moveY);
        }

        // 2. Z pozisyonunu sabitleme
        if (Mathf.Abs(transform.position.z) > 0.01f)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, 0);
        }

        // 3. Mini oyun görselleştirmesi
        if (isBusy && FishingMiniGame.instance != null && FishingMiniGame.instance.IsPlaying && targetRodTip != null)
        {
            float targetProgress = FishingMiniGame.instance.CurrentProgress;
            // Yumuşak geçiş
            visualProgress = Mathf.Lerp(visualProgress, targetProgress, Time.deltaTime * 5f);

            // Pozisyonu güncelle (Başlangıç noktası ile olta ucu arasında)
            transform.position = Vector3.Lerp(catchPosition, targetRodTip.position, visualProgress);
        }
    }

    void FixedUpdate()
    {
        // Fiziksel hareketler FixedUpdate'de yapılmalı
        if (isBusy) return; // Meşgulse fizik uygulama

        // WaveManager kontrolü (Cache veya Instance)
        if (waveManager == null && Time.time >= nextWaveManagerSearchTime)
        {
            waveManager = WaveManager.instance;
            nextWaveManagerSearchTime = Time.time + 0.5f;
        }

        if (waveManager != null)
        {
            float waveHeight = waveManager.GetWaveHeight(transform.position.x);
            bool isUnderwater = transform.position.y < waveHeight;

            // Su altındaysa
            if (isUnderwater)
            {
                if (!lastWasUnderwater)
                {
                    if (SoundManager.instance != null && Time.time - lastSplashTime >= SplashCooldown)
                    {
                        lastSplashTime = Time.time;
                        SoundManager.instance.PlaySFX(SoundManager.instance.splashSound, 0.9f, 0.08f);
                    }

                    rb.linearDamping = 2f; // Su direnci
                    rb.gravityScale = 0.1f; // Yavaş batma (Kontrolü kolaylaştırmak için düşürdüm)
                }

                // Su altı hareketi
                if (inputDir != Vector2.zero)
                {
                    rb.AddForce(inputDir * underwaterMoveSpeed);
                }
            }
            else
            {
                // Havadaysa
                if (lastWasUnderwater)
                {
                    rb.linearDamping = 0f;
                    rb.gravityScale = 1f;
                }

                // Havada kontrol (daha az)
                if (inputDir != Vector2.zero)
                {
                    rb.AddForce(inputDir * airMoveSpeed);
                }
            }

            lastWasUnderwater = isUnderwater;
        }

        ApplyCastRadiusLimitAndTension();
        ApplyHorizontalLimit();
    }

    private void ApplyCastRadiusLimitAndTension()
    {
        if (rb == null) return;
        if (fishingRod == null) return;
        if (!fishingRod.limitHookRadiusFromCastPoint) return;

        float max = fishingRod.GetMaxDistanceFromCastPoint();
        if (max <= 0.001f) return;

        float elasticity = Mathf.Max(0f, fishingRod.lineElasticity);
        float hardMax = max + elasticity;

        Vector2 origin = hasCastOrigin ? castOrigin : rb.position;
        Vector2 p = rb.position;
        Vector2 d = p - origin;
        float dist = d.magnitude;

        // Smooth limit: allow a small elastic overshoot, then pull back with a spring.
        if (dist > max)
        {
            Vector2 dir = d / Mathf.Max(0.0001f, dist);
            float overshoot = dist - max;

            // Spring force only within the elastic zone.
            if (elasticity > 0.001f)
            {
                float springK = Mathf.Max(0f, fishingRod.lineSpringStrength);
                float damping = Mathf.Max(0f, fishingRod.lineSpringDamping);

                // Pull inward proportional to overshoot.
                Vector2 spring = -dir * (overshoot * springK);

                // Dampen only the outward component (prevents jitter when moving back in).
                float outwardVel = Vector2.Dot(rb.linearVelocity, dir);
                Vector2 damp = outwardVel > 0f ? (-dir * (outwardVel * damping)) : Vector2.zero;

                rb.AddForce(spring + damp);
            }

            // Safety hard clamp if something pushes it way out.
            if (dist > hardMax)
            {
                rb.position = origin + dir * hardMax;

                Vector2 v = rb.linearVelocity;
                float outward = Vector2.Dot(v, dir);
                if (outward > 0f)
                    v -= dir * outward;
                rb.linearVelocity = v;
            }
        }

        // Tension build: if player keeps trying to move outward while already at the limit.
        if (!fishingRod.enableLineBreak) return;
        if (Time.unscaledTime - spawnedAtUnscaledTime < fishingRod.GetLineBreakGraceTime())
            return;

        // Recompute after clamping
        p = rb.position;
        d = p - origin;
        dist = d.magnitude;

        float nearLimit = max > 0f ? Mathf.Clamp01(dist / max) : 0f;
        bool atLimit = nearLimit >= 0.98f;

        float pushOut = 0f;
        if (atLimit && inputDir != Vector2.zero && dist > 0.0001f)
        {
            Vector2 outDir = d / dist;
            pushOut = Mathf.Max(0f, Vector2.Dot(inputDir, outDir));
        }

        if (atLimit && pushOut > 0.1f)
        {
            // Build faster if we are deeper into the elastic zone.
            float overshoot01 = 0f;
            if (elasticity > 0.001f)
                overshoot01 = Mathf.Clamp01((dist - max) / elasticity);
            float factor = Mathf.Lerp(1f, 2f, overshoot01);

            lineTension += pushOut * factor * fishingRod.GetLineBreakBuildRate() * Time.fixedDeltaTime;
        }
        else
        {
            lineTension -= fishingRod.GetLineBreakRecoverRate() * Time.fixedDeltaTime;
        }

        float threshold = fishingRod.GetLineBreakThreshold();
        lineTension = Mathf.Clamp(lineTension, 0f, threshold);

        if (lineTension >= threshold)
        {
            fishingRod.BreakLine();
        }
    }

    private void ApplyHorizontalLimit()
    {
        if (rb == null) return;
        if (fishingRod == null) return;
        if (!fishingRod.limitHookHorizontal) return;

        float max = fishingRod.GetMaxHorizontalOffset();
        if (max <= 0f) return;

        float anchorX = (fishingRod.rodTip != null) ? fishingRod.rodTip.position.x : initialAnchorX;
        float minX = anchorX - max;
        float maxX = anchorX + max;

        Vector2 p = rb.position;
        float clampedX = Mathf.Clamp(p.x, minX, maxX);
        if (Mathf.Abs(clampedX - p.x) > 0.0001f)
        {
            rb.position = new Vector2(clampedX, p.y);
            Vector2 v = rb.linearVelocity;
            rb.linearVelocity = new Vector2(0f, v.y);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Erken çıkış kontrolleri
        if (caughtFish != null || isBusy) return;

        // Global kontrol (Başka bir yerde minigame oynanıyor mu?)
        if (FishingMiniGame.instance != null && FishingMiniGame.instance.IsPlaying) return;

        // TryGetComponent ile optimizasyon (GetComponent + null check yerine)
        if (other.TryGetComponent<Fish>(out Fish fish))
        {
            Debug.Log($"Balıkla temas edildi: {fish.name}");
            isBusy = true;
            StartMiniGame(fish);
        }
    }

    void StartMiniGame(Fish fish)
    {
        // Görsel efekt başlangıç değerleri
        catchPosition = transform.position;
        visualProgress = 0f;

        // FishingRod referansını kontrol et
        if (fishingRod == null)
        {
            if (cachedRod == null)
                cachedRod = FindFirstObjectByType<FishingRod>();
            fishingRod = cachedRod;
        }

        if (fishingRod == null)
        {
            Debug.LogError("FishingRod bulunamadı!");
            // Fallback: Direkt yakala
            caughtFish = fish;
            fish.Catch(transform);
            OnMiniGameWin(fish); // Direkt kazan
            return;
        }

        targetRodTip = fishingRod.rodTip;
        float distance = Vector3.Distance(transform.position, targetRodTip.position);

        DevLog.Info("Fishing", $"StartMiniGame fish='{(fish != null ? fish.fishName : "<null>")}' difficulty={(fish != null ? fish.difficulty : 0f):0.00} distance={distance:0.0}m");

        // Balığı hemen kancaya tak (Görsel olarak beraber hareket etsinler)
        fish.Catch(transform);

        // Kancayı durdur
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic; // Geçici olarak durdur

        if (FishingMiniGame.instance != null)
        {
            Sprite fishSprite = null;
            SpriteRenderer sr = fish.GetComponent<SpriteRenderer>();
            if (sr != null) fishSprite = sr.sprite;

            FishingMiniGame.instance.StartGame(fish.difficulty, distance, fishSprite,
                () => OnMiniGameWin(fish),
                () => OnMiniGameLose(fish)
            );
        }
        else
        {
            // Mini game yoksa direkt kazan (Fallback)
            OnMiniGameWin(fish);
        }
    }

    // Callback'leri ayırarak temiz kod
    void OnMiniGameWin(Fish fish)
    {
        isBusy = false;
        caughtFish = fish;
        DevLog.Info("Fishing", "MiniGame WIN (fish caught)");

        if (fishingRod != null)
        {
            fishingRod.ReelInSuccess(fish);
        }
        else
        {
            // Yedek plan (Rod yoksa)
            if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
        }
    }

    void OnMiniGameLose(Fish fish)
    {
        isBusy = false;
        DevLog.Info("Fishing", "MiniGame LOSE (fish escaped)");

        if (fish != null) fish.Escape();

        // Kancayı tekrar fiziksel yap
        if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;

        // Başarısız olunca otomatik çek
        if (fishingRod != null) fishingRod.ReelInFail();
    }

    [Header("Debug")]
    public bool showGizmos = true;

    [Tooltip("Editor'de, yalnızca obje seçiliyken gizmo çiz.")]
    public bool gizmosOnlyWhenSelected = false;

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

#if UNITY_EDITOR
        if (gizmosOnlyWhenSelected && !Selection.Contains(gameObject))
            return;
#endif

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.2f);

        // Trigger radius (FishRadar vs.)
        CircleCollider2D c = triggerCollider != null ? triggerCollider : GetComponent<CircleCollider2D>();
        if (c != null)
        {
            Gizmos.color = new Color(1f, 1f, 0.2f, 0.7f);
            Vector3 center = transform.TransformPoint(c.offset);
            Gizmos.DrawWireSphere(new Vector3(center.x, center.y, 0f), Mathf.Max(0f, c.radius));
        }

        if (fishingRod != null && fishingRod.rodTip != null)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.65f);
            Vector3 a = new Vector3(fishingRod.rodTip.position.x, fishingRod.rodTip.position.y, 0f);
            Vector3 b = new Vector3(transform.position.x, transform.position.y, 0f);
            Gizmos.DrawLine(a, b);
        }

        if (fishingRod != null)
        {
            if (fishingRod.limitHookHorizontal)
            {
                float max = Mathf.Max(0f, fishingRod.GetMaxHorizontalOffset());
                if (max > 0f)
                {
                    float anchorX = (fishingRod.rodTip != null) ? fishingRod.rodTip.position.x : initialAnchorX;
                    Gizmos.color = new Color(0.2f, 1f, 1f, 0.9f);

                    Vector3 a = new Vector3(anchorX, transform.position.y, 0f);
                    Vector3 left = new Vector3(anchorX - max, transform.position.y, 0f);
                    Vector3 right = new Vector3(anchorX + max, transform.position.y, 0f);

                    Gizmos.DrawLine(left, right);
                    Gizmos.DrawLine(a, transform.position);
                }
            }

            if (fishingRod.limitHookRadiusFromCastPoint)
            {
                float r = Mathf.Max(0f, fishingRod.GetMaxDistanceFromCastPoint());
                if (r > 0.01f)
                {
                    Vector2 origin2 = hasCastOrigin
                        ? castOrigin
                        : (fishingRod.rodTip != null ? (Vector2)fishingRod.rodTip.position : (Vector2)transform.position);

                    // Origin marker
                    Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
                    Gizmos.DrawSphere(new Vector3(origin2.x, origin2.y, 0f), 0.08f);

                    // Soft limit ring
                    Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.65f);
                    Gizmos.DrawWireSphere(new Vector3(origin2.x, origin2.y, 0f), r);

                    float e = Mathf.Max(0f, fishingRod.lineElasticity);
                    if (e > 0.01f)
                    {
                        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.25f);
                        Gizmos.DrawWireSphere(new Vector3(origin2.x, origin2.y, 0f), r + e);
                    }
                }
            }
        }

#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.9f);
            Handles.Label(transform.position + Vector3.up * 0.45f, $"Hook\nTension: {lineTension:0.00}");
        }
#endif
    }
}
