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
    public TextMeshProUGUI depthText;
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

}
