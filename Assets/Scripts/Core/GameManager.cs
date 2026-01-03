using UnityEngine;
using TMPro; // TextMeshPro için gerekli
using UnityEngine.UI; // Canvas ve UI bileşenleri için
using UnityEngine.SceneManagement; // Sahne yönetimi için
using System.Collections; // Coroutines için gerekli
using System.Collections.Generic; // List<> için gerekli

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
    public GameObject mainMenuPanel; // Ana menü
    public GameObject settingsPanel; // Ayarlar menüsü

    [Header("Font Ayarları")]
    public List<TMP_FontAsset> gameFonts; // Inspector'dan atanacak fontlar
    public int currentFontIndex = 0;

    [Header("Oyun Durumu")]
    public int money = 0; // Para birimi
    public bool isGameActive = false; // Başlangıçta false
    public bool isPaused = false;
    public bool isFirstStart = true;

    private DayNightCycle dayNightCycle;
    private Coroutine feedbackCoroutine;
    private int lastMinute = -1;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        // Para Yükle
        money = PlayerPrefs.GetInt("Money", 0);
        isFirstStart = PlayerPrefs.GetInt("FirstStart", 1) == 1;
        currentFontIndex = PlayerPrefs.GetInt("SelectedFont", 0);

        CreateUI();
        CreateMainMenu();
        
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
        
        // UIManager kontrolü
        if (FindFirstObjectByType<UIManager>() == null)
        {
            gameObject.AddComponent<UIManager>();
        }
        
        // Oyunu başlat (menü gösterilecek)
        Time.timeScale = 0f;
        isGameActive = false;
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
            
            // Eski UI objelerini temizle (sahneye kaydedilmiş olabilirler)
            CleanupOldUI(canvas.transform, "MoneyPanel");
            CleanupOldUI(canvas.transform, "MoneyText");
            CleanupOldUI(canvas.transform, "TimePanel");
            CleanupOldUI(canvas.transform, "TimeText");
            CleanupOldUI(canvas.transform, "FeedbackText");
            CleanupOldUI(canvas.transform, "PausePanel");
            CleanupOldUI(canvas.transform, "ShopPanel");
            CleanupOldUI(canvas.transform, "GameOverPanel");
            CleanupOldUI(canvas.transform, "MainHUD");
            CleanupOldUI(canvas.transform, "UpgradePanel");
            CleanupOldUI(canvas.transform, "StatsPanel");
            CleanupOldUI(canvas.transform, "WeatherPanel");
            CleanupOldUI(canvas.transform, "FishingMiniGamePanel");
        }

        // Referansları sıfırla
        moneyText = null;
        timeText = null;
        feedbackText = null;
        pausePanel = null;
        shopPanel = null;
        gameOverPanel = null;

        // 2. Money Panel - Modern glass efekti
        if (moneyText == null)
        {
            // Panel arkaplanı
            GameObject moneyPanel = new GameObject("MoneyPanel");
            moneyPanel.transform.SetParent(canvas.transform, false);
            Image panelBg = moneyPanel.AddComponent<Image>();
            panelBg.color = new Color(0.08f, 0.12f, 0.18f, 0.85f);
            
            RectTransform panelRect = moneyPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(20, -20);
            panelRect.sizeDelta = new Vector2(200, 50);
            
            // Gradient border efekti
            Outline panelOutline = moneyPanel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(0.3f, 0.8f, 0.4f, 0.5f);
            panelOutline.effectDistance = new Vector2(2, -2);
            
            // Money Text
            GameObject scoreObj = new GameObject("MoneyText");
            scoreObj.transform.SetParent(moneyPanel.transform, false);
            moneyText = scoreObj.AddComponent<TextMeshProUGUI>();
            
            moneyText.fontSize = 32;
            moneyText.color = new Color(0.5f, 1f, 0.5f);
            moneyText.alignment = TextAlignmentOptions.Center;
            moneyText.fontStyle = FontStyles.Bold;
            moneyText.outlineWidth = 0.1f;
            moneyText.outlineColor = new Color(0, 0, 0, 0.6f);
            
            ApplyFont(moneyPanel);



            RectTransform rect = moneyText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10, 5);
            rect.offsetMax = new Vector2(-10, -5);
        }

        // 3. Time Panel - Modern glass efekti
        if (timeText == null)
        {
            // Panel arkaplanı
            GameObject timePanel = new GameObject("TimePanel");
            timePanel.transform.SetParent(canvas.transform, false);
            Image timePanelBg = timePanel.AddComponent<Image>();
            timePanelBg.color = new Color(0.08f, 0.12f, 0.18f, 0.85f);
            
            RectTransform timePanelRect = timePanel.GetComponent<RectTransform>();
            timePanelRect.anchorMin = new Vector2(1, 1);
            timePanelRect.anchorMax = new Vector2(1, 1);
            timePanelRect.pivot = new Vector2(1, 1);
            timePanelRect.anchoredPosition = new Vector2(-20, -20);
            timePanelRect.sizeDelta = new Vector2(120, 50);
            
            Outline timeOutline = timePanel.AddComponent<Outline>();
            timeOutline.effectColor = new Color(0.4f, 0.6f, 1f, 0.5f);
            timeOutline.effectDistance = new Vector2(-2, -2);
            
            // Time Text
            GameObject timeObj = new GameObject("TimeText");
            timeObj.transform.SetParent(timePanel.transform, false);
            timeText = timeObj.AddComponent<TextMeshProUGUI>();

            timeText.fontSize = 28;
            timeText.color = Color.white;
            timeText.alignment = TextAlignmentOptions.Center;
            timeText.fontStyle = FontStyles.Bold;
            timeText.outlineWidth = 0.1f;
            timeText.outlineColor = new Color(0, 0, 0, 0.6f);

            ApplyFont(timePanel);

            RectTransform rect = timeText.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(5, 5);
            rect.offsetMax = new Vector2(-5, -5);
        }

        // 4. Feedback Text - Minimal, orta üstte
        if (feedbackText == null)
        {
            GameObject fbObj = new GameObject("FeedbackText");
            fbObj.transform.SetParent(canvas.transform, false);
            feedbackText = fbObj.AddComponent<TextMeshProUGUI>();
            
            feedbackText.fontSize = 24;
            feedbackText.color = new Color(1f, 0.9f, 0.4f);
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.fontStyle = FontStyles.Bold;
            feedbackText.outlineWidth = 0.1f;
            feedbackText.outlineColor = new Color(0, 0, 0, 0.7f);
            
            RectTransform rect = feedbackText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -80);
            rect.sizeDelta = new Vector2(400, 40);
            
            feedbackText.gameObject.SetActive(false);
        }

        // 5. Pause Panel - Küçük, üst ortada
        if (pausePanel == null)
        {
            pausePanel = new GameObject("PausePanel");
            pausePanel.transform.SetParent(canvas.transform, false);
            
            // Küçük panel
            Image bg = pausePanel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);
            
            // Üst ortada küçük kutu
            RectTransform rect = pausePanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -150);
            rect.sizeDelta = new Vector2(250, 80);

            // "PAUSED" Yazısı
            GameObject textObj = new GameObject("PauseText");
            textObj.transform.SetParent(pausePanel.transform, false);
            TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
            txt.text = "DURAKLATILDI";
            txt.fontSize = 28;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;
            
            ApplyFont(pausePanel);
            pausePanel.SetActive(false);
        }

        // 6. Game Over Panel Oluştur
        if (gameOverPanel == null)
        {
            gameOverPanel = new GameObject("GameOverPanel");
            gameOverPanel.transform.SetParent(canvas.transform, false);
            
            // Orta kısımda kompakt panel
            Image bg = gameOverPanel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.05f, 0.1f, 0.95f);
            
            // Ortada küçük panel
            RectTransform rect = gameOverPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(400, 280);
            
            // Kenar efekti
            Outline goOutline = gameOverPanel.AddComponent<Outline>();
            goOutline.effectColor = new Color(0.8f, 0.2f, 0.2f, 0.6f);
            goOutline.effectDistance = new Vector2(4, -4);

            // "GAME OVER" Başlığı
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(gameOverPanel.transform, false);
            TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = "OYUN BITTI";
            title.fontSize = 42;
            title.color = new Color(1f, 0.3f, 0.3f);
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;
            
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -20);
            titleRect.sizeDelta = new Vector2(0, 60);

            // Final Score Yazısı
            GameObject finalScoreObj = new GameObject("FinalScoreText");
            finalScoreObj.transform.SetParent(gameOverPanel.transform, false);
            finalScoreText = finalScoreObj.AddComponent<TextMeshProUGUI>();
            finalScoreText.fontSize = 28;
            finalScoreText.color = Color.white;
            finalScoreText.alignment = TextAlignmentOptions.Center;
            
            RectTransform fsRect = finalScoreText.rectTransform;
            fsRect.anchorMin = new Vector2(0, 0.5f);
            fsRect.anchorMax = new Vector2(1, 0.5f);
            fsRect.pivot = new Vector2(0.5f, 0.5f);
            fsRect.anchoredPosition = new Vector2(0, 20);
            fsRect.sizeDelta = new Vector2(0, 60);

            // Restart Button
            GameObject btnObj = new GameObject("RestartButton");
            btnObj.transform.SetParent(gameOverPanel.transform, false);
            Button btn = btnObj.AddComponent<Button>();
            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.5f, 0.2f);
            
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0);
            btnRect.anchorMax = new Vector2(0.5f, 0);
            btnRect.pivot = new Vector2(0.5f, 0);
            btnRect.anchoredPosition = new Vector2(0, 25);
            btnRect.sizeDelta = new Vector2(180, 50);
            
            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "YENIDEN";
            btnText.fontSize = 22;
            btnText.color = Color.white;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.fontStyle = FontStyles.Bold;
            
            RectTransform btnTextRect = btnText.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            btn.onClick.AddListener(RestartGame);

            ApplyFont(gameOverPanel);
            gameOverPanel.SetActive(false);
        }
    }
    
    void CreateMainMenu()
    {
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null) return;
        
        mainMenuPanel = new GameObject("MainMenuPanel");
        mainMenuPanel.transform.SetParent(canvasObj.transform, false);
        
        // Tam ekran arkaplan
        Image bg = mainMenuPanel.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.05f, 0.1f, 0.95f);
        
        RectTransform rect = mainMenuPanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        
        // Ana içerik container
        GameObject content = new GameObject("Content");
        content.transform.SetParent(mainMenuPanel.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(500, 450);
        
        // Oyun Başlığı
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(content.transform, false);
        TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "BALIKCI";
        title.fontSize = 72;
        title.color = new Color(0.3f, 0.8f, 1f);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        title.outlineWidth = 0.1f;
        title.outlineColor = new Color(0, 0.2f, 0.4f);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, 0);
        titleRect.sizeDelta = new Vector2(0, 100);
        
        // Alt başlık
        GameObject subtitleObj = new GameObject("Subtitle");
        subtitleObj.transform.SetParent(content.transform, false);
        TextMeshProUGUI subtitle = subtitleObj.AddComponent<TextMeshProUGUI>();
        subtitle.text = "2D Balik Tutma Oyunu";
        subtitle.fontSize = 24;
        subtitle.color = new Color(0.6f, 0.7f, 0.8f);
        subtitle.alignment = TextAlignmentOptions.Center;
        RectTransform subtitleRect = subtitle.rectTransform;
        subtitleRect.anchorMin = new Vector2(0, 1);
        subtitleRect.anchorMax = new Vector2(1, 1);
        subtitleRect.pivot = new Vector2(0.5f, 1);
        subtitleRect.anchoredPosition = new Vector2(0, -100);
        subtitleRect.sizeDelta = new Vector2(0, 40);
        
        // Para bilgisi
        GameObject moneyInfoObj = new GameObject("MoneyInfo");
        moneyInfoObj.transform.SetParent(content.transform, false);
        TextMeshProUGUI moneyInfo = moneyInfoObj.AddComponent<TextMeshProUGUI>();
        moneyInfo.text = $"Toplam Para: ${money}";
        moneyInfo.fontSize = 22;
        moneyInfo.color = new Color(0.5f, 1f, 0.5f);
        moneyInfo.alignment = TextAlignmentOptions.Center;
        RectTransform moneyInfoRect = moneyInfo.rectTransform;
        moneyInfoRect.anchorMin = new Vector2(0, 1);
        moneyInfoRect.anchorMax = new Vector2(1, 1);
        moneyInfoRect.pivot = new Vector2(0.5f, 1);
        moneyInfoRect.anchoredPosition = new Vector2(0, -150);
        moneyInfoRect.sizeDelta = new Vector2(0, 35);
        
        // Devam Et Butonu (Eğer kayıt varsa)
        if (!isFirstStart)
        {
            CreateMenuButton(content.transform, "DEVAM ET", new Vector2(0, -220), new Color(0.15f, 0.4f, 0.2f), () => StartGame(false));
        }
        
        // Yeni Oyun Butonu
        float newGameY = isFirstStart ? -220 : -290;
        CreateMenuButton(content.transform, "YENI OYUN", new Vector2(0, newGameY), new Color(0.2f, 0.35f, 0.5f), () => ShowNewGameConfirm());
        
        // Ayarlar Butonu
        float settingsY = newGameY - 70;
        CreateMenuButton(content.transform, "AYARLAR", new Vector2(0, settingsY), new Color(0.3f, 0.3f, 0.35f), () => CreateSettingsUI());

        // Kontroller bilgisi
        GameObject controlsObj = new GameObject("Controls");
        controlsObj.transform.SetParent(content.transform, false);
        TextMeshProUGUI controls = controlsObj.AddComponent<TextMeshProUGUI>();
        controls.text = "<size=14><color=#888>A/D: Hareket | SPACE: Balik Tut | B: Dukkan | ESC: Duraklat</color></size>";
        controls.fontSize = 14;
        controls.alignment = TextAlignmentOptions.Center;
        RectTransform controlsRect = controls.rectTransform;
        controlsRect.anchorMin = new Vector2(0, 0);
        controlsRect.anchorMax = new Vector2(1, 0);
        controlsRect.pivot = new Vector2(0.5f, 0);
        controlsRect.anchoredPosition = new Vector2(0, 20);
        controlsRect.sizeDelta = new Vector2(0, 40);
        
        ApplyFont(mainMenuPanel);
        mainMenuPanel.SetActive(true);
    }
    
    void CreateMenuButton(Transform parent, string text, Vector2 position, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject("Button_" + text);
        btnObj.transform.SetParent(parent, false);
        
        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = color;
        
        Button btn = btnObj.AddComponent<Button>();
        
        // Hover efekti
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(color.r + 0.1f, color.g + 0.1f, color.b + 0.1f);
        colors.pressedColor = new Color(color.r - 0.1f, color.g - 0.1f, color.b - 0.1f);
        btn.colors = colors;
        
        Outline btnOutline = btnObj.AddComponent<Outline>();
        btnOutline.effectColor = new Color(1f, 1f, 1f, 0.2f);
        btnOutline.effectDistance = new Vector2(2, -2);
        
        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 1);
        btnRect.anchorMax = new Vector2(0.5f, 1);
        btnRect.pivot = new Vector2(0.5f, 1);
        btnRect.anchoredPosition = position;
        btnRect.sizeDelta = new Vector2(280, 55);
        
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = 24;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontStyle = FontStyles.Bold;
        RectTransform txtRect = txt.rectTransform;
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
        
        btn.onClick.AddListener(action);
    }
    
    void ShowNewGameConfirm()
    {
        if (isFirstStart || money == 0)
        {
            StartGame(true);
            return;
        }
        
        // Onay penceresi göster
        GameObject confirmPanel = new GameObject("ConfirmPanel");
        confirmPanel.transform.SetParent(mainMenuPanel.transform, false);
        
        Image bg = confirmPanel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.05f, 0.1f, 0.98f);
        
        RectTransform rect = confirmPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(400, 200);
        
        Outline outline = confirmPanel.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.5f, 0.2f, 0.6f);
        outline.effectDistance = new Vector2(3, -3);
        
        // Uyarı metni
        GameObject warnObj = new GameObject("Warning");
        warnObj.transform.SetParent(confirmPanel.transform, false);
        TextMeshProUGUI warn = warnObj.AddComponent<TextMeshProUGUI>();
        warn.text = $"DIKKAT!\n\nTum ilerlemeniz silinecek!\n(${money} para kaybedilecek)";
        warn.fontSize = 20;
        warn.color = new Color(1f, 0.8f, 0.4f);
        warn.alignment = TextAlignmentOptions.Center;
        RectTransform warnRect = warn.rectTransform;
        warnRect.anchorMin = new Vector2(0, 0.4f);
        warnRect.anchorMax = new Vector2(1, 1);
        warnRect.offsetMin = new Vector2(20, 0);
        warnRect.offsetMax = new Vector2(-20, -20);
        
        // Evet butonu
        GameObject yesBtn = new GameObject("YesBtn");
        yesBtn.transform.SetParent(confirmPanel.transform, false);
        Image yesImg = yesBtn.AddComponent<Image>();
        yesImg.color = new Color(0.5f, 0.2f, 0.2f);
        Button yesBtnComp = yesBtn.AddComponent<Button>();
        RectTransform yesRect = yesBtn.GetComponent<RectTransform>();
        yesRect.anchorMin = new Vector2(0, 0);
        yesRect.anchorMax = new Vector2(0.5f, 0);
        yesRect.pivot = new Vector2(0, 0);
        yesRect.anchoredPosition = new Vector2(20, 20);
        yesRect.sizeDelta = new Vector2(-30, 50);
        
        GameObject yesTxt = new GameObject("Text");
        yesTxt.transform.SetParent(yesBtn.transform, false);
        TextMeshProUGUI yesTxtComp = yesTxt.AddComponent<TextMeshProUGUI>();
        yesTxtComp.text = "EVET, SIL";
        yesTxtComp.fontSize = 18;
        yesTxtComp.color = Color.white;
        yesTxtComp.alignment = TextAlignmentOptions.Center;
        yesTxtComp.fontStyle = FontStyles.Bold;
        RectTransform yesTxtRect = yesTxtComp.rectTransform;
        yesTxtRect.anchorMin = Vector2.zero;
        yesTxtRect.anchorMax = Vector2.one;
        yesTxtRect.offsetMin = Vector2.zero;
        yesTxtRect.offsetMax = Vector2.zero;
        
        yesBtnComp.onClick.AddListener(() => {
            Destroy(confirmPanel);
            StartGame(true);
        });
        
        // Hayır butonu
        GameObject noBtn = new GameObject("NoBtn");
        noBtn.transform.SetParent(confirmPanel.transform, false);
        Image noImg = noBtn.AddComponent<Image>();
        noImg.color = new Color(0.2f, 0.4f, 0.2f);
        Button noBtnComp = noBtn.AddComponent<Button>();
        RectTransform noRect = noBtn.GetComponent<RectTransform>();
        noRect.anchorMin = new Vector2(0.5f, 0);
        noRect.anchorMax = new Vector2(1, 0);
        noRect.pivot = new Vector2(1, 0);
        noRect.anchoredPosition = new Vector2(-20, 20);
        noRect.sizeDelta = new Vector2(-30, 50);
        
        GameObject noTxt = new GameObject("Text");
        noTxt.transform.SetParent(noBtn.transform, false);
        TextMeshProUGUI noTxtComp = noTxt.AddComponent<TextMeshProUGUI>();
        noTxtComp.text = "VAZGEC";
        noTxtComp.fontSize = 18;
        noTxtComp.color = Color.white;
        noTxtComp.alignment = TextAlignmentOptions.Center;
        noTxtComp.fontStyle = FontStyles.Bold;
        RectTransform noTxtRect = noTxtComp.rectTransform;
        noTxtRect.anchorMin = Vector2.zero;
        noTxtRect.anchorMax = Vector2.one;
        noTxtRect.offsetMin = Vector2.zero;
        noTxtRect.offsetMax = Vector2.zero;
        
        noBtnComp.onClick.AddListener(() => Destroy(confirmPanel));
    }
    
    public void StartGame(bool newGame)
    {
        if (newGame)
        {
            // Tüm verileri sıfırla
            PlayerPrefs.DeleteAll();
            money = 0;
            PlayerPrefs.SetInt("FirstStart", 0);
            PlayerPrefs.Save();
            
            // Upgrade seviyelerini sıfırla
            if (UpgradeManager.instance != null)
            {
                // UpgradeManager'ı yeniden yükle
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }
        }
        
        PlayerPrefs.SetInt("FirstStart", 0);
        PlayerPrefs.Save();
        
        // Menüyü kapat
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        
        // Oyunu başlat
        isGameActive = true;
        isFirstStart = false;
        Time.timeScale = 1f;
        
        UpdateMoneyUI();
    }

    void Start()
    {
        dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        UpdateMoneyUI();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        
        // Ana menüyü göster
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
        
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
        
        // Arkaplan - Ortada geniş panel
        Image bg = shopPanel.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.06f, 0.1f, 0.97f);
        
        RectTransform rect = shopPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(700, 500);
        
        // Modern kenar efekti
        Outline shopOutline = shopPanel.AddComponent<Outline>();
        shopOutline.effectColor = new Color(0.2f, 0.5f, 0.8f, 0.6f);
        shopOutline.effectDistance = new Vector2(3, -3);
        
        // Başlık Bar
        GameObject titleBar = new GameObject("TitleBar");
        titleBar.transform.SetParent(shopPanel.transform, false);
        Image titleBg = titleBar.AddComponent<Image>();
        titleBg.color = new Color(0.08f, 0.12f, 0.2f, 1f);
        RectTransform titleBarRect = titleBar.GetComponent<RectTransform>();
        titleBarRect.anchorMin = new Vector2(0, 1);
        titleBarRect.anchorMax = new Vector2(1, 1);
        titleBarRect.pivot = new Vector2(0.5f, 1);
        titleBarRect.anchoredPosition = Vector2.zero;
        titleBarRect.sizeDelta = new Vector2(0, 50);
        
        // Başlık Text
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(titleBar.transform, false);
        TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "MARKET";
        title.fontSize = 26;
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        title.color = Color.white;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(50, 0);
        titleRect.offsetMax = new Vector2(-50, 0);
        
        // Kapat Butonu
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(titleBar.transform, false);
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        Image closeImg = closeBtnObj.AddComponent<Image>();
        closeImg.color = new Color(0.7f, 0.2f, 0.2f, 0.9f);
        RectTransform closeBtnRect = closeBtnObj.GetComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(1, 0.5f);
        closeBtnRect.anchorMax = new Vector2(1, 0.5f);
        closeBtnRect.pivot = new Vector2(1, 0.5f);
        closeBtnRect.anchoredPosition = new Vector2(-10, 0);
        closeBtnRect.sizeDelta = new Vector2(30, 30);
        closeBtn.onClick.AddListener(ToggleShop);
        
        GameObject closeTxtObj = new GameObject("Text");
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        TextMeshProUGUI closeTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
        closeTxt.text = "✕";
        closeTxt.fontSize = 18;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.color = Color.white;
        RectTransform closeTxtRect = closeTxt.rectTransform;
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;
        closeTxtRect.offsetMin = Vector2.zero;
        closeTxtRect.offsetMax = Vector2.zero;
        
        // Tab Butonları
        CreateShopTabs(shopPanel.transform);
        
        // İçerik Alanı - ScrollView
        GameObject contentArea = new GameObject("ContentArea");
        contentArea.transform.SetParent(shopPanel.transform, false);
        Image contentBg = contentArea.AddComponent<Image>();
        contentBg.color = new Color(0.03f, 0.05f, 0.08f, 0.8f);
        RectTransform contentRect = contentArea.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.offsetMin = new Vector2(10, 10);
        contentRect.offsetMax = new Vector2(-10, -95);
        
        // ScrollRect
        ScrollRect scroll = contentArea.AddComponent<ScrollRect>();
        scroll.vertical = true;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25f;
        
        // Viewport
        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(contentArea.transform, false);
        Image vpMask = viewport.AddComponent<Image>();
        vpMask.color = Color.white;
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        RectTransform vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;
        scroll.viewport = vpRect;
        
        // Container
        GameObject container = new GameObject("Container");
        container.transform.SetParent(viewport.transform, false);
        VerticalLayoutGroup vLayout = container.AddComponent<VerticalLayoutGroup>();
        vLayout.spacing = 6;
        vLayout.padding = new RectOffset(8, 8, 8, 8);
        vLayout.childAlignment = TextAnchor.UpperCenter;
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = false;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;
        
        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 1);
        containerRect.anchorMax = new Vector2(1, 1);
        containerRect.pivot = new Vector2(0.5f, 1);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = new Vector2(0, 0);
        scroll.content = containerRect;
        
        ContentSizeFitter fitter = container.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        
        ApplyFont(shopPanel);
        shopPanel.SetActive(false);
    }
    
    private int currentShopTab = 0; // 0: Misina, 1: Tekne, 2: Balık Pazarı
    
    void CreateShopTabs(Transform parent)
    {
        GameObject tabBar = new GameObject("TabBar");
        tabBar.transform.SetParent(parent, false);
        HorizontalLayoutGroup hLayout = tabBar.AddComponent<HorizontalLayoutGroup>();
        hLayout.spacing = 5;
        hLayout.padding = new RectOffset(10, 10, 0, 0);
        hLayout.childAlignment = TextAnchor.MiddleCenter;
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = true;
        hLayout.childForceExpandWidth = true;
        hLayout.childForceExpandHeight = true;
        
        RectTransform tabBarRect = tabBar.GetComponent<RectTransform>();
        tabBarRect.anchorMin = new Vector2(0, 1);
        tabBarRect.anchorMax = new Vector2(1, 1);
        tabBarRect.pivot = new Vector2(0.5f, 1);
        tabBarRect.anchoredPosition = new Vector2(0, -50);
        tabBarRect.sizeDelta = new Vector2(0, 35);
        
        string[] tabNames = { "Misina", "Tekne", "Balik Pazari" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            int tabIndex = i;
            GameObject tabBtn = new GameObject("Tab_" + i);
            tabBtn.transform.SetParent(tabBar.transform, false);
            Button btn = tabBtn.AddComponent<Button>();
            Image btnImg = tabBtn.AddComponent<Image>();
            btnImg.color = (i == currentShopTab) ? new Color(0.15f, 0.3f, 0.5f) : new Color(0.1f, 0.12f, 0.18f);
            
            GameObject tabTxt = new GameObject("Text");
            tabTxt.transform.SetParent(tabBtn.transform, false);
            TextMeshProUGUI txt = tabTxt.AddComponent<TextMeshProUGUI>();
            txt.text = tabNames[i];
            txt.fontSize = 14;
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontStyle = FontStyles.Bold;
            txt.color = (i == currentShopTab) ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            RectTransform txtRect = txt.rectTransform;
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            
            btn.onClick.AddListener(() => {
                currentShopTab = tabIndex;
                UpdateShopUI();
            });
        }
    }
    
    void UpdateShopUI()
    {
        if (shopPanel == null) return;
        
        // Tab renklerini güncelle
        Transform tabBar = shopPanel.transform.Find("TabBar");
        if (tabBar != null)
        {
            for (int i = 0; i < tabBar.childCount; i++)
            {
                Transform tab = tabBar.GetChild(i);
                Image tabImg = tab.GetComponent<Image>();
                TextMeshProUGUI tabTxt = tab.GetComponentInChildren<TextMeshProUGUI>();
                if (tabImg != null)
                    tabImg.color = (i == currentShopTab) ? new Color(0.15f, 0.3f, 0.5f) : new Color(0.1f, 0.12f, 0.18f);
                if (tabTxt != null)
                    tabTxt.color = (i == currentShopTab) ? Color.white : new Color(0.6f, 0.6f, 0.6f);
            }
        }
        
        // Container bul
        Transform contentArea = shopPanel.transform.Find("ContentArea");
        if (contentArea == null) return;
        Transform viewport = contentArea.Find("Viewport");
        if (viewport == null) return;
        Transform container = viewport.Find("Container");
        if (container == null) return;
        
        // Eski içeriği temizle
        foreach (Transform child in container) Destroy(child.gameObject);
        
        if (currentShopTab == 0 || currentShopTab == 1)
        {
            // Misina veya Tekne Geliştirmeleri
            CreateUpgradeItems(container, currentShopTab == 0 ? UpgradeCategory.Line : UpgradeCategory.Boat);
            
            // Genel geliştirmeler de tekne sekmesinde göster
            if (currentShopTab == 1)
            {
                CreateCategoryHeader(container, "GENEL");
                CreateUpgradeItems(container, UpgradeCategory.General);
            }
        }
        else if (currentShopTab == 2)
        {
            // Balık Pazarı
            CreateFishMarket(container);
        }

        // Yeni oluşturulan içeriğe font uygula
        ApplyFont(container.gameObject);
    }
    
    void CreateCategoryHeader(Transform parent, string text)
    {
        GameObject header = new GameObject("Header");
        header.transform.SetParent(parent, false);
        Image headerBg = header.AddComponent<Image>();
        headerBg.color = new Color(0.1f, 0.15f, 0.25f, 0.8f);
        LayoutElement headerLayout = header.AddComponent<LayoutElement>();
        headerLayout.minHeight = 28;
        headerLayout.preferredHeight = 28;
        
        GameObject headerTxt = new GameObject("Text");
        headerTxt.transform.SetParent(header.transform, false);
        TextMeshProUGUI txt = headerTxt.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = 14;
        txt.fontStyle = FontStyles.Bold;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.7f, 0.8f, 0.9f);
        RectTransform txtRect = txt.rectTransform;
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
    }
    
    void CreateUpgradeItems(Transform container, UpgradeCategory category)
    {
        if (UpgradeManager.instance == null) return;
        
        var upgrades = UpgradeManager.instance.GetUpgradesByCategory(category);
        
        foreach (var upg in upgrades)
        {
            GameObject item = new GameObject("Item_" + upg.name);
            item.transform.SetParent(container, false);
            
            // Yatay layout
            HorizontalLayoutGroup hLayout = item.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 8;
            hLayout.padding = new RectOffset(12, 12, 6, 6);
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = true;
            
            Image itemBg = item.AddComponent<Image>();
            itemBg.color = new Color(0.08f, 0.1f, 0.15f, 0.95f);
            
            LayoutElement itemLayout = item.AddComponent<LayoutElement>();
            itemLayout.minHeight = 45;
            itemLayout.preferredHeight = 45;
            
            // Icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(item.transform, false);
            TextMeshProUGUI iconTxt = iconObj.AddComponent<TextMeshProUGUI>();
            iconTxt.text = upg.icon;
            iconTxt.fontSize = 22;
            iconTxt.alignment = TextAlignmentOptions.Center;
            LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
            iconLayout.minWidth = 30;
            iconLayout.preferredWidth = 30;
            
            // İsim ve Level
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(item.transform, false);
            TextMeshProUGUI nameTxt = nameObj.AddComponent<TextMeshProUGUI>();
            int level = UpgradeManager.instance.GetLevel(upg.type);
            nameTxt.text = $"<color=#FFD700>{upg.turkishName}</color>\n<size=10><color=#AAA>{upg.description}</color></size>";
            nameTxt.fontSize = 13;
            nameTxt.alignment = TextAlignmentOptions.Left;
            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;
            nameLayout.minWidth = 150;
            
            // Level göstergesi
            GameObject levelObj = new GameObject("Level");
            levelObj.transform.SetParent(item.transform, false);
            TextMeshProUGUI levelTxt = levelObj.AddComponent<TextMeshProUGUI>();
            levelTxt.text = $"Lv.{level}/{upg.maxLevel}";
            levelTxt.fontSize = 12;
            levelTxt.alignment = TextAlignmentOptions.Center;
            levelTxt.color = new Color(0.5f, 0.8f, 1f);
            LayoutElement levelLayout = levelObj.AddComponent<LayoutElement>();
            levelLayout.minWidth = 50;
            levelLayout.preferredWidth = 50;
            
            // Satın Al Butonu
            int cost = UpgradeManager.instance.GetCost(upg.type);
            
            GameObject buyBtn = new GameObject("BuyBtn");
            buyBtn.transform.SetParent(item.transform, false);
            Image buyImg = buyBtn.AddComponent<Image>();
            Button btn = buyBtn.AddComponent<Button>();
            LayoutElement buyLayout = buyBtn.AddComponent<LayoutElement>();
            buyLayout.minWidth = 70;
            buyLayout.preferredWidth = 70;
            
            GameObject buyTxtObj = new GameObject("Text");
            buyTxtObj.transform.SetParent(buyBtn.transform, false);
            TextMeshProUGUI buyTxt = buyTxtObj.AddComponent<TextMeshProUGUI>();
            buyTxt.fontSize = 12;
            buyTxt.fontStyle = FontStyles.Bold;
            buyTxt.alignment = TextAlignmentOptions.Center;
            RectTransform buyTxtRect = buyTxt.rectTransform;
            buyTxtRect.anchorMin = Vector2.zero;
            buyTxtRect.anchorMax = Vector2.one;
            buyTxtRect.offsetMin = Vector2.zero;
            buyTxtRect.offsetMax = Vector2.zero;
            
            if (cost < 0)
            {
                buyTxt.text = "MAX";
                buyTxt.color = new Color(0.6f, 0.6f, 0.6f);
                buyImg.color = new Color(0.2f, 0.2f, 0.25f);
                btn.interactable = false;
            }
            else if (money >= cost)
            {
                buyTxt.text = "$" + cost;
                buyTxt.color = Color.white;
                buyImg.color = new Color(0.15f, 0.5f, 0.2f);
                var upgCopy = upg;
                btn.onClick.AddListener(() => {
                    if (UpgradeManager.instance.TryUpgrade(upgCopy.type))
                    {
                        UpdateShopUI();
                        if (SoundManager.instance != null) SoundManager.instance.PlaySFX(SoundManager.instance.catchSound);
                    }
                });
            }
            else
            {
                buyTxt.text = "$" + cost;
                buyTxt.color = new Color(0.8f, 0.4f, 0.4f);
                buyImg.color = new Color(0.4f, 0.15f, 0.15f);
                btn.interactable = false;
            }
        }
    }
    
    void CreateFishMarket(Transform container)
    {
        // Başlık
        CreateCategoryHeader(container, "BALIK FIYATLARI");
        
        // Hava durumu bonusu
        float weatherBonus = 1f;
        string weatherInfo = "";
        
        // Bonus bilgisi
        if (weatherInfo != "")
        {
            GameObject bonusObj = new GameObject("BonusInfo");
            bonusObj.transform.SetParent(container, false);
            Image bonusBg = bonusObj.AddComponent<Image>();
            bonusBg.color = new Color(0.1f, 0.2f, 0.15f, 0.9f);
            LayoutElement bonusLayout = bonusObj.AddComponent<LayoutElement>();
            bonusLayout.minHeight = 30;
            bonusLayout.preferredHeight = 30;
            
            GameObject bonusTxt = new GameObject("Text");
            bonusTxt.transform.SetParent(bonusObj.transform, false);
            TextMeshProUGUI txt = bonusTxt.AddComponent<TextMeshProUGUI>();
            txt.text = $"<color=#90EE90>Hava Durumu Bonusu Aktif!</color>{weatherInfo}";
            txt.fontSize = 12;
            txt.alignment = TextAlignmentOptions.Center;
            RectTransform txtRect = txt.rectTransform;
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
        }
        
        // Balık listesini al ve sırala
        List<FishType> fishList = new List<FishType>();
        FishSpawner spawner = FindFirstObjectByType<FishSpawner>();
        
        if (spawner != null && spawner.fishTypes != null && spawner.fishTypes.Count > 0)
        {
            fishList.AddRange(spawner.fishTypes);
        }
        else
        {
            // Fallback veriler (Eğer spawner bulunamazsa)
            fishList.Add(new FishType { name = "Sardalya", score = 10, spawnWeight = 35 });
            fishList.Add(new FishType { name = "Çipura", score = 25, spawnWeight = 25 });
            fishList.Add(new FishType { name = "Levrek", score = 50, spawnWeight = 20 });
            fishList.Add(new FishType { name = "Palamut", score = 100, spawnWeight = 12 });
            fishList.Add(new FishType { name = "Orkinos", score = 250, spawnWeight = 6 });
            fishList.Add(new FishType { name = "Köpekbalığı", score = 500, spawnWeight = 2 });
        }

        // Fiyata göre sırala (Küçükten büyüğe)
        fishList.Sort((a, b) => a.score.CompareTo(b.score));

        // Toplam ağırlığı hesapla (Nadirlik için)
        float totalWeight = 0;
        foreach(var f in fishList) totalWeight += f.spawnWeight;
        if (totalWeight <= 0) totalWeight = 1;

        for (int i = 0; i < fishList.Count; i++)
        {
            FishType fish = fishList[i];
            GameObject item = new GameObject("Fish_" + i);
            item.transform.SetParent(container, false);
            
            HorizontalLayoutGroup hLayout = item.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 10;
            hLayout.padding = new RectOffset(15, 15, 8, 8);
            hLayout.childAlignment = TextAnchor.MiddleLeft;
            hLayout.childControlWidth = false;
            hLayout.childControlHeight = true;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = true;
            
            Image itemBg = item.AddComponent<Image>();
            // Nadirlere göre renk
            float rarity = (float)fish.spawnWeight / totalWeight;
            
            if (rarity <= 0.05f) itemBg.color = new Color(0.2f, 0.1f, 0.25f, 0.95f); // Efsanevi
            else if (rarity <= 0.1f) itemBg.color = new Color(0.15f, 0.15f, 0.25f, 0.95f); // Nadir
            else if (rarity <= 0.2f) itemBg.color = new Color(0.1f, 0.15f, 0.2f, 0.95f); // Uncommon
            else itemBg.color = new Color(0.08f, 0.1f, 0.15f, 0.95f); // Common
            
            LayoutElement itemLayout = item.AddComponent<LayoutElement>();
            itemLayout.minHeight = 40;
            itemLayout.preferredHeight = 40;
            
            // Icon
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(item.transform, false);
            LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
            iconLayout.minWidth = 35;
            iconLayout.preferredWidth = 35;
            iconLayout.minHeight = 35;
            iconLayout.preferredHeight = 35;

            if (fish.sprite != null)
            {
                Image iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = fish.sprite;
                iconImg.preserveAspect = true;
            }
            else
            {
                TextMeshProUGUI iconTxt = iconObj.AddComponent<TextMeshProUGUI>();
                iconTxt.text = "><>";
                iconTxt.fontSize = 24;
                iconTxt.alignment = TextAlignmentOptions.Center;
            }
            
            // İsim
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(item.transform, false);
            TextMeshProUGUI nameTxt = nameObj.AddComponent<TextMeshProUGUI>();
            nameTxt.text = fish.name;
            nameTxt.fontSize = 14;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.alignment = TextAlignmentOptions.Left;
            nameTxt.color = Color.white;
            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.minWidth = 120;
            nameLayout.preferredWidth = 120;
            
            // Nadirlik
            GameObject rarityObj = new GameObject("Rarity");
            rarityObj.transform.SetParent(item.transform, false);
            TextMeshProUGUI rarityTxt = rarityObj.AddComponent<TextMeshProUGUI>();
            string rarityStr = rarity <= 0.05f ? "<color=#FF66FF>Efsanevi</color>" :
                              rarity <= 0.1f ? "<color=#9966FF>Nadir</color>" :
                              rarity <= 0.2f ? "<color=#66CCFF>Uncommon</color>" :
                              "<color=#AAAAAA>Common</color>";
            rarityTxt.text = rarityStr;
            rarityTxt.fontSize = 11;
            rarityTxt.alignment = TextAlignmentOptions.Center;
            LayoutElement rarityLayout = rarityObj.AddComponent<LayoutElement>();
            rarityLayout.minWidth = 80;
            rarityLayout.preferredWidth = 80;
            
            // Fiyat
            GameObject priceObj = new GameObject("Price");
            priceObj.transform.SetParent(item.transform, false);
            TextMeshProUGUI priceTxt = priceObj.AddComponent<TextMeshProUGUI>();
            int finalPrice = Mathf.RoundToInt(fish.score * weatherBonus);
            priceTxt.text = $"<color=#90EE90>${finalPrice}</color>";
            priceTxt.fontSize = 14;
            priceTxt.fontStyle = FontStyles.Bold;
            priceTxt.alignment = TextAlignmentOptions.Right;
            LayoutElement priceLayout = priceObj.AddComponent<LayoutElement>();
            priceLayout.flexibleWidth = 1;
        }
        
        // Alt bilgi
        GameObject infoObj = new GameObject("Info");
        infoObj.transform.SetParent(container, false);
        LayoutElement infoLayout = infoObj.AddComponent<LayoutElement>();
        infoLayout.minHeight = 50;
        infoLayout.preferredHeight = 50;
        
        TextMeshProUGUI infoTxt = infoObj.AddComponent<TextMeshProUGUI>();
        infoTxt.text = "<size=11><color=#888>Firtinali havalarda nadir baliklar daha sik cikar!\nSansli Yem gelistirmesi nadir balik sansini artirir.</color></size>";
        infoTxt.fontSize = 11;
        infoTxt.alignment = TextAlignmentOptions.Center;
    }

    void CleanupOldUI(Transform parent, string objName)
    {
        Transform old = parent.Find(objName);
        if (old != null) DestroyImmediate(old.gameObject);
    }

    // --- Font Sistemi ---

    public void ApplyFontToAll()
    {
        if (gameFonts == null || gameFonts.Count == 0) return;
        if (currentFontIndex < 0 || currentFontIndex >= gameFonts.Count) currentFontIndex = 0;

        TMP_FontAsset targetFont = gameFonts[currentFontIndex];
        if (targetFont == null) return;

        // Sahnedeki tüm TextMeshProUGUI bileşenlerini bul
        TextMeshProUGUI[] allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None);
        foreach (var txt in allTexts)
        {
            txt.font = targetFont;
            txt.SetAllDirty(); // Mesh'i yenilemeye zorla
        }
    }

    public void ApplyFont(GameObject root)
    {
        if (gameFonts == null || gameFonts.Count == 0) return;
        if (currentFontIndex < 0 || currentFontIndex >= gameFonts.Count) currentFontIndex = 0;

        TMP_FontAsset targetFont = gameFonts[currentFontIndex];
        if (targetFont == null) return;

        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in texts)
        {
            txt.font = targetFont;
            txt.SetAllDirty(); // Mesh'i yenilemeye zorla
        }
    }

    public void ChangeFont(int direction)
    {
        if (gameFonts == null || gameFonts.Count == 0) return;

        currentFontIndex += direction;
        if (currentFontIndex >= gameFonts.Count) currentFontIndex = 0;
        if (currentFontIndex < 0) currentFontIndex = gameFonts.Count - 1;

        PlayerPrefs.SetInt("SelectedFont", currentFontIndex);
        PlayerPrefs.Save();

        ApplyFontToAll();
        UpdateSettingsUI(); // Eğer ayarlar açıksa güncelle
    }

    void CreateSettingsUI()
    {
        if (settingsPanel != null) 
        {
            settingsPanel.SetActive(true);
            UpdateSettingsUI();
            return;
        }

        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null) return;

        settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(canvasObj.transform, false);

        // Arkaplan
        Image bg = settingsPanel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.08f, 0.12f, 0.98f);
        RectTransform rect = settingsPanel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // İçerik Kutusu
        GameObject content = new GameObject("Content");
        content.transform.SetParent(settingsPanel.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(400, 300);

        Outline outline = content.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.6f, 0.9f, 0.5f);
        outline.effectDistance = new Vector2(2, -2);

        // Başlık
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(content.transform, false);
        TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = "AYARLAR";
        title.fontSize = 32;
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        title.color = Color.white;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -30);
        titleRect.sizeDelta = new Vector2(0, 50);

        // Font Seçimi Başlık
        GameObject fontTitleObj = new GameObject("FontTitle");
        fontTitleObj.transform.SetParent(content.transform, false);
        TextMeshProUGUI fontTitle = fontTitleObj.AddComponent<TextMeshProUGUI>();
        fontTitle.text = "Yazı Tipi (Font)";
        fontTitle.fontSize = 18;
        fontTitle.alignment = TextAlignmentOptions.Center;
        fontTitle.color = new Color(0.7f, 0.8f, 0.9f);
        RectTransform fontTitleRect = fontTitle.rectTransform;
        fontTitleRect.anchorMin = new Vector2(0.5f, 0.5f);
        fontTitleRect.anchoredPosition = new Vector2(0, 40);
        fontTitleRect.sizeDelta = new Vector2(300, 30);

        // Font Seçici Container
        GameObject fontSelector = new GameObject("FontSelector");
        fontSelector.transform.SetParent(content.transform, false);
        RectTransform fsRect = fontSelector.AddComponent<RectTransform>();
        fsRect.anchorMin = new Vector2(0.5f, 0.5f);
        fsRect.anchoredPosition = new Vector2(0, 0);
        fsRect.sizeDelta = new Vector2(300, 50);

        // Sol Ok
        CreateArrowButton(fontSelector.transform, "<", new Vector2(-120, 0), () => ChangeFont(-1));

        // Sağ Ok
        CreateArrowButton(fontSelector.transform, ">", new Vector2(120, 0), () => ChangeFont(1));

        // Font İsmi
        GameObject fontNameObj = new GameObject("FontName");
        fontNameObj.transform.SetParent(fontSelector.transform, false);
        TextMeshProUGUI fontName = fontNameObj.AddComponent<TextMeshProUGUI>();
        fontName.text = "Default";
        fontName.fontSize = 20;
        fontName.alignment = TextAlignmentOptions.Center;
        fontName.color = Color.yellow;
        RectTransform fontNameRect = fontName.rectTransform;
        fontNameRect.anchorMin = Vector2.zero;
        fontNameRect.anchorMax = Vector2.one;
        fontNameRect.offsetMin = new Vector2(40, 0);
        fontNameRect.offsetMax = new Vector2(-40, 0);

        // Kapat Butonu
        GameObject closeBtnObj = new GameObject("CloseButton");
        closeBtnObj.transform.SetParent(content.transform, false);
        Image closeImg = closeBtnObj.AddComponent<Image>();
        closeImg.color = new Color(0.7f, 0.2f, 0.2f);
        Button closeBtn = closeBtnObj.AddComponent<Button>();
        RectTransform closeRect = closeBtnObj.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.5f, 0);
        closeRect.anchorMax = new Vector2(0.5f, 0);
        closeRect.pivot = new Vector2(0.5f, 0);
        closeRect.anchoredPosition = new Vector2(0, 30);
        closeRect.sizeDelta = new Vector2(150, 40);

        GameObject closeTxtObj = new GameObject("Text");
        closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
        TextMeshProUGUI closeTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
        closeTxt.text = "KAPAT";
        closeTxt.fontSize = 18;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.color = Color.white;
        RectTransform closeTxtRect = closeTxt.rectTransform;
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;

        closeBtn.onClick.AddListener(() => settingsPanel.SetActive(false));

        ApplyFont(settingsPanel);
        UpdateSettingsUI();
    }

    void CreateArrowButton(Transform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject("ArrowBtn");
        btnObj.transform.SetParent(parent, false);
        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.3f, 0.4f);
        Button btn = btnObj.AddComponent<Button>();
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(40, 40);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        txt.text = text;
        txt.fontSize = 24;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;
        RectTransform txtRect = txt.rectTransform;
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;

        btn.onClick.AddListener(action);
    }

    void UpdateSettingsUI()
    {
        if (settingsPanel == null) return;
        
        Transform fontSelector = settingsPanel.transform.Find("Content/FontSelector");
        if (fontSelector != null)
        {
            Transform fontNameObj = fontSelector.Find("FontName");
            if (fontNameObj != null)
            {
                TextMeshProUGUI txt = fontNameObj.GetComponent<TextMeshProUGUI>();
                if (gameFonts != null && gameFonts.Count > 0 && currentFontIndex < gameFonts.Count)
                {
                    txt.text = gameFonts[currentFontIndex].name;
                    txt.font = gameFonts[currentFontIndex]; // Önizleme
                }
                else
                {
                    txt.text = "Font Yok";
                }
            }
        }
    }
}
