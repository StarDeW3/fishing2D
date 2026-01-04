using UnityEngine;
using TMPro; // TextMeshPro için gerekli
using UnityEngine.UI; // Canvas ve UI bileşenleri için
using UnityEngine.SceneManagement; // Sahne yönetimi için
using System.Collections; // Coroutines için gerekli
using System.Collections.Generic; // List<> için gerekli

public partial class GameManager : MonoBehaviour
{
    public static GameManager instance;

    private const string PREF_MONEY = "Money";
    private const string PREF_FIRST_START = "FirstStart";
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
    public TextMeshProUGUI depthText;
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI feedbackText;
    public GameObject pausePanel;
    public GameObject shopPanel; // Shop UI
    public GameObject mainMenuPanel; // Ana menü
    public GameObject settingsPanel; // Ayarlar menüsü

    [Header("Font Ayarları")]
    public TMP_FontAsset uiFont; // Inspector'dan atanacak tek font

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

    private Hook cachedHook;
    private WaveManager cachedWaveManager;
    private float nextDepthSearchTime = 0f;
    private float lastDepthDisplay = -1f;

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

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    private static Color Clamp01(Color c) => new Color(Clamp01(c.r), Clamp01(c.g), Clamp01(c.b), Clamp01(c.a));

    private static void ReplaceOutlineWithShadow(GameObject go, Color shadowColor, Vector2 shadowDistance)
    {
        if (go == null) return;

        Outline outline = go.GetComponent<Outline>();
        if (outline != null) Object.Destroy(outline);

        Graphic g = go.GetComponent<Graphic>();
        if (g == null) return;

        Shadow shadow = go.GetComponent<Shadow>();
        if (shadow == null) shadow = go.AddComponent<Shadow>();
        shadow.effectColor = shadowColor;
        shadow.effectDistance = shadowDistance;
        shadow.useGraphicAlpha = true;
    }

    private static void ApplyPanelOutline(GameObject panel, Color effectColor, Vector2 effectDistance)
    {
        if (panel == null) return;
        if (panel.GetComponent<Outline>() != null) return;
        if (panel.GetComponent<Graphic>() == null) return;

        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = effectColor;
        outline.effectDistance = effectDistance;
    }

    private static void ApplyPanelShadow(GameObject panel)
    {
        if (panel == null) return;

        // Soft drop shadow for depth (no built-in sprite dependency)
        ReplaceOutlineWithShadow(panel, new Color(0f, 0f, 0f, 0.45f), new Vector2(0, -4));
    }

    private static void ApplyButtonTint(Button button, Image target, Color baseColor)
    {
        if (button == null || target == null) return;

        target.color = baseColor;

        // Ensure transitions actually affect this Image
        button.targetGraphic = target;

        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.82f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
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

        // Make UI labels resilient when switching fonts at runtime.
        tmp.enableAutoSizing = true;
        tmp.fontSizeMax = fontSize;
        tmp.fontSizeMin = Mathf.Max(10f, fontSize * 0.6f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

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

        // If this GameManager accidentally survives scene reloads (e.g. because another component on the same
        // GameObject called DontDestroyOnLoad), we must rebuild UI references after each load.
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        // Para Yükle
        money = PlayerPrefs.GetInt(PREF_MONEY, 0);
        isFirstStart = PlayerPrefs.GetInt(PREF_FIRST_START, 1) == 1;

        DevLog.Info("Boot", $"GameManager.Awake money={money} firstStart={isFirstStart}");

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
        FindOrCreateManager<SoundManager>("SoundManager");

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

        // QuestManager kontrolü (TAB görev paneli)
        if (QuestManager.instance == null)
        {
            FindOrCreateManager<QuestManager>("QuestManager");
        }

        if (autoStartGameOnLoad)
        {
            DevLog.Info("Boot", "AutoStartGameOnLoad=true -> StartGame(false)");
            StartGame(false);
        }
        else
        {
            // Oyunu başlat (menü gösterilecek)
            isGameActive = false;
            SetPause(PauseSource.MainMenu, true);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only the active singleton should react.
        if (instance != this) return;

        // Scene reload destroys scene UI objects; cached references can become "missing".
        cachedCanvasTransform = null;
        dayNightCycle = null;
        cachedFishSpawner = null;
        cachedHook = null;
        cachedWaveManager = null;
        lastMinute = -1;

        EnsureCoreUI();

        // Keep menu/pause state consistent after reload.
        if (!isGameActive)
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            SetPause(PauseSource.MainMenu, true);
        }
    }

    private void EnsureCoreUI()
    {
        // Core HUD is created at runtime; if refs are missing (common after scene reload), rebuild it.
        if (moneyText == null || timeText == null || depthText == null || feedbackText == null)
            CreateUI();

        if (mainMenuPanel == null)
            CreateMainMenu();

        // Some panels are created in Start(); if this GameManager persisted across reload, Start() won't rerun.
        CreateShopUI();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

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

        // Quest ilerlemesini de sıfırla (New Game)
        QuestManager.ResetAllQuestProgressPrefs();

        // Upgrade seviyelerini sıfırla
        foreach (UpgradeType type in System.Enum.GetValues(typeof(UpgradeType)))
            PlayerPrefs.DeleteKey("Upgrade_" + type);

        // Not: İstatistikler (TotalFishCaught/PlayTime) burada bilinçli olarak korunuyor.
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
        PauseSource before = pauseMask;
        if (enabled)
            pauseMask |= source;
        else
            pauseMask &= ~source;

        if (before != pauseMask)
            DevLog.Info("Pause", $"{source} {(enabled ? "ON" : "OFF")} -> mask={pauseMask}");

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
        TMP_FontAsset selected = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;

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

        // Keep a global fallback for missing glyphs (e.g., Turkish characters in fonts like Jersey15).
        // Fallback fonts are only used when a glyph is missing, so they won't change the look otherwise.
        TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();
        TMP_FontAsset liberationFallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
        TMP_FontAsset liberation = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (liberationFallback != null) TMP_Settings.fallbackFontAssets.Add(liberationFallback);
        else if (liberation != null) TMP_Settings.fallbackFontAssets.Add(liberation);

    }

    public void StartGame(bool newGame)
    {
        if (newGame)
        {
            DevLog.Info("Game", "StartGame(newGame=true) -> reset progress + reload scene");
            // Ensure no persistent overlay UI (DontDestroy managers) remains visible across reload.
            if (QuestManager.instance != null)
                QuestManager.instance.ForceClosePanel();

            // New Game should land on the main menu after reload.
            isGameActive = false;

            // Clear pause state before reloading the scene (Time.timeScale is global).
            pauseMask = PauseSource.None;
            uiPauseRequests = 0;
            ApplyPauseState();

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

        DevLog.Info("Game", "StartGame(newGame=false) -> gameplay active");

        PlayerPrefs.SetInt(PREF_FIRST_START, 0);
        PlayerPrefs.Save();

        // Ensure HUD exists (can be missing if the GameManager survived a reload and scene UI got destroyed).
        EnsureCoreUI();

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
            // ESC: Oyun içindeyken ana menüyü aç/kapat
            if (kb.escapeKey.wasPressedThisFrame)
            {
                HandleEscapeKey();
            }
            // P: Pause aç/kapat
            else if (kb.pKey.wasPressedThisFrame)
            {
                // Ana menü açıksa pause açma.
                if (mainMenuPanel != null && mainMenuPanel.activeSelf)
                    return;

                // Allow closing pause menu anytime, but only allow opening while game is active.
                if ((pauseMask & PauseSource.PauseMenu) != 0 || isGameActive)
                    TogglePause();
            }

            // Shop Kontrolü (B tuşu - Buy)
            if (kb.bKey.wasPressedThisFrame)
            {
                // Ana menü açıksa shop açma.
                if (mainMenuPanel != null && mainMenuPanel.activeSelf)
                    return;

                // Allow closing shop anytime, but only allow opening while game is active and not otherwise paused.
                bool shopIsOpen = shopPanel != null && shopPanel.activeSelf;
                if (shopIsOpen || (isGameActive && !isPaused))
                    ToggleShop();
            }
        }

        UpdateDepthUI();

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

    private void HandleEscapeKey()
    {
        // Öncelik: Ayarlar açıksa ESC ile geri dön (ayarları kapat).
        if (settingsPanel != null && settingsPanel.activeSelf)
        {
            settingsPanel.SetActive(false);
            SetPause(PauseSource.UIPanel, false);
            return;
        }

        // Minigame oynanırken ESC ile ana menü açmak gameplay'i bozabilir.
        if (FishingMiniGame.instance != null && FishingMiniGame.instance.IsPlaying)
        {
            // Minigame sırasında sadece pause menüyü kapatmaya izin ver.
            if ((pauseMask & PauseSource.PauseMenu) != 0)
                TogglePause();
            return;
        }

        // Oyun içindeyken ana menüyü aç/kapat.
        if (isGameActive)
        {
            ToggleMainMenuOverlay();
            return;
        }

        // Oyun aktif değilse (başlangıç menüsü vs.) mevcut davranışı bozmayalım.
    }

    private void ToggleMainMenuOverlay()
    {
        if (mainMenuPanel == null) return;

        bool show = !mainMenuPanel.activeSelf;

        if (show)
        {
            // Diğer panelleri kapat
            if (pausePanel != null) pausePanel.SetActive(false);
            if (shopPanel != null) shopPanel.SetActive(false);

            // Pause kaynaklarını temizle ve menüyü pause kaynağı olarak aç
            SetPause(PauseSource.PauseMenu, false);
            SetPause(PauseSource.Shop, false);

            mainMenuPanel.SetActive(true);
            SetPause(PauseSource.MainMenu, true);
            UpdateMainMenuLocalizedText();
        }
        else
        {
            mainMenuPanel.SetActive(false);
            SetPause(PauseSource.MainMenu, false);
        }
    }

    private void UpdateDepthUI()
    {
        if (depthText == null) return;

        // Olta atıldı mı? (Sahnede Hook var mı?)
        if (cachedHook == null)
        {
            if (Time.unscaledTime >= nextDepthSearchTime)
            {
                cachedHook = FindFirstObjectByType<Hook>();
                nextDepthSearchTime = Time.unscaledTime + 0.25f;
            }
        }
        else if (cachedHook.gameObject == null)
        {
            cachedHook = null;
        }

        bool hasHook = cachedHook != null;
        if (!hasHook)
        {
            if (depthText.gameObject.activeSelf)
                depthText.gameObject.SetActive(false);
            lastDepthDisplay = -1f;
            return;
        }

        if (!depthText.gameObject.activeSelf)
            depthText.gameObject.SetActive(true);

        if (cachedWaveManager == null)
            cachedWaveManager = WaveManager.instance;

        float depth = 0f;
        if (cachedWaveManager != null)
        {
            float waterLevel = cachedWaveManager.GetWaveHeight(cachedHook.transform.position.x);
            depth = Mathf.Max(0f, waterLevel - cachedHook.transform.position.y);
        }

        if (QuestManager.instance != null)
            QuestManager.instance.ReportDepth(depth);

        float rounded = Mathf.Round(depth * 10f) / 10f;
        if (Mathf.Abs(rounded - lastDepthDisplay) > 0.01f)
        {
            lastDepthDisplay = rounded;
            depthText.SetText(T("ui.depthFmt", "Derinlik: {0:0.0}m"), rounded);
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
        TMP_FontAsset targetFont = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
        if (targetFont == null) return;

        // Include inactive UI too (e.g., Shop/Settings panels). Filter out assets/prefabs by only touching loaded scenes.
        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (var txt in allTexts)
        {
            if (txt == null) continue;
            var scene = txt.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded) continue;

            txt.font = targetFont;
            // Olası material override'larını da temizle (bazı objeler font değiştirse bile eski materyalle kalabiliyor)
            txt.fontSharedMaterial = targetFont.material;
            txt.SetAllDirty(); // Mesh'i yenilemeye zorla
        }
    }

    public void ApplyFont(GameObject root)
    {
        TMP_FontAsset targetFont = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
        if (targetFont == null) return;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (var txt in texts)
        {
            txt.font = targetFont;
            txt.fontSharedMaterial = targetFont.material;
            txt.SetAllDirty(); // Mesh'i yenilemeye zorla
        }
    }

}
