using UnityEngine;
using UnityEngine.InputSystem;

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

    private GameObject currentHook;
    private LineRenderer lineRenderer;
    private bool isCasting = false;
    public bool isMiniGameActive = false; // Dışarıdan erişim için
    private CameraFollow cameraFollow;
    private WaveManager waveManager; // Cache
    private float nextCameraFollowLookupTime = 0f;

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
            // Space tuşuna basınca
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (!isCasting)
                {
                    CastLine();
                }
                else
                {
                    ReelIn();
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

    void CastLine()
    {
        isCasting = true;
        if (SoundManager.instance != null)
            SoundManager.instance.PlaySFX(SoundManager.instance.castSound, 1f, 0.1f); // Hafif pitch değişimi

        currentHook = Instantiate(hookPrefab, rodTip.position, Quaternion.identity);
        
        // Hook scriptine referans ata (Optimizasyon)
        Hook hookScript = currentHook.GetComponent<Hook>();
        if (hookScript != null)
        {
            hookScript.fishingRod = this;
        }

        Rigidbody2D hookRb = currentHook.GetComponent<Rigidbody2D>();
        
        // Fırlatma yönü: Ayarlanan vektörü kullan
        Vector2 dir = throwDirection.normalized;
        
        // Eğer tekne sola dönükse (basit kontrol) yönü çevir
        if (transform.right.x < 0)
             dir.x *= -1;

        // Upgrade sisteminden güç al
        float finalForce = castForce;
        if (UpgradeManager.instance != null)
        {
            finalForce = UpgradeManager.instance.GetValue(UpgradeType.CastDistance);
        }

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

    public void ReelInSuccess(Fish fish)
    {
        if (fish == null)
        {
            ResetFishingState();
            return;
        }

        // Lifetime istatistiklerini güncelle (yakalanan balık sayısı ve toplam kazanç)
        if (UIManager.instance != null)
        {
            UIManager.instance.OnFishCaught(fish.scoreValue);
        }

        if (SoundManager.instance != null)
            SoundManager.instance.PlaySFX(SoundManager.instance.catchSound, 1f, 0.2f);

        if (GameManager.instance != null)
        {
            GameManager.instance.AddMoney(fish.scoreValue); // Score -> Money
            
            string fishName = !string.IsNullOrEmpty(fish.fishName) ? fish.fishName : "Balık";
            GameManager.instance.ShowFeedback($"{fishName} yakaladın!\n+${fish.scoreValue}");
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
            GameManager.instance.ShowFeedback("FISH ESCAPED!", Color.red);
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
        }
    }
}
