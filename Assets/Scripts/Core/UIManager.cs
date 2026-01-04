using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    private const string LOG_CAT = "UIManager";

    private const string PREF_TOTAL_FISH_CAUGHT = "TotalFishCaught";
    private const string PREF_TOTAL_MONEY_EARNED = "TotalMoneyEarned";
    private const string PREF_PLAY_TIME = "PlayTime";

    [Header("Ana Paneller")]
    public GameObject mainHUD;
    public GameObject statsPanel;

    [Header("İstatistik UI")]
    public TextMeshProUGUI totalFishText;
    public TextMeshProUGUI totalMoneyEarnedText;
    public TextMeshProUGUI playTimeText;

    private Canvas mainCanvas;
    private int totalFishCaught = 0;
    private int totalMoneyEarned = 0;
    private float playTime = 0f;

    private bool statsDirty = false;
    private float nextStatsSaveTime = 0f;
    private const float STATS_SAVE_INTERVAL = 5f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        DevLog.Info(LOG_CAT, "Awake");
        LoadStats();
    }

    private void OnEnable()
    {
        if (LocalizationManager.instance != null)
        {
            LocalizationManager.instance.LanguageChanged -= OnLanguageChanged;
            LocalizationManager.instance.LanguageChanged += OnLanguageChanged;
        }

        DevLog.Info(LOG_CAT, "OnEnable");
    }

    private void OnDisable()
    {
        if (LocalizationManager.instance != null)
            LocalizationManager.instance.LanguageChanged -= OnLanguageChanged;

        DevLog.Info(LOG_CAT, "OnDisable");
    }

    private void OnLanguageChanged()
    {
        DevLog.Info(LOG_CAT, "LanguageChanged -> refresh stats UI");
        RefreshLocalizedStaticText();
        RefreshStatsUI();
    }

    void Start()
    {
        DevLog.Info(LOG_CAT, "Start -> CreateAllUI");
        CreateAllUI();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        playTime += dt;
        if (dt > 0f) statsDirty = true;

        // Tab tuşu - Upgrade Panel (özellik iptal edildi)
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.iKey.wasPressedThisFrame)
            {
                ToggleStatsPanel();
            }
        }

        FlushStatsIfDue();
    }

    void OnDestroy()
    {
        DevLog.Info(LOG_CAT, "OnDestroy -> flush stats");
        FlushStatsIfDue(force: true);
    }

    void OnApplicationQuit()
    {
        DevLog.Info(LOG_CAT, "OnApplicationQuit -> flush stats");
        FlushStatsIfDue(force: true);
    }

    private void FlushStatsIfDue(bool force = false)
    {
        if (!statsDirty) return;
        if (!force && Time.unscaledTime < nextStatsSaveTime) return;

        SaveStatsInternal();
        statsDirty = false;
        nextStatsSaveTime = Time.unscaledTime + STATS_SAVE_INTERVAL;
    }

    void CreateAllUI()
    {
        DevLog.Info(LOG_CAT, "CreateAllUI");
        // Canvas Bul veya Oluştur
        mainCanvas = null;

        if (GameManager.instance != null)
        {
            Transform t = GameManager.instance.CanvasTransform;
            if (t != null) mainCanvas = t.GetComponent<Canvas>();
        }

        if (mainCanvas == null)
            mainCanvas = FindFirstObjectByType<Canvas>();

        if (mainCanvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Eski UI objelerini temizle
        DestroyOldUI("MainHUD");
        DestroyOldUI("StatsPanel");

        CreateMainHUD();
        CreateStatsPanel();

        // Runtime oluşturulan tüm TMP yazılarında seçili fontun uygulanmasını garanti et
        if (GameManager.instance != null)
            GameManager.instance.ApplyFontToAll();
    }

    void DestroyOldUI(string objName)
    {
        if (mainCanvas == null) return;
        Transform old = mainCanvas.transform.Find(objName);
        if (old != null) Destroy(old.gameObject);
    }


    void CreateMainHUD()
    {
        mainHUD = new GameObject("MainHUD");
        mainHUD.transform.SetParent(mainCanvas.transform, false);

        // Hiçbir panel oluşturma - ekran temiz kalsın
    }

    void CreateStatsPanel()
    {
        statsPanel = new GameObject("StatsPanel");
        statsPanel.transform.SetParent(mainCanvas.transform, false);

        Image bg = statsPanel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.02f, 0.1f, 0.92f);

        // Soft shadow (avoid built-in sprite paths; they vary by Unity version)
        Shadow shadow = statsPanel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(0, -4);
        shadow.useGraphicAlpha = true;


        RectTransform rect = statsPanel.GetComponent<RectTransform>();
        // Sağ tarafta kompakt panel
        rect.anchorMin = new Vector2(1f, 0.3f);
        rect.anchorMax = new Vector2(1f, 0.7f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-10, 0);
        rect.sizeDelta = new Vector2(280, 0);

        // Kenar efekti
        // Başlık
        CreateTextElement(statsPanel.transform, "Title", LocalizationManager.T("ui.stats.title", "ISTATISTIKLER"),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), 32, TextAlignmentOptions.Center, new Color(0.9f, 0.85f, 1f));

        // Kapat Butonu (Toggle ile kapat ki pause state düzgün çözülsün)
        CreateCloseButton(statsPanel.transform, ToggleStatsPanel);

        // İstatistikler - daha kompakt
        float yPos = -70;

        GameObject fishStatObj = CreateTextElement(statsPanel.transform, "TotalFish", LocalizationManager.Format("ui.stats.fishFmt", "Fish: {0}", 0),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, yPos), 24, TextAlignmentOptions.Center);
        totalFishText = fishStatObj.GetComponent<TextMeshProUGUI>();
        totalFishText.color = new Color(0.5f, 1f, 0.8f);
        yPos -= 40;

        GameObject moneyStatObj = CreateTextElement(statsPanel.transform, "TotalMoney", LocalizationManager.Format("ui.stats.earningsFmt", "Earnings: ${0}", 0),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, yPos), 24, TextAlignmentOptions.Center);
        totalMoneyEarnedText = moneyStatObj.GetComponent<TextMeshProUGUI>();
        totalMoneyEarnedText.color = new Color(0.5f, 1f, 0.5f);
        yPos -= 40;

        GameObject timeStatObj = CreateTextElement(statsPanel.transform, "PlayTime", LocalizationManager.Format("ui.stats.playTimeFmt", "Oynama Suresi: {0}:{1:00}", 0, 0),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, yPos), 24, TextAlignmentOptions.Center);
        playTimeText = timeStatObj.GetComponent<TextMeshProUGUI>();
        playTimeText.color = new Color(1f, 0.9f, 0.5f);

        statsPanel.SetActive(false);
    }

    GameObject CreateTextElement(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, int fontSize, TextAlignmentOptions align, Color? color = null)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color ?? Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.05f;
        tmp.outlineColor = Color.black;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(600, 50);

        return obj;
    }

    void CreateCloseButton(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject("CloseButton");
        btnObj.transform.SetParent(parent, false);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.7f, 0.15f, 0.15f, 0.9f);

        Button btn = btnObj.AddComponent<Button>();

        // Modern color-tint states (same behavior)
        btn.transition = Selectable.Transition.ColorTint;
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
        colors.pressedColor = new Color(1f, 1f, 1f, 0.82f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        btn.onClick.AddListener(onClick);
        btn.onClick.AddListener(() => ResumeGame());

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(1, 1);
        rect.anchoredPosition = new Vector2(-8, -8);
        rect.sizeDelta = new Vector2(35, 35);

        // X yazısı
        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI txt = txtObj.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            txt.font = TMP_Settings.defaultFontAsset;
        txt.text = "X";
        txt.fontSize = 24;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = Color.white;

        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;
    }

    public void ToggleStatsPanel()
    {
        bool isActive = !statsPanel.activeSelf;
        statsPanel.SetActive(isActive);

        DevLog.Info(LOG_CAT, $"StatsPanel {(isActive ? "opened" : "closed")}");

        if (isActive)
        {
            PauseGame();
            RefreshStatsUI();
        }
        else
        {
            ResumeGame();
        }
    }

    void PauseGame()
    {
        if (GameManager.instance != null)
            GameManager.instance.PushUIPause();
    }

    void ResumeGame()
    {
        if (GameManager.instance != null)
            GameManager.instance.PopUIPause();
    }

    void RefreshStatsUI()
    {
        if (totalFishText != null)
            totalFishText.SetText(LocalizationManager.T("ui.stats.fishFmt", "Fish: {0}"), totalFishCaught);

        if (totalMoneyEarnedText != null)
            totalMoneyEarnedText.SetText(LocalizationManager.T("ui.stats.earningsFmt", "Earnings: ${0}"), totalMoneyEarned);

        if (playTimeText != null)
        {
            int minutes = Mathf.FloorToInt(playTime / 60);
            int seconds = Mathf.FloorToInt(playTime % 60);
            playTimeText.SetText(LocalizationManager.T("ui.stats.playTimeFmt", "Oynama Suresi: {0}:{1:00}"), minutes, seconds);
        }
    }

    private void RefreshLocalizedStaticText()
    {
        if (statsPanel != null)
        {
            var title = statsPanel.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
            if (title != null) title.text = LocalizationManager.T("ui.stats.title", title.text);
        }
    }

    public void OnFishCaught(int value)
    {
        totalFishCaught++;
        totalMoneyEarned += value;
        statsDirty = true;
        FlushStatsIfDue();
    }

    void SaveStatsInternal()
    {
        PlayerPrefs.SetInt(PREF_TOTAL_FISH_CAUGHT, totalFishCaught);
        PlayerPrefs.SetInt(PREF_TOTAL_MONEY_EARNED, totalMoneyEarned);
        PlayerPrefs.SetFloat(PREF_PLAY_TIME, playTime);
        PlayerPrefs.Save();

        DevLog.Info(LOG_CAT, $"SaveStats (fish={totalFishCaught}, earned=${totalMoneyEarned}, playTime={playTime:0.0}s)");
    }

    void LoadStats()
    {
        totalFishCaught = PlayerPrefs.GetInt(PREF_TOTAL_FISH_CAUGHT, 0);
        totalMoneyEarned = PlayerPrefs.GetInt(PREF_TOTAL_MONEY_EARNED, 0);
        playTime = PlayerPrefs.GetFloat(PREF_PLAY_TIME, 0f);

        DevLog.Info(LOG_CAT, $"LoadStats (fish={totalFishCaught}, earned=${totalMoneyEarned}, playTime={playTime:0.0}s)");
    }
}
