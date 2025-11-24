using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(LineRenderer))]
public class FishingRod : MonoBehaviour
{
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

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.enabled = false;
        
        // Line Renderer ayarları (ince bir ip gibi görünmesi için)
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = Color.white;

        if (Camera.main != null)
            cameraFollow = Camera.main.GetComponent<CameraFollow>();
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

        // Misina çizimi
        if (currentHook != null)
        {
            UpdateFishingLine();
            
            // Tekne hareketini durdur
            if (BoatController.instance != null)
                BoatController.instance.canMove = false;
        }
        else
        {
            lineRenderer.enabled = false;
            // Tekne hareketini serbest bırak (Mini oyun yoksa)
            if (BoatController.instance != null && !isMiniGameActive)
                BoatController.instance.canMove = true;
        }
    }

    void UpdateFishingLine()
    {
        lineRenderer.enabled = true;
        
        // Bezier Eğrisi ile ipin sarkmasını simüle et
        int pointCount = 20;
        lineRenderer.positionCount = pointCount;
        
        Vector3 p0 = rodTip.position;
        Vector3 p2 = currentHook.transform.position;
        
        // Orta kontrol noktası (Sarkma miktarı)
        float distance = Vector3.Distance(p0, p2);
        float sagAmount = distance * 0.3f; 
        
        // Kanca suyun altındaysa sarkma azalır
        // Optimize: Check instance once
        var waveManager = WaveManager.instance;
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

        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)(pointCount - 1);
            // Optimize: Bezier calculation
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            
            Vector3 pos = uu * p0 + 2 * u * t * p1 + tt * p2;
            
            lineRenderer.SetPosition(i, pos);
        }
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

        hookRb.AddForce(dir * castForce, ForceMode2D.Impulse);

        // Kameraya kancayı bildir
        if (cameraFollow != null) cameraFollow.secondaryTarget = currentHook.transform;
    }

    public void ReelInSuccess(Fish fish)
    {
        if (SoundManager.instance != null)
            SoundManager.instance.PlaySFX(SoundManager.instance.catchSound, 1f, 0.2f);

        if (GameManager.instance != null)
        {
            GameManager.instance.AddScore(fish.scoreValue);
            
            string fishName = !string.IsNullOrEmpty(fish.fishName) ? fish.fishName : "Fish";
            GameManager.instance.ShowFeedback(fishName + " CAUGHT!\n+" + fish.scoreValue);
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
