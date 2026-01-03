using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class FishingMiniGame : MonoBehaviour
{
    public static FishingMiniGame instance;

    [Header("UI Referansları")]
    public GameObject gamePanel;
    public RectTransform fishIcon;
    public RectTransform catchArea;
    public Image progressBar;
    public TextMeshProUGUI statusText; // Yeni durum metni
    public TextMeshProUGUI distanceText; // Mesafe metni

    [Header("Oyun Ayarları")]
    public float barSize = 400f;
    public float catchAreaSize = 150f;
    public float catchSpeed = 0.4f; // Biraz dengelendi
    public float drainSpeed = 0.15f; // Biraz daha cezalandırıcı ama adil

    private bool isPlaying = false;
    public bool IsPlaying => isPlaying; // Dışarıdan erişim için property

    private float currentProgress = 0f;
    public float CurrentProgress => currentProgress; // Hook scripti erişebilsin diye
    private float fishPosition = 0.5f;
    private float catchPosition = 0.5f;
    private float catchVelocity = 0f;

    // Balık hareketi için
    private float fishTarget = 0.5f;
    private float fishMoveTimer = 0f;
    private float currentDifficulty = 1f;

    // Efektler için
    private Vector3 originalPanelPos;
    private float shakeTimer = 0f;
    
    private float initialDistance = 10f;
    private float actualCatchSpeed;
    private float actualDrainSpeed;
    private float lastDistanceDisplayed = -1f; // UI optimizasyonu için

    private bool lastIsInside = false;
    private bool hasInsideState = false;

    // Callbackler
    private System.Action onWin;
    private System.Action onLose;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    void Start()
    {
        // Eğer inspector'dan atanmamışsa otomatik oluştur
        if (gamePanel == null)
        {
            CreateUI();
        }
        
        if (gamePanel != null)
            originalPanelPos = gamePanel.transform.localPosition;
    }

    void Update()
    {
        if (!isPlaying) return;

        UpdateFishMovement();
        UpdatePlayerInput();
        CheckProgress();
        UpdateUI();
        UpdateEffects();
    }

    public void StartGame(float difficulty, float distance, System.Action winCallback, System.Action loseCallback)
    {
        if (isPlaying) return; // Zaten oyun varsa yenisini başlatma

        // UI referansları yoksa oluşturmayı dene; yine de yoksa crash yerine güvenli şekilde iptal et.
        if (gamePanel == null || fishIcon == null || catchArea == null || progressBar == null)
        {
            if (gamePanel == null) CreateUI();
        }

        if (gamePanel == null || fishIcon == null || catchArea == null || progressBar == null)
        {
            Debug.LogError("FishingMiniGame: UI referanslari eksik, minigame baslatilamiyor.");
            // Ödül vermemek için lose callback.
            loseCallback?.Invoke();
            return;
        }

        currentDifficulty = difficulty;
        initialDistance = distance;
        lastDistanceDisplayed = -1f; // Reset
        onWin = winCallback;
        onLose = loseCallback;

        isPlaying = true;
        currentProgress = 0.25f; // %25'ten başla
        fishPosition = 0.5f;
        catchPosition = 0.5f;
        catchVelocity = 0f;

        hasInsideState = false;

        // Mesafeye göre hızı ayarla (Uzaksa daha yavaş dolsun)
        float referenceDist = 8f; 
        float factor = referenceDist / Mathf.Max(distance, 1f);
        factor = Mathf.Clamp(factor, 0.5f, 1.5f); // Çok aşırı yavaşlamasın (0.2 -> 0.5)
        
        // Upgrade sisteminden hız al
        float baseReelSpeed = catchSpeed;
        if (UpgradeManager.instance != null)
        {
            baseReelSpeed = UpgradeManager.instance.GetValue(UpgradeType.ReelSpeed);
        }
        
        actualCatchSpeed = baseReelSpeed * factor;

        // Line Strength: outside penalty gets reduced (harder to "lose the fish")
        float strengthPct = 0f;
        if (UpgradeManager.instance != null)
            strengthPct = Mathf.Clamp(UpgradeManager.instance.GetValue(UpgradeType.LineStrength), 0f, 75f);
        actualDrainSpeed = drainSpeed * (1f - (strengthPct / 100f));

        // Zorluğa göre yeşil alan boyutunu ayarla
        float baseSize = 150f;
        if (UpgradeManager.instance != null)
        {
            baseSize = UpgradeManager.instance.GetValue(UpgradeType.BarSize);
        }
        
        float newSize = Mathf.Lerp(baseSize, baseSize * 0.5f, (difficulty - 1) / 4f);
        catchArea.sizeDelta = new Vector2(catchArea.sizeDelta.x, newSize);
        catchAreaSize = newSize;

        gamePanel.SetActive(true);
        if (statusText != null)
        {
            statusText.text = LocalizationManager.T("minigame.hooked", "HOOKED!");
            statusText.color = Color.white;
        }

        if (BoatController.instance != null) BoatController.instance.canMove = false;
    }

    public void EndGame(bool win)
    {
        isPlaying = false;
        gamePanel.SetActive(false);

        if (win)
        {
            onWin?.Invoke();
            // SoundManager çağrısı FishingRod içinde yapılıyor (ReelInSuccess) ama burada da olabilir
        }
        else
        {
            onLose?.Invoke();
            if (SoundManager.instance != null)
                SoundManager.instance.PlaySFX(SoundManager.instance.escapeSound, 1f, 0.1f);
        }
    }

    void UpdateFishMovement()
    {
        fishMoveTimer -= Time.deltaTime;
        if (fishMoveTimer <= 0)
        {
            // Yeni hedef belirle
            fishTarget = Random.value;

            // Balık bazen beklesin (Daha doğal)
            if (Random.value > 0.7f) fishTarget = fishPosition;

            fishMoveTimer = Random.Range(1f, 2.5f);
        }

        // Hava durumu etkisi (Fırtınada balık daha çok kaçar)
        float weatherMultiplier = 1f;

        // Balık hareketi
        float speed = Time.deltaTime * (0.2f + (currentDifficulty * 0.1f)) * weatherMultiplier;
        fishPosition = Mathf.MoveTowards(fishPosition, fishTarget, speed);

        // Kenarlarda çok durmasın
        if (fishPosition < 0.05f && Random.value > 0.9f) fishTarget = 0.3f;
        if (fishPosition > 0.95f && Random.value > 0.9f) fishTarget = 0.7f;
    }

    void UpdatePlayerInput()
    {
        bool isPressing = false;
        
        // Input kontrolünü optimize et (Null checkleri azalt)
        var kb = Keyboard.current;
        if (kb != null && kb.spaceKey.isPressed) isPressing = true;
        
        if (!isPressing) // Eğer klavye basılı değilse mouse'a bak
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) isPressing = true;
        }

        // Daha tok fizik (Snappy) - Hız düşürüldü
        float acceleration = isPressing ? 8f : -6f; 
        catchVelocity += acceleration * Time.deltaTime;

        // Yüksek sürtünme (Kontrolü kolaylaştırır)
        catchVelocity = Mathf.Lerp(catchVelocity, 0, Time.deltaTime * 5f);

        catchVelocity = Mathf.Clamp(catchVelocity, -3f, 3f);
        catchPosition += catchVelocity * Time.deltaTime;        // Bounce
        if (catchPosition < 0)
        {
            catchPosition = 0;
            catchVelocity = 0;
        }
        if (catchPosition > 1)
        {
            catchPosition = 1;
            catchVelocity = 0;
        }
    }

    void CheckProgress()
    {
        float areaRatio = catchAreaSize / barSize;
        float minCatch = catchPosition - (areaRatio / 2f);
        float maxCatch = catchPosition + (areaRatio / 2f);

        bool isInside = fishPosition >= minCatch && fishPosition <= maxCatch;

        if (isInside)
        {
            currentProgress += actualCatchSpeed * Time.deltaTime;
            if (statusText != null && (!hasInsideState || lastIsInside != isInside))
            {
                statusText.text = LocalizationManager.T("minigame.reeling", "REELING!");
                statusText.color = Color.green;
            }
        }
        else
        {
            float drain = (actualDrainSpeed > 0f) ? actualDrainSpeed : drainSpeed;
            currentProgress -= drain * Time.deltaTime;
            if (statusText != null && (!hasInsideState || lastIsInside != isInside))
            {
                statusText.text = LocalizationManager.T("minigame.escaping", "ESCAPING!");
                statusText.color = Color.red;
            }

            // Shake efekti başlat
            shakeTimer = 0.1f;
        }

        hasInsideState = true;
        lastIsInside = isInside;

        currentProgress = Mathf.Clamp01(currentProgress);

        if (currentProgress >= 1f) EndGame(true);
        else if (currentProgress <= 0f) EndGame(false);
    }

    void UpdateUI()
    {
        if (fishIcon == null || catchArea == null || progressBar == null) return;

        // Track boyutunu hesapla (barSize değil, gerçek track boyutu)
        float trackHeight = barSize - 20; // Track'in offset'lerini hesaba kat
        float halfTrack = trackHeight / 2f;
        
        // Fish icon pozisyonu - sınırlar içinde
        float fishY = (fishPosition - 0.5f) * trackHeight;
        fishIcon.anchoredPosition = new Vector2(0, fishY);

        // Catch area pozisyonu - yeşil çubuğun taşmasını önle
        float halfCatchArea = catchAreaSize / 2f;
        float maxCatchY = halfTrack - halfCatchArea;
        float minCatchY = -halfTrack + halfCatchArea;
        
        float catchY = (catchPosition - 0.5f) * trackHeight;
        catchY = Mathf.Clamp(catchY, minCatchY, maxCatchY);
        catchArea.anchoredPosition = new Vector2(0, catchY);

        progressBar.fillAmount = currentProgress;

        // Renk değişimi (Kırmızı -> Sarı -> Yeşil)
        progressBar.color = Color.Lerp(Color.red, Color.green, currentProgress);

        if (distanceText != null)
        {
            // Yüzde yerine kalan mesafeyi göster (Daha şık)
            float distance = Mathf.Lerp(initialDistance, 0f, currentProgress);
            
            // String oluşturma maliyetini düşürmek için sadece değer değiştiğinde güncelle
            if (Mathf.Abs(distance - lastDistanceDisplayed) > 0.1f)
            {
                distanceText.SetText(LocalizationManager.T("minigame.distanceFmt", "{0:0.0} m"), distance);
                lastDistanceDisplayed = distance;
            }
            
            // Yaklaştıkça metin büyüsün ve rengi değişsin
            distanceText.fontSize = Mathf.Lerp(24f, 32f, currentProgress);
            distanceText.color = Color.Lerp(Color.white, Color.cyan, currentProgress);
        }
    }

    void UpdateEffects()
    {
        if (gamePanel == null) return;

        if (shakeTimer > 0)
        {
            shakeTimer -= Time.deltaTime;
            float x = Random.Range(-3f, 3f);
            float y = Random.Range(-3f, 3f);
            gamePanel.transform.localPosition = originalPanelPos + new Vector3(x, y, 0);
        }
        else
        {
            // Reset position
            if (gamePanel != null)
                gamePanel.transform.localPosition = originalPanelPos;
        }
    }

    void CreateUI()
    {
        GameObject canvasObj = null;
        if (GameManager.instance != null)
        {
            Transform t = GameManager.instance.CanvasTransform;
            if (t != null) canvasObj = t.gameObject;
        }

        if (canvasObj == null)
        {
            Canvas c = FindFirstObjectByType<Canvas>();
            if (c != null) canvasObj = c.gameObject;
        }

        if (canvasObj == null) 
        {
            Debug.LogError("FishingMiniGame: Canvas bulunamadi! UI olusturulamiyor.");
            return;
        }

        Transform oldPanel = canvasObj.transform.Find("FishingMiniGamePanel");
        if (oldPanel != null) Destroy(oldPanel.gameObject);

        // 1. Ana Panel - Sağ kenara yakın, dikey çubuk
        gamePanel = new GameObject("FishingMiniGamePanel");
        gamePanel.transform.SetParent(canvasObj.transform, false);

        RectTransform panelRect = gamePanel.AddComponent<RectTransform>();
        // Sağ tarafta, ortadan biraz yukarıda
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-30, 30);
        panelRect.sizeDelta = new Vector2(80, barSize + 40);

        Image bg = gamePanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);
        
        // Glow efekti
        Outline glow = gamePanel.AddComponent<Outline>();
        glow.effectColor = new Color(0.3f, 0.6f, 1f, 0.4f);
        glow.effectDistance = new Vector2(4, -4);

        // Border
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(gamePanel.transform, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(4, 4);
        borderRect.offsetMax = new Vector2(-4, -4);
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(0.1f, 0.1f, 0.12f, 1f);

        // 2. Track
        GameObject trackObj = new GameObject("Track");
        trackObj.transform.SetParent(borderObj.transform, false);
        RectTransform trackRect = trackObj.AddComponent<RectTransform>();
        trackRect.anchorMin = Vector2.zero;
        trackRect.anchorMax = Vector2.one;
        trackRect.offsetMin = new Vector2(10, 10);
        trackRect.offsetMax = new Vector2(-25, -10); // Sağda boşluk

        Image trackImg = trackObj.AddComponent<Image>();
        trackImg.color = new Color(0.05f, 0.15f, 0.3f, 1f);

        // 3. Catch Area
        GameObject catchObj = new GameObject("CatchArea");
        catchObj.transform.SetParent(trackObj.transform, false);
        catchArea = catchObj.AddComponent<RectTransform>();
        catchArea.anchorMin = new Vector2(0, 0.5f);
        catchArea.anchorMax = new Vector2(1, 0.5f);
        catchArea.sizeDelta = new Vector2(0, catchAreaSize);

        Image catchImg = catchObj.AddComponent<Image>();
        catchImg.color = new Color(0.6f, 1f, 0.2f, 0.6f); // Daha parlak

        // 4. Fish Icon
        GameObject fishObj = new GameObject("FishIcon");
        fishObj.transform.SetParent(trackObj.transform, false);
        fishIcon = fishObj.AddComponent<RectTransform>();
        fishIcon.anchorMin = new Vector2(0.5f, 0.5f);
        fishIcon.anchorMax = new Vector2(0.5f, 0.5f);
        fishIcon.sizeDelta = new Vector2(30, 30);

        Image fishImg = fishObj.AddComponent<Image>();
        fishImg.color = new Color(1f, 0.7f, 0.1f);
        fishIcon.localRotation = Quaternion.Euler(0, 0, 45);

        // 5. Progress Bar
        GameObject progressBgObj = new GameObject("ProgressBG");
        progressBgObj.transform.SetParent(borderObj.transform, false);
        RectTransform pBgRect = progressBgObj.AddComponent<RectTransform>();
        pBgRect.anchorMin = new Vector2(1, 0);
        pBgRect.anchorMax = new Vector2(1, 1);
        pBgRect.offsetMin = new Vector2(-20, 10);
        pBgRect.offsetMax = new Vector2(-8, -10);

        Image pBgImg = progressBgObj.AddComponent<Image>();
        pBgImg.color = Color.black;

        GameObject progressFillObj = new GameObject("ProgressFill");
        progressFillObj.transform.SetParent(progressBgObj.transform, false);
        RectTransform pFillRect = progressFillObj.AddComponent<RectTransform>();
        pFillRect.anchorMin = Vector2.zero;
        pFillRect.anchorMax = Vector2.one;
        pFillRect.offsetMin = new Vector2(2, 2);
        pFillRect.offsetMax = new Vector2(-2, -2);

        progressBar = progressFillObj.AddComponent<Image>();
        progressBar.color = Color.green;
        progressBar.type = Image.Type.Filled;
        progressBar.fillMethod = Image.FillMethod.Vertical;
        progressBar.fillOrigin = 0;

        // 6. Status Text (YENİ)
        GameObject textObj = new GameObject("StatusText");
        textObj.transform.SetParent(gamePanel.transform, false);
        statusText = textObj.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            statusText.font = TMP_Settings.defaultFontAsset;
        statusText.fontSize = 24;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.fontStyle = FontStyles.Bold;
        statusText.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform textRect = statusText.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 1f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0, 30); // Panelin üstünde
        textRect.sizeDelta = new Vector2(200, 50);

        // 7. Distance Text (Panelin altında)
        GameObject distObj = new GameObject("DistanceText");
        distObj.transform.SetParent(gamePanel.transform, false);
        distanceText = distObj.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            distanceText.font = TMP_Settings.defaultFontAsset;
        distanceText.fontSize = 24;
        distanceText.color = Color.white;
        distanceText.alignment = TextAlignmentOptions.Center;
        distanceText.fontStyle = FontStyles.Bold;
        // Outline
        distanceText.outlineWidth = 0.2f;
        distanceText.outlineColor = Color.black;
        
        RectTransform distRect = distanceText.rectTransform;
        distRect.anchorMin = new Vector2(0.5f, 0f);
        distRect.anchorMax = new Vector2(0.5f, 0f);
        distRect.pivot = new Vector2(0.5f, 1f); // Üstten hizala (Panelin altına sarkıt)
        distRect.anchoredPosition = new Vector2(0, -10); 
        distRect.sizeDelta = new Vector2(200, 50);

        if (GameManager.instance != null)
            GameManager.instance.ApplyFont(gamePanel);

        gamePanel.SetActive(false);
    }
}
