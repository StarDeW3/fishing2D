using UnityEngine;
using TMPro; // TextMeshPro için gerekli
using UnityEngine.UI; // Canvas ve UI bileşenleri için
using UnityEngine.SceneManagement; // Sahne yönetimi için
using System.Collections; // Coroutines için gerekli

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("UI Referansları")]
    public TextMeshProUGUI moneyText; // Score yerine Money
    public TextMeshProUGUI timeText;
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI feedbackText; 
    public GameObject pausePanel;
    public GameObject shopPanel; // Shop UI

    [Header("Oyun Durumu")]
    public int money = 0; // Para birimi
    public bool isGameActive = true;
    public bool isPaused = false;

    private DayNightCycle dayNightCycle;
    private Coroutine feedbackCoroutine;
    private int lastMinute = -1;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        // Para Yükle
        money = PlayerPrefs.GetInt("Money", 0);

        CreateUI();
        
        // FishingMiniGame'i kontrol et ve ekle
        if (FindFirstObjectByType<FishingMiniGame>() == null)
        {
            gameObject.AddComponent<FishingMiniGame>();
        }
        
        // UpgradeManager kontrolü
        if (FindFirstObjectByType<UpgradeManager>() == null)
        {
            gameObject.AddComponent<UpgradeManager>();
        }
    }

    void CreateUI()
    {
        // 1. Canvas Oluştur (Eğer yoksa)
        GameObject canvasObj = GameObject.Find("Canvas");
        Canvas canvas;
        if (canvasObj == null)
        {
            canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvas = canvasObj.GetComponent<Canvas>();
        }

        // 2. Money Text Oluştur
        if (moneyText == null)
        {
            GameObject scoreObj = new GameObject("MoneyText");
            scoreObj.transform.SetParent(canvas.transform, false);
            moneyText = scoreObj.AddComponent<TextMeshProUGUI>();
            
            // Stil
            moneyText.fontSize = 48;
            moneyText.color = new Color(0.5f, 1f, 0.5f); // Yeşil (Para)
            moneyText.alignment = TextAlignmentOptions.TopLeft;
            moneyText.fontStyle = FontStyles.Bold;
            moneyText.outlineWidth = 0.2f;
            moneyText.outlineColor = Color.black;

            // Konum (Sol Üst)
            RectTransform rect = moneyText.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(50, -50);
            rect.sizeDelta = new Vector2(400, 100);
        }

        // 3. Time Text Oluştur
        if (timeText == null)
        {
            GameObject timeObj = new GameObject("TimeText");
            timeObj.transform.SetParent(canvas.transform, false);
            timeText = timeObj.AddComponent<TextMeshProUGUI>();

            // Stil
            timeText.fontSize = 48;
            timeText.color = Color.white;
            timeText.alignment = TextAlignmentOptions.TopRight;
            timeText.fontStyle = FontStyles.Bold;
            timeText.outlineWidth = 0.2f;
            timeText.outlineColor = Color.black;

            // Konum (Sağ Üst)
            RectTransform rect = timeText.rectTransform;
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-50, -50);
            rect.sizeDelta = new Vector2(300, 100);
        }

        // 4. Feedback Text (YENİ)
        if (feedbackText == null)
        {
            GameObject fbObj = new GameObject("FeedbackText");
            fbObj.transform.SetParent(canvas.transform, false);
            feedbackText = fbObj.AddComponent<TextMeshProUGUI>();
            
            feedbackText.fontSize = 64;
            feedbackText.color = Color.yellow;
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.fontStyle = FontStyles.Bold;
            feedbackText.outlineWidth = 0.2f;
            feedbackText.outlineColor = Color.black;
            
            RectTransform rect = feedbackText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, 200); // Ekranın biraz üstünde
            rect.sizeDelta = new Vector2(800, 100);
            
            feedbackText.gameObject.SetActive(false);
        }

        // 5. Pause Panel Oluştur
        if (pausePanel == null)
        {
            pausePanel = new GameObject("PausePanel");
            pausePanel.transform.SetParent(canvas.transform, false);
            
            // Yarı saydam siyah arka plan
            Image bg = pausePanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.5f);
            
            // Tam ekran yap
            RectTransform rect = pausePanel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // "PAUSED" Yazısı
            GameObject textObj = new GameObject("PauseText");
            textObj.transform.SetParent(pausePanel.transform, false);
            TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
            txt.text = "PAUSED";
            txt.fontSize = 80;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;
            
            pausePanel.SetActive(false);
        }

        // 6. Game Over Panel Oluştur
        if (gameOverPanel == null)
        {
            gameOverPanel = new GameObject("GameOverPanel");
            gameOverPanel.transform.SetParent(canvas.transform, false);
            
            // Yarı saydam siyah arka plan
            Image bg = gameOverPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.85f);
            
            // Tam ekran yap
            RectTransform rect = gameOverPanel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // "GAME OVER" Başlığı
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(gameOverPanel.transform, false);
            TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = "GAME OVER";
            title.fontSize = 96;
            title.color = new Color(1f, 0.2f, 0.2f); // Kırmızımsı
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;
            
            RectTransform titleRect = title.rectTransform;
            titleRect.anchoredPosition = new Vector2(0, 100);
            titleRect.sizeDelta = new Vector2(800, 200);

            // Final Score Yazısı
            GameObject finalScoreObj = new GameObject("FinalScoreText");
            finalScoreObj.transform.SetParent(gameOverPanel.transform, false);
            finalScoreText = finalScoreObj.AddComponent<TextMeshProUGUI>();
            finalScoreText.fontSize = 64;
            finalScoreText.color = Color.white;
            finalScoreText.alignment = TextAlignmentOptions.Center;
            
            RectTransform fsRect = finalScoreText.rectTransform;
            fsRect.anchoredPosition = new Vector2(0, -50);
            fsRect.sizeDelta = new Vector2(800, 100);

            // Restart Button
            GameObject btnObj = new GameObject("RestartButton");
            btnObj.transform.SetParent(gameOverPanel.transform, false);
            Button btn = btnObj.AddComponent<Button>();
            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = Color.white;
            
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchoredPosition = new Vector2(0, -200);
            btnRect.sizeDelta = new Vector2(200, 60);
            
            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "RESTART";
            btnText.fontSize = 32;
            btnText.color = Color.black;
            btnText.alignment = TextAlignmentOptions.Center;
            
            RectTransform btnTextRect = btnText.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            btn.onClick.AddListener(RestartGame);

            gameOverPanel.SetActive(false);
        }
    }

    void Start()
    {
        dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        UpdateMoneyUI();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        
        // Shop UI oluştur (Basitçe)
        CreateShopUI();
    }

    void Update()
    {
        // Pause Kontrolü (P veya ESC tuşu)
        if (UnityEngine.InputSystem.Keyboard.current != null && 
           (UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame || 
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            TogglePause();
        }
        
        // Shop Kontrolü (B tuşu - Buy)
        if (UnityEngine.InputSystem.Keyboard.current != null && 
            UnityEngine.InputSystem.Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleShop();
        }

        if (!isGameActive || isPaused) return;

        // Saati göster (DayNightCycle'dan alıp)
        if (dayNightCycle != null && timeText != null)
        {
            // 0.0 - 1.0 arasını 24 saate çevir
            float hour = dayNightCycle.timeOfDay * 24f;
            // Gece yarısı ofseti (0.0 = 00:00 olsun diye)
            // DayNightCycle'da 0.25 sabah, 0.0 gece yarısı demiştik.
            // Basitçe:
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.FloorToInt((hour - h) * 60f);
            
            // Optimization: Only update text if minute changed
            if (m != lastMinute)
            {
                lastMinute = m;
                timeText.SetText("{0:00}:{1:00}", h, m);
            }
        }
    }

    public void ShowFeedback(string message, Color? color = null)
    {
        if (feedbackText != null)
        {
            if (feedbackCoroutine != null) StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = StartCoroutine(FeedbackRoutine(message, color ?? Color.yellow));
        }
    }

    private IEnumerator FeedbackRoutine(string message, Color color)
    {
        feedbackText.text = message;
        feedbackText.color = color;
        feedbackText.gameObject.SetActive(true);
        feedbackText.alpha = 1f;
        feedbackText.rectTransform.anchoredPosition = new Vector2(0, 100); // Reset position

        float timer = 2f;
        while (timer > 0)
        {
            if (!isPaused)
            {
                timer -= Time.deltaTime;
                // Yukarı doğru süzülme efekti
                feedbackText.rectTransform.anchoredPosition += Vector2.up * Time.deltaTime * 50f;
                // Yavaşça kaybolma
                feedbackText.alpha = Mathf.Clamp01(timer); 
            }
            yield return null;
        }
        
        feedbackText.gameObject.SetActive(false);
    }

    public void AddMoney(int amount)
    {
        money += amount;
        PlayerPrefs.SetInt("Money", money);
        PlayerPrefs.Save();
        
        // Efekt (Para rengi)
        if (moneyText != null) moneyText.color = Color.green;
        
        UpdateMoneyUI();
    }
    
    public bool SpendMoney(int amount)
    {
        if (money >= amount)
        {
            money -= amount;
            PlayerPrefs.SetInt("Money", money);
            PlayerPrefs.Save();
            UpdateMoneyUI();
            return true;
        }
        return false;
    }

    void UpdateMoneyUI()
    {
        if (moneyText != null)
            moneyText.SetText("${0}", money);
    }

    public void GameOver()
    {
        isGameActive = false;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (finalScoreText != null)
                finalScoreText.text = "Total Money: $" + money;
        }
        
        // Oyunu durdurmak istersen:
        Time.timeScale = 0f; 
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        
        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
        }
        
        // Shop açıksa kapat
        if (shopPanel != null && shopPanel.activeSelf && !isPaused)
        {
            shopPanel.SetActive(false);
        }
    }
    
    public void ToggleShop()
    {
        if (shopPanel == null) return;
        
        bool isActive = !shopPanel.activeSelf;
        shopPanel.SetActive(isActive);
        
        // Shop açılınca oyunu durdur
        isPaused = isActive;
        Time.timeScale = isPaused ? 0f : 1f;
        
        if (isActive) UpdateShopUI();
    }
    
    void CreateShopUI()
    {
        if (shopPanel != null) return;
        
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null) return;
        
        shopPanel = new GameObject("ShopPanel");
        shopPanel.transform.SetParent(canvasObj.transform, false);
        
        // Arkaplan
        Image bg = shopPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);
        
        RectTransform rect = shopPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.1f);
        rect.anchorMax = new Vector2(0.9f, 0.9f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        
        // Başlık
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(shopPanel.transform, false);
        TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "FISHING SHOP";
        title.fontSize = 64;
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.anchoredPosition = new Vector2(0, 350);
        title.rectTransform.sizeDelta = new Vector2(600, 100);
        
        // Kapat Butonu
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(shopPanel.transform, false);
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        Image closeImg = closeBtnObj.AddComponent<Image>();
        closeImg.color = Color.red;
        closeBtnObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(500, 350);
        closeBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(50, 50);
        closeBtn.onClick.AddListener(ToggleShop);
        
        GameObject closeTxtObj = new GameObject("Text");
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        TextMeshProUGUI closeTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
        closeTxt.text = "X";
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.rectTransform.anchorMin = Vector2.zero;
        closeTxt.rectTransform.anchorMax = Vector2.one;
        
        // Upgrade Butonları Container
        GameObject container = new GameObject("Container");
        container.transform.SetParent(shopPanel.transform, false);
        GridLayoutGroup grid = container.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(300, 300);
        grid.spacing = new Vector2(50, 50);
        grid.childAlignment = TextAnchor.MiddleCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        
        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.1f, 0.1f);
        containerRect.anchorMax = new Vector2(0.9f, 0.8f);
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;
        
        shopPanel.SetActive(false);
    }
    
    void UpdateShopUI()
    {
        if (shopPanel == null || UpgradeManager.instance == null) return;
        
        Transform container = shopPanel.transform.Find("Container");
        if (container == null) return;
        
        // Eski butonları temizle
        foreach (Transform child in container) Destroy(child.gameObject);
        
        // Yeni butonları oluştur
        foreach (var upg in UpgradeManager.instance.upgrades)
        {
            GameObject btnObj = new GameObject("Upgrade_" + upg.name);
            btnObj.transform.SetParent(container, false);
            
            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.2f, 0.3f);
            
            Button btn = btnObj.AddComponent<Button>();
            
            GameObject textObj = new GameObject("Info");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 24;
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            
            int level = UpgradeManager.instance.GetLevel(upg.type);
            int cost = UpgradeManager.instance.GetCost(upg.type);
            float currentVal = UpgradeManager.instance.GetValue(upg.type);
            
            string costText = (cost < 0) ? "MAX" : "$" + cost;
            txt.text = $"{upg.name}\nLvl {level}\nVal: {currentVal:0.0}\n\n{costText}";
            
            if (cost > 0 && money >= cost)
            {
                btnImg.color = new Color(0.2f, 0.5f, 0.2f); // Alınabilir (Yeşil)
                btn.onClick.AddListener(() => {
                    if (UpgradeManager.instance.TryUpgrade(upg.type))
                    {
                        UpdateShopUI(); // UI güncelle
                        if (SoundManager.instance != null) SoundManager.instance.PlaySFX(SoundManager.instance.catchSound); // Ka-ching!
                    }
                });
            }
            else if (cost < 0)
            {
                btnImg.color = Color.gray; // Max
                btn.interactable = false;
            }
            else
            {
                btnImg.color = new Color(0.5f, 0.2f, 0.2f); // Para yetmiyor (Kırmızı)
                btn.interactable = false;
            }
        }
    }
}
