using UnityEngine;
using TMPro; // TextMeshPro için gerekli
using UnityEngine.UI; // Canvas ve UI bileşenleri için
using UnityEngine.SceneManagement; // Sahne yönetimi için
using System.Collections; // Coroutines için gerekli
using System.Collections.Generic; // List<> için gerekli

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    private const string PREF_MONEY = "Money";
    private const string PREF_FIRST_START = "FirstStart";
    private const string PREF_SELECTED_FONT = "SelectedFont";
    private const string PREF_PENDING_AUTOSTART = "PendingAutoStart";

    [System.Flags]
    private enum PauseSource
    {
        None = 0,
        MainMenu = 1 << 0,
        PauseMenu = 1 << 1,
        Shop = 1 << 2,
        UIPanel = 1 << 3,
        GameOver = 1 << 4,
    }

    private PauseSource pauseMask = PauseSource.None;
    private int uiPauseRequests = 0;

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
    private FishSpawner cachedFishSpawner;
    private Coroutine feedbackCoroutine;
    private int lastMinute = -1;

    private float nextDayNightSearchTime = 0f;
    private const float DAYNIGHT_SEARCH_INTERVAL = 1f;

    private bool autoStartGameOnLoad = false;

    private Transform cachedCanvasTransform;

    private SettingsManager Settings => SettingsManager.instance;
    private LocalizationManager Loc => LocalizationManager.instance;
    private static T FindOrCreateManager<T>(string name) where T : MonoBehaviour
    {
        // Important: Don't rely on T.instance inside another Awake(),
        // because script execution order can make it null even when the component exists.
        T existing = FindFirstObjectByType<T>();
        if (existing != null) return existing;

        GameObject go = new GameObject(name);
        return go.AddComponent<T>();
    }

    private void SubscribeToManagerEvents()
    {
        if (Settings != null)
        {
            Settings.SettingsChanged -= HandleSettingsChanged;
            Settings.SettingsChanged += HandleSettingsChanged;
        }

        if (Loc != null)
        {
            Loc.LanguageChanged -= HandleLanguageChanged;
            Loc.LanguageChanged += HandleLanguageChanged;
        }
    }

    private static void StretchToFill(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private TextMeshProUGUI CreateStretchedLabel(Transform parent, string name, string text, float fontSize, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        FontStyles fontStyle = FontStyles.Bold)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.fontStyle = fontStyle;

        StretchToFill(tmp.rectTransform);
        return tmp;
    }

    private static GameObject CreateImagePanel(string name, Transform parent, Color bgColor,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        Image img = panel.AddComponent<Image>();
        img.color = bgColor;

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return panel;
    }

    private static void AddOutline(GameObject target, Color effectColor, Vector2 effectDistance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = effectColor;
        outline.effectDistance = effectDistance;
    }

    private static TextMeshProUGUI CreateTMPTextObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj.AddComponent<TextMeshProUGUI>();
    }

    private Transform GetCanvasTransform()
    {
        if (cachedCanvasTransform != null) return cachedCanvasTransform;

        // İsimle aramak kırılgan; önce sahnedeki Canvas'ı bul.
        Canvas c = FindFirstObjectByType<Canvas>();
        if (c != null)
            cachedCanvasTransform = c.transform;
        else
            cachedCanvasTransform = null;

        return cachedCanvasTransform;
    }

    public Transform CanvasTransform => GetCanvasTransform();

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        // Para Yükle
        money = PlayerPrefs.GetInt(PREF_MONEY, 0);
        isFirstStart = PlayerPrefs.GetInt(PREF_FIRST_START, 1) == 1;
        currentFontIndex = PlayerPrefs.GetInt(PREF_SELECTED_FONT, 0);

        autoStartGameOnLoad = PlayerPrefs.GetInt(PREF_PENDING_AUTOSTART, 0) == 1;
        if (autoStartGameOnLoad)
        {
            PlayerPrefs.DeleteKey(PREF_PENDING_AUTOSTART);
            PlayerPrefs.Save();
        }

        EnsureUIFontSetup();

        // Settings/Localization managers must NOT be added to the GameManager GameObject,
        // because they call DontDestroyOnLoad(gameObject) which would also persist GameManager.
        // That breaks New Game / scene reload (UI references get destroyed but GameManager doesn't re-Start).
        FindOrCreateManager<SettingsManager>("SettingsManager");
        FindOrCreateManager<LocalizationManager>("LocalizationManager");

        CreateUI();
        CreateMainMenu();
        
        // FishingMiniGame'i kontrol et ve ekle
        if (FishingMiniGame.instance == null)
        {
            gameObject.AddComponent<FishingMiniGame>();
        }
        
        // UpgradeManager kontrolü
        if (UpgradeManager.instance == null)
        {
            gameObject.AddComponent<UpgradeManager>();
        }
        
        // UIManager kontrolü
        if (UIManager.instance == null)
        {
            gameObject.AddComponent<UIManager>();
        }
        
        if (autoStartGameOnLoad)
        {
            StartGame(false);
        }
        else
        {
            // Oyunu başlat (menü gösterilecek)
            isGameActive = false;
            SetPause(PauseSource.MainMenu, true);
        }

        if (Settings != null)
        {
            Settings.SettingsChanged -= HandleSettingsChanged;
            Settings.SettingsChanged += HandleSettingsChanged;
        }

        if (Loc != null)
        {
            Loc.LanguageChanged -= HandleLanguageChanged;
            Loc.LanguageChanged += HandleLanguageChanged;
        }
    }

    private void OnDestroy()
    {
        if (Settings != null)
            Settings.SettingsChanged -= HandleSettingsChanged;

        if (Loc != null)
            Loc.LanguageChanged -= HandleLanguageChanged;
    }

    private void HandleSettingsChanged()
    {
        if (settingsPanel != null && settingsPanel.activeSelf)
            UpdateSettingsUI();
    }

    private void HandleLanguageChanged()
    {
        UpdateSettingsUI();
        UpdateMainMenuLocalizedText();
        UpdatePauseLocalizedText();
        UpdateGameOverLocalizedText();
        UpdateShopLocalizedText();
    }

    private static string T(string key, string fallback = null)
    {
        return LocalizationManager.T(key, fallback);
    }

    private void ChangeLanguage(int direction)
    {
        if (Settings == null) return;
        int value = (int)Settings.Language + direction;
        if (value < 0) value = 1;
        if (value > 1) value = 0;
        Settings.SetLanguage((GameLanguage)value);
        UpdateSettingsUI();
    }

    private Slider CreateLabeledSliderRow(Transform parent, string rowName, Vector2 anchoredPos)
    {
        GameObject row = new GameObject(rowName);
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 1f);
        rowRect.anchorMax = new Vector2(0.5f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = anchoredPos;
        rowRect.sizeDelta = new Vector2(360, 34);

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = "";
        label.fontSize = 16;
        label.color = new Color(0.85f, 0.9f, 0.95f);
        label.alignment = TextAlignmentOptions.Left;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(0, 1);
        labelRect.pivot = new Vector2(0, 0.5f);
        labelRect.anchoredPosition = new Vector2(10, 0);
        labelRect.sizeDelta = new Vector2(150, 0);

        GameObject valueObj = new GameObject("Value");
        valueObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI value = valueObj.AddComponent<TextMeshProUGUI>();
        value.text = "";
        value.fontSize = 14;
        value.color = new Color(1f, 1f, 0.7f);
        value.alignment = TextAlignmentOptions.Right;
        RectTransform valueRect = value.rectTransform;
        valueRect.anchorMin = new Vector2(1, 0);
        valueRect.anchorMax = new Vector2(1, 1);
        valueRect.pivot = new Vector2(1, 0.5f);
        valueRect.anchoredPosition = new Vector2(-10, 0);
        valueRect.sizeDelta = new Vector2(60, 0);

        GameObject sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(row.transform, false);
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0, 0);
        sliderRect.anchorMax = new Vector2(1, 1);
        sliderRect.offsetMin = new Vector2(165, 8);
        sliderRect.offsetMax = new Vector2(-80, -8);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.transition = Selectable.Transition.ColorTint;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        Image bg = bgObj.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.2f, 0.25f, 1f);
        RectTransform bgRect = bg.rectTransform;
        StretchToFill(bgRect);

        // Fill Area
        GameObject fillAreaObj = new GameObject("Fill Area");
        fillAreaObj.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
        StretchToFill(fillAreaRect);
        fillAreaRect.offsetMin = new Vector2(5, 0);
        fillAreaRect.offsetMax = new Vector2(-5, 0);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillAreaObj.transform, false);
        Image fill = fillObj.AddComponent<Image>();
        fill.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        RectTransform fillRect = fill.rectTransform;
        StretchToFill(fillRect);

        // Handle Slide Area
        GameObject handleAreaObj = new GameObject("Handle Slide Area");
        handleAreaObj.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
        StretchToFill(handleAreaRect);

        GameObject handleObj = new GameObject("Handle");
        handleObj.transform.SetParent(handleAreaObj.transform, false);
        Image handle = handleObj.AddComponent<Image>();
        handle.color = new Color(1f, 1f, 1f, 0.9f);
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0, 0.5f);
        handleRect.anchorMax = new Vector2(0, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(18, 18);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;

        // Nice defaults
        var colors = slider.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.95f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
        slider.colors = colors;

        return slider;
    }

    private Toggle CreateLabeledToggleRow(Transform parent, string rowName, Vector2 anchoredPos)
    {
        GameObject row = new GameObject(rowName);
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 1f);
        rowRect.anchorMax = new Vector2(0.5f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = anchoredPos;
        rowRect.sizeDelta = new Vector2(360, 34);

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(row.transform, false);
        TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
        label.text = "";
        label.fontSize = 16;
        label.color = new Color(0.85f, 0.9f, 0.95f);
        label.alignment = TextAlignmentOptions.Left;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0, 0);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.offsetMin = new Vector2(10, 0);
        labelRect.offsetMax = new Vector2(-70, 0);

        GameObject toggleObj = new GameObject("Toggle");
        toggleObj.transform.SetParent(row.transform, false);
        RectTransform toggleRect = toggleObj.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1, 0.5f);
        toggleRect.anchorMax = new Vector2(1, 0.5f);
        toggleRect.pivot = new Vector2(1, 0.5f);
        toggleRect.anchoredPosition = new Vector2(-10, 0);
        toggleRect.sizeDelta = new Vector2(26, 26);

        Image bg = toggleObj.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.2f, 0.25f, 1f);

        Toggle toggle = toggleObj.AddComponent<Toggle>();
        toggle.targetGraphic = bg;

        GameObject checkObj = new GameObject("Checkmark");
        checkObj.transform.SetParent(toggleObj.transform, false);
        Image check = checkObj.AddComponent<Image>();
        check.color = new Color(0.3f, 0.8f, 1f, 0.95f);
        RectTransform checkRect = check.rectTransform;
        StretchToFill(checkRect);
        checkRect.offsetMin = new Vector2(4, 4);
        checkRect.offsetMax = new Vector2(-4, -4);

        toggle.graphic = check;

        return toggle;
    }

    private static void ResetProgressPrefs()
    {
        PlayerPrefs.SetInt(PREF_MONEY, 0);

        // Upgrade seviyelerini sıfırla
        foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
            PlayerPrefs.DeleteKey("Upgrade_" + type);

        // Not: UI font seçimi ve istatistikler (TotalFishCaught/PlayTime) burada bilinçli olarak korunuyor.
        // New Game = ilerleme sıfırlama; ayarlar/lifetime stats resetlemek istenirse buraya eklenebilir.

        PlayerPrefs.Save();
    }

    private void ApplyPauseState()
    {
        isPaused = pauseMask != PauseSource.None;
        Time.timeScale = isPaused ? 0f : 1f;
    }

    private void SetPause(PauseSource source, bool enabled)
    {
        if (enabled)
            pauseMask |= source;
        else
            pauseMask &= ~source;

        ApplyPauseState();

        // UI panellerinin görünürlüğünü pause kaynaklarına göre senkronla
        if (pausePanel != null)
            pausePanel.SetActive((pauseMask & PauseSource.PauseMenu) != 0);
    }

    public void PushUIPause()
    {
        uiPauseRequests++;
        if (uiPauseRequests < 1) uiPauseRequests = 1;
        SetPause(PauseSource.UIPanel, true);
    }

    public void PopUIPause()
    {
        uiPauseRequests--;
        if (uiPauseRequests < 0) uiPauseRequests = 0;
        if (uiPauseRequests == 0)
            SetPause(PauseSource.UIPanel, false);
    }

    void EnsureUIFontSetup()
    {
        // Pixelify Sans'i TMP Settings üzerinden default font olarak kullanacağız.
        // gameFonts listesinin 0. elemanı Pixelify SDF ise onu seçer, yoksa TMP default'a düşer.
        if (gameFonts == null) gameFonts = new List<TMP_FontAsset>();

        TMP_FontAsset selected = null;
        if (gameFonts.Count > 0)
        {
            if (currentFontIndex < 0 || currentFontIndex >= gameFonts.Count)
                currentFontIndex = 0;
            selected = gameFonts[currentFontIndex];
        }
        else
        {
            selected = TMP_Settings.defaultFontAsset;
        }

        if (selected != null)
        {
            // Point filter: piksel fontların bulanık çıkmasını engeller
            if (selected.atlasTextures != null)
            {
                for (int i = 0; i < selected.atlasTextures.Length; i++)
                {
                    var tex = selected.atlasTextures[i];
                    if (tex != null) tex.filterMode = FilterMode.Point;
                }
            }

            if (selected.material != null && selected.material.mainTexture != null)
                selected.material.mainTexture.filterMode = FilterMode.Point;

            TMP_Settings.defaultFontAsset = selected;
        }

        // Fallback listesini temizle (Pixelify görünümünü bozmasın)
        TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();

        // Liste boşsa seçilmiş fontu ekle
        if (gameFonts.Count == 0)
        {
            if (selected != null)
            {
                gameFonts.Add(selected);
                currentFontIndex = 0;
                PlayerPrefs.SetInt(PREF_SELECTED_FONT, currentFontIndex);
                PlayerPrefs.Save();
            }
            return;
        }

        // Seçili font listede varsa indeksi ona çek
        if (selected != null)
        {
            int idx = gameFonts.IndexOf(selected);
            if (idx >= 0)
            {
                currentFontIndex = idx;
                PlayerPrefs.SetInt(PREF_SELECTED_FONT, currentFontIndex);
                PlayerPrefs.Save();
            }
        }
    }

    void CreateUI()
    {
        // 1. Canvas Oluştur (Eğer yoksa)
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
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

        // Cache canvas transform for later UI creation
        cachedCanvasTransform = canvas != null ? canvas.transform : null;

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
            GameObject moneyPanel = CreateImagePanel(
                "MoneyPanel",
                canvas.transform,
                new Color(0.08f, 0.12f, 0.18f, 0.85f),
                new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(0, 1),
                new Vector2(20, -20),
                new Vector2(200, 50));

            moneyText = CreateTMPTextObject("MoneyText", moneyPanel.transform);
            
            moneyText.fontSize = 32;
            moneyText.color = new Color(0.5f, 1f, 0.5f);
            moneyText.alignment = TextAlignmentOptions.Center;
            moneyText.fontStyle = FontStyles.Bold;
            moneyText.outlineWidth = 0.05f;
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
            GameObject timePanel = CreateImagePanel(
                "TimePanel",
                canvas.transform,
                new Color(0.08f, 0.12f, 0.18f, 0.85f),
                new Vector2(1, 1),
                new Vector2(1, 1),
                new Vector2(1, 1),
                new Vector2(-20, -20),
                new Vector2(120, 50));

            timeText = CreateTMPTextObject("TimeText", timePanel.transform);

            timeText.fontSize = 28;
            timeText.color = Color.white;
            timeText.alignment = TextAlignmentOptions.Center;
            timeText.fontStyle = FontStyles.Bold;
            timeText.outlineWidth = 0.05f;
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
            feedbackText.outlineWidth = 0.05f;
            feedbackText.outlineColor = new Color(0, 0, 0, 0.7f);
            
            RectTransform rect = feedbackText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -120);
            rect.sizeDelta = new Vector2(520, 90);
            
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
            txt.text = T("pause.title", "DURAKLATILDI");
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
            title.text = T("gameOver.title", "OYUN BITTI");
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

            CreateStretchedLabel(btnObj.transform, "Text", "YENIDEN", 22, Color.white);

            btn.onClick.AddListener(RestartGame);

            ApplyFont(gameOverPanel);
            gameOverPanel.SetActive(false);
        }
    }
    
    void CreateMainMenu()
    {
        Transform canvasTr = GetCanvasTransform();
        if (canvasTr == null) return;
        
        mainMenuPanel = new GameObject("MainMenuPanel");
        mainMenuPanel.transform.SetParent(canvasTr, false);
        
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
        title.text = T("menu.title", "BALIKCI");
        title.fontSize = 72;
        title.color = new Color(0.3f, 0.8f, 1f);
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        title.outlineWidth = 0.05f;
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
        subtitle.text = T("menu.subtitle", "2D Fishing Game");
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
        moneyInfo.SetText(T("menu.totalMoneyFmt", "Toplam Para: ${0}"), money);
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
            // If progress was reset (e.g., after New Game), show START in the same slot.
            string primaryKey = money == 0 ? "menu.start" : "menu.continue";
            string primaryFallback = money == 0 ? "START" : "CONTINUE";
            CreateMenuButton(content.transform, "Continue", T(primaryKey, primaryFallback), new Vector2(0, -220), new Color(0.15f, 0.4f, 0.2f), () => StartGame(false));
        }
        
        // Yeni Oyun Butonu
        float newGameY = isFirstStart ? -220 : -290;
        CreateMenuButton(content.transform, "NewGame", T("menu.newGame", "YENI OYUN"), new Vector2(0, newGameY), new Color(0.2f, 0.35f, 0.5f), () => ShowNewGameConfirm());
        
        // Ayarlar Butonu
        float settingsY = newGameY - 70;
        CreateMenuButton(content.transform, "Settings", T("menu.settings", "SETTINGS"), new Vector2(0, settingsY), new Color(0.3f, 0.3f, 0.35f), () => CreateSettingsUI());
        
        ApplyFont(mainMenuPanel);
        mainMenuPanel.SetActive(true);
    }

    void CreateMenuButton(Transform parent, string id, string text, Vector2 position, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject("Button_" + id);
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

        CreateStretchedLabel(btnObj.transform, "Text", text, 24, Color.white);

        btn.onClick.AddListener(action);
    }
    
    void CreateMenuButton(Transform parent, string text, Vector2 position, Color color, UnityEngine.Events.UnityAction action)
    {
        // Backward compatible overload: uses text as id.
        CreateMenuButton(parent, text, text, position, color, action);
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
        warn.SetText(T("confirm.newGame.warningFmt", "DİKKAT!\n\nTüm ilerlemeniz silinecek!\n(${0} para kaybedilecek)"), money);
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

        CreateStretchedLabel(yesBtn.transform, "Text", T("confirm.newGame.yesDelete", "EVET, SİL"), 18, Color.white);
        
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

        CreateStretchedLabel(noBtn.transform, "Text", T("confirm.newGame.no", "HAYIR"), 18, Color.white);
        
        noBtnComp.onClick.AddListener(() => Destroy(confirmPanel));
    }
    
    public void StartGame(bool newGame)
    {
        if (newGame)
        {
            ResetProgressPrefs();
            money = 0;
            // After New Game, stay on the main menu (do not auto-start).
            // Also ensure the primary (Continue) slot is visible, but labeled as START.
            PlayerPrefs.SetInt(PREF_FIRST_START, 0);
            PlayerPrefs.DeleteKey(PREF_PENDING_AUTOSTART);
            PlayerPrefs.Save();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        PlayerPrefs.SetInt(PREF_FIRST_START, 0);
        PlayerPrefs.Save();

        isGameActive = true;

        // Başlarken tüm pause kaynaklarını temizle
        pauseMask = PauseSource.None;
        uiPauseRequests = 0;
        ApplyPauseState();

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        UpdateMoneyUI();

    }

    void Start()
    {
        cachedFishSpawner = FindFirstObjectByType<FishSpawner>();
        UpdateMoneyUI();

        // Ensure subscriptions are set after all Awake() calls.
        SubscribeToManagerEvents();

        if (pausePanel != null) pausePanel.SetActive(false);

        // Ana menüyü göster
        if (!isGameActive && mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        // Shop UI oluştur (Basitçe)
        CreateShopUI();

        // Sahnedeki tüm TMP metinlerini Pixelify ile eşitle
        ApplyFontToAll();
    }

    void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            // Pause Kontrolü (P veya ESC tuşu)
            if (kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
            {
                // Allow closing pause menu anytime, but only allow opening while game is active.
                if ((pauseMask & PauseSource.PauseMenu) != 0 || isGameActive)
                    TogglePause();
            }

            // Shop Kontrolü (B tuşu - Buy)
            if (kb.bKey.wasPressedThisFrame)
            {
                // Allow closing shop anytime, but only allow opening while game is active and not otherwise paused.
                bool shopIsOpen = shopPanel != null && shopPanel.activeSelf;
                if (shopIsOpen || (isGameActive && !isPaused))
                    ToggleShop();
            }
        }

        if (!isGameActive || isPaused) return;

        // DayNightCycle sahnede sonradan oluşabilir ya da scene reload ile değişebilir.
        if (dayNightCycle == null && Time.unscaledTime >= nextDayNightSearchTime)
        {
            nextDayNightSearchTime = Time.unscaledTime + DAYNIGHT_SEARCH_INTERVAL;
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();
            lastMinute = -1; // Reacquire sonrası saat yazısını yenile
        }

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
        feedbackText.rectTransform.anchoredPosition = new Vector2(0, -120); // Reset position (screen-visible)

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
        PlayerPrefs.SetInt(PREF_MONEY, money);
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
            PlayerPrefs.SetInt(PREF_MONEY, money);
            PlayerPrefs.Save();
            UpdateMoneyUI();
            return true;
        }
        return false;
    }

    void UpdateMoneyUI()
    {
        if (moneyText != null)
            moneyText.SetText(T("ui.moneyAmountFmt", "${0}"), money);
    }

    public void GameOver()
    {
        isGameActive = false;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (finalScoreText != null)
                finalScoreText.SetText(T("gameOver.totalMoneyFmt", "Toplam Para: ${0}"), money);
        }

        SetPause(PauseSource.GameOver, true);
    }

    public void RestartGame()
    {
        pauseMask = PauseSource.None;
        uiPauseRequests = 0;
        ApplyPauseState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void TogglePause()
    {
        bool enablePauseMenu = (pauseMask & PauseSource.PauseMenu) == 0;
        SetPause(PauseSource.PauseMenu, enablePauseMenu);
    }
    
    public void ToggleShop()
    {
        if (shopPanel == null) return;
        
        bool isActive = !shopPanel.activeSelf;
        shopPanel.SetActive(isActive);

        // Shop açıkken oyunu durdur (PauseMenu gibi ayrı bir kaynak)
        SetPause(PauseSource.Shop, isActive);
        
        if (isActive) UpdateShopUI();
    }
    
    void CreateShopUI()
    {
        if (shopPanel != null) return;

        Transform canvasTr = GetCanvasTransform();
        if (canvasTr == null) return;
        
        shopPanel = new GameObject("ShopPanel");
        shopPanel.transform.SetParent(canvasTr, false);
        
        // Arkaplan - Ortada geniş panel
        Image bg = shopPanel.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.06f, 0.1f, 0.97f);
        
        RectTransform rect = shopPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(700, 500);
        
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
        title.text = T("shop.title", "SHOP");
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

        CreateStretchedLabel(closeBtnObj.transform, "Text", "X", 18, Color.white, TextAlignmentOptions.Center, FontStyles.Normal);
        
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
        
        string[] tabKeys = { "shop.tab.line", "shop.tab.boat", "shop.tab.fishMarket" };
        string[] tabFallbacks = { "LINE", "BOAT", "FISH MARKET" };
        for (int i = 0; i < tabKeys.Length; i++)
        {
            int tabIndex = i;
            GameObject tabBtn = new GameObject("Tab_" + i);
            tabBtn.transform.SetParent(tabBar.transform, false);
            Button btn = tabBtn.AddComponent<Button>();
            Image btnImg = tabBtn.AddComponent<Image>();
            btnImg.color = (i == currentShopTab) ? new Color(0.15f, 0.3f, 0.5f) : new Color(0.1f, 0.12f, 0.18f);

            TextMeshProUGUI txt = CreateStretchedLabel(
                tabBtn.transform,
                "Text",
                T(tabKeys[i], tabFallbacks[i]),
                14,
                (i == currentShopTab) ? Color.white : new Color(0.6f, 0.6f, 0.6f));
            txt.fontStyle = FontStyles.Bold;

            btn.onClick.AddListener(() =>
            {
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
                CreateCategoryHeader(container, T("shop.category.general", "GENEL"));
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
            iconTxt.text = T($"shop.icon.{upg.icon}", upg.icon);
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
            string upgTitle = LocalizationManager.T($"upgrade.{upg.type}.name", upg.turkishName);
            string upgDesc = LocalizationManager.T($"upgrade.{upg.type}.desc", upg.description);
            nameTxt.text = $"<color=#FFD700>{upgTitle}</color>\n<size=10><color=#AAA>{upgDesc}</color></size>";
            nameTxt.fontSize = 13;
            nameTxt.alignment = TextAlignmentOptions.Left;
            LayoutElement nameLayout = nameObj.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;
            nameLayout.minWidth = 150;
            
            // Level göstergesi
            GameObject levelObj = new GameObject("Level");
            levelObj.transform.SetParent(item.transform, false);
            TextMeshProUGUI levelTxt = levelObj.AddComponent<TextMeshProUGUI>();
            levelTxt.SetText(T("shop.levelFmt", "Lv.{0}/{1}"), level, upg.maxLevel);
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
                buyTxt.text = T("shop.max", "MAX");
                buyTxt.color = new Color(0.6f, 0.6f, 0.6f);
                buyImg.color = new Color(0.2f, 0.2f, 0.25f);
                btn.interactable = false;
            }
            else if (money >= cost)
            {
                buyTxt.SetText(T("ui.moneyAmountFmt", "${0}"), cost);
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
                buyTxt.SetText(T("ui.moneyAmountFmt", "${0}"), cost);
                buyTxt.color = new Color(0.8f, 0.4f, 0.4f);
                buyImg.color = new Color(0.4f, 0.15f, 0.15f);
                btn.interactable = false;
            }
        }
    }
    
    void CreateFishMarket(Transform container)
    {
        // Başlık
        CreateCategoryHeader(container, T("shop.fishPricesTitle", "FISH PRICES"));
        
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
            txt.text = string.Format(T("shop.weatherBonusActiveRichFmt", "<color=#90EE90>Hava Durumu Bonusu Aktif!</color>{0}"), weatherInfo);
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
        FishSpawner spawner = cachedFishSpawner;
        if (spawner == null)
        {
            spawner = FindFirstObjectByType<FishSpawner>();
            cachedFishSpawner = spawner;
        }
        
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
            string rarityLabel = rarity <= 0.05f
                ? T("shop.rarity.legendary", "Efsanevi")
                : rarity <= 0.1f
                    ? T("shop.rarity.rare", "Nadir")
                    : rarity <= 0.2f
                        ? T("shop.rarity.uncommon", "Sıradışı")
                        : T("shop.rarity.common", "Yaygın");
            string rarityStr = rarity <= 0.05f
                ? $"<color=#FF66FF>{rarityLabel}</color>"
                : rarity <= 0.1f
                    ? $"<color=#9966FF>{rarityLabel}</color>"
                    : rarity <= 0.2f
                        ? $"<color=#66CCFF>{rarityLabel}</color>"
                        : $"<color=#AAAAAA>{rarityLabel}</color>";
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
            priceTxt.text = string.Format(T("shop.priceRichFmt", "<color=#90EE90>${0}</color>"), finalPrice);
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
        infoTxt.text = T("shop.info", "<size=11><color=#888>In stormy weather, rare fish appear more often!\nThe Lucky Bait upgrade increases rare fish chance.</color></size>");
        infoTxt.fontSize = 11;
        infoTxt.alignment = TextAlignmentOptions.Center;
    }

    void CleanupOldUI(Transform parent, string objName)
    {
        Transform old = parent.Find(objName);
        if (old == null) return;

        // Runtime UI cleanup; avoid DestroyImmediate to keep behavior safe/consistent.
        Destroy(old.gameObject);
    }

    // --- Font Sistemi ---

    public void ApplyFontToAll()
    {
        if (gameFonts == null || gameFonts.Count == 0) return;
        if (currentFontIndex < 0 || currentFontIndex >= gameFonts.Count) currentFontIndex = 0;

        TMP_FontAsset targetFont = gameFonts[currentFontIndex];
        if (targetFont == null) return;

        // Sahnedeki tüm TMP_Text bileşenlerini bul (TextMeshProUGUI + TextMeshPro)
        TMP_Text[] allTexts = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);
        foreach (var txt in allTexts)
        {
            txt.font = targetFont;
            // Olası material override'larını da temizle (bazı objeler font değiştirse bile eski materyalle kalabiliyor)
            txt.fontSharedMaterial = targetFont.material;
            txt.SetAllDirty(); // Mesh'i yenilemeye zorla
        }
    }

    public void ApplyFont(GameObject root)
    {
        if (gameFonts == null || gameFonts.Count == 0) return;
        if (currentFontIndex < 0 || currentFontIndex >= gameFonts.Count) currentFontIndex = 0;

        TMP_FontAsset targetFont = gameFonts[currentFontIndex];
        if (targetFont == null) return;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (var txt in texts)
        {
            txt.font = targetFont;
            txt.fontSharedMaterial = targetFont.material;
            txt.SetAllDirty(); // Mesh'i yenilemeye zorla
        }
    }

    public void ChangeFont(int direction)
    {
        if (gameFonts == null || gameFonts.Count == 0) return;

        currentFontIndex += direction;
        if (currentFontIndex >= gameFonts.Count) currentFontIndex = 0;
        if (currentFontIndex < 0) currentFontIndex = gameFonts.Count - 1;

        PlayerPrefs.SetInt(PREF_SELECTED_FONT, currentFontIndex);
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

        Transform canvasTr = GetCanvasTransform();
        if (canvasTr == null) return;

        settingsPanel = new GameObject("SettingsPanel");
        settingsPanel.transform.SetParent(canvasTr, false);

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
        contentRect.sizeDelta = new Vector2(480, 580);

        Outline outline = content.AddComponent<Outline>();
        outline.effectColor = new Color(0.3f, 0.6f, 0.9f, 0.5f);
        outline.effectDistance = new Vector2(2, -2);

        // Başlık
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(content.transform, false);
        TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
        title.text = T("settings.title", "SETTINGS");
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

        // Dil Başlık
        GameObject langTitleObj = new GameObject("LanguageTitle");
        langTitleObj.transform.SetParent(content.transform, false);
        TextMeshProUGUI langTitle = langTitleObj.AddComponent<TextMeshProUGUI>();
        langTitle.text = T("settings.language", "Dil");
        langTitle.fontSize = 18;
        langTitle.alignment = TextAlignmentOptions.Center;
        langTitle.color = new Color(0.7f, 0.8f, 0.9f);
        RectTransform langTitleRect = langTitle.rectTransform;
        langTitleRect.anchorMin = new Vector2(0.5f, 1f);
        langTitleRect.anchorMax = new Vector2(0.5f, 1f);
        langTitleRect.pivot = new Vector2(0.5f, 1f);
        langTitleRect.anchoredPosition = new Vector2(0, -90);
        langTitleRect.sizeDelta = new Vector2(300, 30);

        // Dil Seçici Container
        GameObject langSelector = new GameObject("LanguageSelector");
        langSelector.transform.SetParent(content.transform, false);
        RectTransform lsRect = langSelector.AddComponent<RectTransform>();
        lsRect.anchorMin = new Vector2(0.5f, 1f);
        lsRect.anchorMax = new Vector2(0.5f, 1f);
        lsRect.pivot = new Vector2(0.5f, 1f);
        lsRect.anchoredPosition = new Vector2(0, -125);
        lsRect.sizeDelta = new Vector2(300, 50);

        CreateArrowButton(langSelector.transform, "<", new Vector2(-120, 0), () => ChangeLanguage(-1));
        CreateArrowButton(langSelector.transform, ">", new Vector2(120, 0), () => ChangeLanguage(1));

        GameObject langNameObj = new GameObject("LanguageName");
        langNameObj.transform.SetParent(langSelector.transform, false);
        TextMeshProUGUI langName = langNameObj.AddComponent<TextMeshProUGUI>();
        langName.text = (Settings != null && Settings.Language == GameLanguage.English)
            ? T("language.english", "English")
            : T("language.turkish", "Türkçe");
        langName.fontSize = 20;
        langName.alignment = TextAlignmentOptions.Center;
        langName.color = Color.yellow;
        RectTransform langNameRect = langName.rectTransform;
        langNameRect.anchorMin = Vector2.zero;
        langNameRect.anchorMax = Vector2.one;
        langNameRect.offsetMin = new Vector2(40, 0);
        langNameRect.offsetMax = new Vector2(-40, 0);

        // Font Seçimi Başlık
        GameObject fontTitleObj = new GameObject("FontTitle");
        fontTitleObj.transform.SetParent(content.transform, false);
        TextMeshProUGUI fontTitle = fontTitleObj.AddComponent<TextMeshProUGUI>();
        fontTitle.text = T("settings.font", "Yazı Tipi (Font)");
        fontTitle.fontSize = 18;
        fontTitle.alignment = TextAlignmentOptions.Center;
        fontTitle.color = new Color(0.7f, 0.8f, 0.9f);
        RectTransform fontTitleRect = fontTitle.rectTransform;
        fontTitleRect.anchorMin = new Vector2(0.5f, 1f);
        fontTitleRect.anchorMax = new Vector2(0.5f, 1f);
        fontTitleRect.pivot = new Vector2(0.5f, 1f);
        fontTitleRect.anchoredPosition = new Vector2(0, -185);
        fontTitleRect.sizeDelta = new Vector2(300, 30);

        // Font Seçici Container
        GameObject fontSelector = new GameObject("FontSelector");
        fontSelector.transform.SetParent(content.transform, false);
        RectTransform fsRect = fontSelector.AddComponent<RectTransform>();
        fsRect.anchorMin = new Vector2(0.5f, 1f);
        fsRect.anchorMax = new Vector2(0.5f, 1f);
        fsRect.pivot = new Vector2(0.5f, 1f);
        fsRect.anchoredPosition = new Vector2(0, -220);
        fsRect.sizeDelta = new Vector2(300, 50);

        // Sol Ok
        CreateArrowButton(fontSelector.transform, "<", new Vector2(-120, 0), () => ChangeFont(-1));

        // Sağ Ok
        CreateArrowButton(fontSelector.transform, ">", new Vector2(120, 0), () => ChangeFont(1));

        // Font İsmi
        GameObject fontNameObj = new GameObject("FontName");
        fontNameObj.transform.SetParent(fontSelector.transform, false);
        TextMeshProUGUI fontName = fontNameObj.AddComponent<TextMeshProUGUI>();
        fontName.text = T("settings.fontNone", "Font Yok");
        fontName.fontSize = 20;
        fontName.alignment = TextAlignmentOptions.Center;
        fontName.color = Color.yellow;
        RectTransform fontNameRect = fontName.rectTransform;
        fontNameRect.anchorMin = Vector2.zero;
        fontNameRect.anchorMax = Vector2.one;
        fontNameRect.offsetMin = new Vector2(40, 0);
        fontNameRect.offsetMax = new Vector2(-40, 0);

        // Ses Başlık
        GameObject audioTitleObj = new GameObject("AudioTitle");
        audioTitleObj.transform.SetParent(content.transform, false);
        TextMeshProUGUI audioTitle = audioTitleObj.AddComponent<TextMeshProUGUI>();
        audioTitle.text = T("settings.audio", "Ses");
        audioTitle.fontSize = 18;
        audioTitle.alignment = TextAlignmentOptions.Center;
        audioTitle.color = new Color(0.7f, 0.8f, 0.9f);
        RectTransform audioTitleRect = audioTitle.rectTransform;
        audioTitleRect.anchorMin = new Vector2(0.5f, 1f);
        audioTitleRect.anchorMax = new Vector2(0.5f, 1f);
        audioTitleRect.pivot = new Vector2(0.5f, 1f);
        audioTitleRect.anchoredPosition = new Vector2(0, -290);
        audioTitleRect.sizeDelta = new Vector2(300, 30);

        // Sliders / Toggles
        Slider musicSlider = CreateLabeledSliderRow(content.transform, "MusicVolumeRow", new Vector2(0, -330));
        musicSlider.onValueChanged.AddListener(v => { if (Settings != null) Settings.SetMusicVolume(v); });

        Slider sfxSlider = CreateLabeledSliderRow(content.transform, "SfxVolumeRow", new Vector2(0, -375));
        sfxSlider.onValueChanged.AddListener(v => { if (Settings != null) Settings.SetSfxVolume(v); });

        Toggle muteToggle = CreateLabeledToggleRow(content.transform, "MuteRow", new Vector2(0, -420));
        muteToggle.onValueChanged.AddListener(v => { if (Settings != null) Settings.SetMuted(v); });

        Slider shakeSlider = CreateLabeledSliderRow(content.transform, "ShakeRow", new Vector2(0, -465));
        shakeSlider.onValueChanged.AddListener(v => { if (Settings != null) Settings.SetShakeIntensity(v); });

        Toggle rarityToggle = CreateLabeledToggleRow(content.transform, "ShowRarityRow", new Vector2(0, -510));
        rarityToggle.onValueChanged.AddListener(v => { if (Settings != null) Settings.SetShowRarityOnCatch(v); });

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
        closeTxt.text = T("settings.close", "CLOSE");
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

        // Başlıklar
        Transform content = settingsPanel.transform.Find("Content");
        if (content != null)
        {
            Transform titleObj = content.Find("Title");
            if (titleObj != null)
            {
                var t = titleObj.GetComponent<TextMeshProUGUI>();
                if (t != null) t.text = T("settings.title", "SETTINGS");
            }

            Transform langTitle = content.Find("LanguageTitle");
            if (langTitle != null)
            {
                var t = langTitle.GetComponent<TextMeshProUGUI>();
                if (t != null) t.text = T("settings.language", "Dil");
            }

            Transform fontTitle = content.Find("FontTitle");
            if (fontTitle != null)
            {
                var t = fontTitle.GetComponent<TextMeshProUGUI>();
                if (t != null) t.text = T("settings.font", "Yazı Tipi (Font)");
            }

            Transform audioTitle = content.Find("AudioTitle");
            if (audioTitle != null)
            {
                var t = audioTitle.GetComponent<TextMeshProUGUI>();
                if (t != null) t.text = T("settings.audio", "Ses");
            }
        }

        // Dil adı
        if (Settings != null)
        {
            Transform langSelector = settingsPanel.transform.Find("Content/LanguageSelector/LanguageName");
            if (langSelector != null)
            {
                var t = langSelector.GetComponent<TextMeshProUGUI>();
                if (t != null)
                    t.text = Settings.Language == GameLanguage.English
                        ? T("language.english", "English")
                        : T("language.turkish", "Türkçe");
            }
        }
        
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
                    txt.text = T("settings.fontNone", "Font Yok");
                }
            }
        }

        // Slider/Toggle değerlerini senkronla
        if (Settings != null)
        {
            // Music
            Transform musicRow = settingsPanel.transform.Find("Content/MusicVolumeRow");
            if (musicRow != null)
            {
                var label = musicRow.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (label != null) label.text = T("settings.music", "Müzik");
                var slider = musicRow.Find("Slider")?.GetComponent<Slider>();
                var value = musicRow.Find("Value")?.GetComponent<TextMeshProUGUI>();
                if (slider != null) slider.SetValueWithoutNotify(Settings.MusicVolume);
                if (value != null) value.text = $"{Mathf.RoundToInt(Settings.MusicVolume * 100f)}%";
            }

            // SFX
            Transform sfxRow = settingsPanel.transform.Find("Content/SfxVolumeRow");
            if (sfxRow != null)
            {
                var label = sfxRow.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (label != null) label.text = T("settings.sfx", "Efekt");
                var slider = sfxRow.Find("Slider")?.GetComponent<Slider>();
                var value = sfxRow.Find("Value")?.GetComponent<TextMeshProUGUI>();
                if (slider != null) slider.SetValueWithoutNotify(Settings.SfxVolume);
                if (value != null) value.text = $"{Mathf.RoundToInt(Settings.SfxVolume * 100f)}%";
            }

            // Mute
            Transform muteRow = settingsPanel.transform.Find("Content/MuteRow");
            if (muteRow != null)
            {
                var label = muteRow.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (label != null) label.text = T("settings.mute", "Mute");
                var toggle = muteRow.Find("Toggle")?.GetComponent<Toggle>();
                if (toggle != null) toggle.SetIsOnWithoutNotify(Settings.Muted);
            }

            // Shake
            Transform shakeRow = settingsPanel.transform.Find("Content/ShakeRow");
            if (shakeRow != null)
            {
                var label = shakeRow.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (label != null) label.text = T("settings.shake", "Ekran Sallantısı");
                var slider = shakeRow.Find("Slider")?.GetComponent<Slider>();
                var value = shakeRow.Find("Value")?.GetComponent<TextMeshProUGUI>();
                if (slider != null) slider.SetValueWithoutNotify(Settings.ShakeIntensity);
                if (value != null) value.text = $"{Mathf.RoundToInt(Settings.ShakeIntensity * 100f)}%";
            }

            // Show rarity
            Transform rarityRow = settingsPanel.transform.Find("Content/ShowRarityRow");
            if (rarityRow != null)
            {
                var label = rarityRow.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (label != null) label.text = T("settings.showRarity", "Yakalamada Nadirlik");
                var toggle = rarityRow.Find("Toggle")?.GetComponent<Toggle>();
                if (toggle != null) toggle.SetIsOnWithoutNotify(Settings.ShowRarityOnCatch);
            }

            // Close
            Transform closeText = settingsPanel.transform.Find("Content/CloseButton/Text");
            if (closeText != null)
            {
                var t = closeText.GetComponent<TextMeshProUGUI>();
                if (t != null) t.text = T("settings.close", "CLOSE");
            }
        }
    }

    private void UpdateMainMenuLocalizedText()
    {
        if (mainMenuPanel == null) return;

        var title = mainMenuPanel.transform.Find("Content/Title")?.GetComponent<TextMeshProUGUI>();
        if (title != null) title.text = T("menu.title", title.text);

        var subtitle = mainMenuPanel.transform.Find("Content/Subtitle")?.GetComponent<TextMeshProUGUI>();
        if (subtitle != null) subtitle.text = T("menu.subtitle", subtitle.text);

        var controls = mainMenuPanel.transform.Find("Content/Controls")?.GetComponent<TextMeshProUGUI>();
        if (controls != null) controls.text = T("menu.controls", controls.text);

        // Buttons created with CreateMenuButton => Button_<ID>/Text
        var continueTxt = mainMenuPanel.transform.Find("Content/Button_Continue/Text")?.GetComponent<TextMeshProUGUI>();
        if (continueTxt != null)
        {
            string primaryKey = money == 0 ? "menu.start" : "menu.continue";
            string primaryFallback = money == 0 ? "START" : continueTxt.text;
            continueTxt.text = T(primaryKey, primaryFallback);
        }

        var newGameTxt = mainMenuPanel.transform.Find("Content/Button_NewGame/Text")?.GetComponent<TextMeshProUGUI>();
        if (newGameTxt != null) newGameTxt.text = T("menu.newGame", newGameTxt.text);

        var settingsTxt = mainMenuPanel.transform.Find("Content/Button_Settings/Text")?.GetComponent<TextMeshProUGUI>();
        if (settingsTxt != null) settingsTxt.text = T("menu.settings", settingsTxt.text);

        var moneyInfo = mainMenuPanel.transform.Find("Content/MoneyInfo")?.GetComponent<TextMeshProUGUI>();
        if (moneyInfo != null)
            moneyInfo.SetText(T("menu.totalMoneyFmt", "Toplam Para: ${0}"), money);
    }

    private void UpdatePauseLocalizedText()
    {
        if (pausePanel == null) return;
        var t = pausePanel.transform.Find("PauseText")?.GetComponent<TextMeshProUGUI>();
        if (t != null) t.text = T("pause.title", t.text);
    }

    private void UpdateGameOverLocalizedText()
    {
        if (gameOverPanel == null) return;
        var title = gameOverPanel.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        if (title != null) title.text = T("gameOver.title", title.text);

        if (finalScoreText != null)
            finalScoreText.SetText(T("gameOver.totalMoneyFmt", "Toplam Para: ${0}"), money);
    }

    private void UpdateShopLocalizedText()
    {
        if (shopPanel == null) return;

        var title = shopPanel.transform.Find("TitleBar/Title")?.GetComponent<TextMeshProUGUI>();
        if (title != null) title.text = T("shop.title", title.text);

        // Tabs
        var tabBar = shopPanel.transform.Find("TabBar");
        if (tabBar != null)
        {
            string[] tabKeys = { "shop.tab.line", "shop.tab.boat", "shop.tab.fishMarket" };
            string[] tabFallbacks = { "LINE", "BOAT", "FISH MARKET" };
            for (int i = 0; i < tabBar.childCount && i < tabKeys.Length; i++)
            {
                var tabText = tabBar.GetChild(i).Find("Text")?.GetComponent<TextMeshProUGUI>();
                if (tabText != null) tabText.text = T(tabKeys[i], tabFallbacks[i]);
            }
        }

        // If open, rebuild content so market/rarity strings refresh.
        if (shopPanel.activeSelf)
            UpdateShopUI();
    }
}
