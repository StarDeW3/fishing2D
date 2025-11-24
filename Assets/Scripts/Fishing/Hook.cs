using UnityEngine;
using UnityEngine.InputSystem;

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Çarpışma algılama modunu iyileştir (Hızlı hareketlerde içinden geçmeyi önler)
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Collider'ı Trigger yap (Fiziksel çarpışma yerine olay tetiklesin)
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void Start()
    {
        // WaveManager'ı cache'le (Eğer sahnede varsa)
        waveManager = WaveManager.instance;
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
        inputDir = new Vector2(moveX, moveY).normalized;

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
        if (waveManager == null) waveManager = WaveManager.instance;

        if (waveManager != null)
        {
            float waveHeight = waveManager.GetWaveHeight(transform.position.x);
            
            // Su altındaysa
            if (transform.position.y < waveHeight)
            {
                rb.linearDamping = 2f; // Su direnci
                rb.gravityScale = 0.1f; // Yavaş batma (Kontrolü kolaylaştırmak için düşürdüm)
                
                // Su altı hareketi
                if (inputDir != Vector2.zero)
                {
                    rb.AddForce(inputDir * underwaterMoveSpeed);
                }
            }
            else
            {
                // Havadaysa
                rb.linearDamping = 0f;
                rb.gravityScale = 1f;

                // Havada kontrol (daha az)
                if (inputDir != Vector2.zero)
                {
                    rb.AddForce(inputDir * airMoveSpeed);
                }
            }
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
            fishingRod = FindFirstObjectByType<FishingRod>();
            if (fishingRod == null)
            {
                Debug.LogError("FishingRod bulunamadı!");
                // Fallback: Direkt yakala
                caughtFish = fish;
                fish.Catch(transform);
                return;
            }
        }
        
        targetRodTip = fishingRod.rodTip;
        float distance = Vector3.Distance(transform.position, targetRodTip.position);

        // Balığı hemen kancaya tak (Görsel olarak beraber hareket etsinler)
        fish.Catch(transform);

        // Kancayı durdur
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic; // Geçici olarak durdur

        if (FishingMiniGame.instance != null)
        {
            FishingMiniGame.instance.StartGame(fish.difficulty, distance, 
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
        Debug.Log("Balık Yakalandı!");
        
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
        Debug.Log("Balık Kaçtı!");
        
        if (fish != null) fish.Escape();
        
        // Kancayı tekrar fiziksel yap
        if (rb != null) rb.bodyType = RigidbodyType2D.Dynamic;
        
        // Başarısız olunca otomatik çek
        if (fishingRod != null) fishingRod.ReelInFail();
    }

    [Header("Debug")]
    public bool showGizmos = true;

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
    }
}
