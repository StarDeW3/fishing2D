using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    private const string PREF_TOTAL_FISH_CAUGHT = "TotalFishCaught";
    private const string PREF_TOTAL_MONEY_EARNED = "TotalMoneyEarned";
    private const string PREF_PLAY_TIME = "PlayTime";

    [Header("Ana Paneller")]
    public GameObject mainHUD;
    public GameObject upgradePanel;
    public GameObject statsPanel;
    public GameObject settingsPanel;

    [Header("HUD Elemanları")]
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI depthText;
    public TextMeshProUGUI fishCountText;
    public TextMeshProUGUI weatherInfoText;

    [Header("Geliştirme UI")]
    public Transform lineUpgradeContainer;
    public Transform boatUpgradeContainer;
    public TextMeshProUGUI upgradeMoneyText;

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

    // Cache (Update içinde pahalı Find* çağrılarını azaltmak için)
    private FishingRod cachedRod;
    private Hook cachedHook;
    private WaveManager cachedWaveManager;
    private float nextRefSearchTime = 0f;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        LoadStats();
    }

    void Start()
    {
        CreateAllUI();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        playTime += dt;
        if (dt > 0f) statsDirty = true;

        // Tab tuşu - Upgrade Panel
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleUpgradePanel();
            }
            if (UnityEngine.InputSystem.Keyboard.current.iKey.wasPressedThisFrame)
            {
                ToggleStatsPanel();
            }
        }

        FlushStatsIfDue();

        // HUD is currently optional; skip work when there's nothing to update.
        if (depthText != null)
            UpdateHUD();
    }

    void OnDestroy()
    {
        FlushStatsIfDue(force: true);
    }

    void OnApplicationQuit()
    {
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
        DestroyOldUI("UpgradePanel");
        DestroyOldUI("StatsPanel");

        CreateMainHUD();

        CreateUpgradePanel();
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

        // Sadece referansları null yap - GameManager zaten para/saat gösteriyor
        depthText = null;
        fishCountText = null;

        // Hiçbir panel oluşturma - ekran temiz kalsın
    }

    void CreateUpgradePanel()
    {
        upgradePanel = new GameObject("UpgradePanel");
        upgradePanel.transform.SetParent(mainCanvas.transform, false);

        // Arkaplan - Sol tarafta dikey panel
        Image bg = upgradePanel.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.05f, 0.1f, 0.92f);


        RectTransform rect = upgradePanel.GetComponent<RectTransform>();
        // Sol tarafta dikey panel
        rect.anchorMin = new Vector2(0, 0.05f);
        rect.anchorMax = new Vector2(0.35f, 0.95f);
        rect.offsetMin = new Vector2(10, 0);
        rect.offsetMax = new Vector2(0, 0);
        
        // Kenar çizgisi
        // Başlık
        CreateTextElement(upgradePanel.transform, "Title", "GELISTIRMELER",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -25), 36, TextAlignmentOptions.Center, new Color(0.9f, 0.95f, 1f));

        // Para Gösterimi
        GameObject moneyObj = CreateTextElement(upgradePanel.transform, "UpgradeMoney", "$0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -70), 28, TextAlignmentOptions.Center, new Color(0.4f, 1f, 0.4f));
        upgradeMoneyText = moneyObj.GetComponent<TextMeshProUGUI>();

        // Kapat Butonu (Toggle ile kapat ki pause state düzgün çözülsün)
        CreateCloseButton(upgradePanel.transform, ToggleUpgradePanel);

        // Misina Geliştirmeleri Bölümü
        CreateSectionHeader(upgradePanel.transform, "MISINA", new Vector2(0, -110));
        lineUpgradeContainer = CreateUpgradeContainer(upgradePanel.transform, "LineUpgrades", new Vector2(0, -160));

        // Tekne Geliştirmeleri Bölümü
        CreateSectionHeader(upgradePanel.transform, "TEKNE & GENEL", new Vector2(0, -370));
        boatUpgradeContainer = CreateUpgradeContainer(upgradePanel.transform, "BoatUpgrades", new Vector2(0, -420));

        upgradePanel.SetActive(false);
    }

    void CreateStatsPanel()
    {
        statsPanel = new GameObject("StatsPanel");
        statsPanel.transform.SetParent(mainCanvas.transform, false);

        Image bg = statsPanel.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.02f, 0.1f, 0.92f);


        RectTransform rect = statsPanel.GetComponent<RectTransform>();
        // Sağ tarafta kompakt panel
        rect.anchorMin = new Vector2(1f, 0.3f);
        rect.anchorMax = new Vector2(1f, 0.7f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-10, 0);
        rect.sizeDelta = new Vector2(280, 0);
        
        // Kenar efekti
        // Başlık
        CreateTextElement(statsPanel.transform, "Title", "ISTATISTIKLER",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), 32, TextAlignmentOptions.Center, new Color(0.9f, 0.85f, 1f));

        // Kapat Butonu (Toggle ile kapat ki pause state düzgün çözülsün)
        CreateCloseButton(statsPanel.transform, ToggleStatsPanel);

        // İstatistikler - daha kompakt
        float yPos = -70;
        
        GameObject fishStatObj = CreateTextElement(statsPanel.transform, "TotalFish", "Balik: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, yPos), 24, TextAlignmentOptions.Center);
        totalFishText = fishStatObj.GetComponent<TextMeshProUGUI>();
        totalFishText.color = new Color(0.5f, 1f, 0.8f);
        yPos -= 40;

        GameObject moneyStatObj = CreateTextElement(statsPanel.transform, "TotalMoney", "Kazanc: $0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, yPos), 24, TextAlignmentOptions.Center);
        totalMoneyEarnedText = moneyStatObj.GetComponent<TextMeshProUGUI>();
        totalMoneyEarnedText.color = new Color(0.5f, 1f, 0.5f);
        yPos -= 40;

        GameObject timeStatObj = CreateTextElement(statsPanel.transform, "PlayTime", "Sure: 0:00",
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

    void CreateSectionHeader(Transform parent, string text, Vector2 pos)
    {
        GameObject obj = new GameObject("SectionHeader");
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color = new Color(1f, 0.85f, 0.4f);
        tmp.fontStyle = FontStyles.Bold;

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(15, pos.y);
        rect.sizeDelta = new Vector2(-30, 35);
    }

    Transform CreateUpgradeContainer(Transform parent, string name, Vector2 pos)
    {
        GameObject container = new GameObject(name);
        container.transform.SetParent(parent, false);

        // Dikey layout - kartlar alt alta
        VerticalLayoutGroup layout = container.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(15, 15, 5, 5);

        RectTransform rect = container.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0.5f, 1);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(-20, 200);

        return container.transform;
    }

    void CreateCloseButton(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject("CloseButton");
        btnObj.transform.SetParent(parent, false);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.7f, 0.15f, 0.15f, 0.9f);

        Button btn = btnObj.AddComponent<Button>();
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

    public void ToggleUpgradePanel()
    {
        bool isActive = !upgradePanel.activeSelf;
        upgradePanel.SetActive(isActive);

        if (isActive)
        {
            PauseGame();
            RefreshUpgradeUI();
        }
        else
        {
            ResumeGame();
        }
    }

    public void ToggleStatsPanel()
    {
        bool isActive = !statsPanel.activeSelf;
        statsPanel.SetActive(isActive);

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

    void UpdateHUD()
    {
        // Derinlik
        if (depthText != null)
        {
            float depth = 0f;

            // Referansları arada bir yenile (sahnede yoksa her frame tarama yapma)
            if ((cachedRod == null || cachedHook == null) && Time.unscaledTime >= nextRefSearchTime)
            {
                if (cachedRod == null) cachedRod = FindFirstObjectByType<FishingRod>();
                if (cachedHook == null) cachedHook = FindFirstObjectByType<Hook>();
                nextRefSearchTime = Time.unscaledTime + 0.5f;
            }

            if (cachedWaveManager == null) cachedWaveManager = WaveManager.instance;

            // Hook derinliği
            if (cachedHook != null && cachedWaveManager != null)
            {
                float waterLevel = cachedWaveManager.GetWaveHeight(cachedHook.transform.position.x);
                depth = Mathf.Max(0, waterLevel - cachedHook.transform.position.y);
            }
            depthText.SetText("Derinlik: {0:F1}m", depth);
        }
    }

    public void RefreshUpgradeUI()
    {
        if (upgradeMoneyText != null && GameManager.instance != null)
        {
            upgradeMoneyText.SetText("${0}", GameManager.instance.money);
        }

        // Misina Geliştirmelerini Göster
        RefreshLineUpgrades();

        // Tekne Geliştirmelerini Göster
        RefreshBoatUpgrades();
    }

    void RefreshLineUpgrades()
    {
        if (lineUpgradeContainer == null || UpgradeManager.instance == null) return;

        // Temizle
        foreach (Transform child in lineUpgradeContainer)
        {
            Destroy(child.gameObject);
        }

        // Misina kategorisindeki upgrade'leri al
        var lineUpgrades = UpgradeManager.instance.GetUpgradesByCategory(UpgradeCategory.Line);
        foreach (var upg in lineUpgrades)
        {
            CreateUpgradeCard(lineUpgradeContainer, upg.turkishName, upg.icon, upg.type,
                upg.description, new Color(0.3f, 0.6f, 1f));
        }
    }

    void RefreshBoatUpgrades()
    {
        if (boatUpgradeContainer == null || UpgradeManager.instance == null) return;

        // Temizle
        foreach (Transform child in boatUpgradeContainer)
        {
            Destroy(child.gameObject);
        }

        // Tekne kategorisindeki upgrade'leri al
        var boatUpgrades = UpgradeManager.instance.GetUpgradesByCategory(UpgradeCategory.Boat);
        foreach (var upg in boatUpgrades)
        {
            CreateUpgradeCard(boatUpgradeContainer, upg.turkishName, upg.icon, upg.type,
                upg.description, new Color(0.8f, 0.6f, 0.2f));
        }
        
        // Genel kategorideki upgrade'leri de tekne bölümüne ekle
        var generalUpgrades = UpgradeManager.instance.GetUpgradesByCategory(UpgradeCategory.General);
        foreach (var upg in generalUpgrades)
        {
            CreateUpgradeCard(boatUpgradeContainer, upg.turkishName, upg.icon, upg.type,
                upg.description, new Color(0.3f, 0.8f, 0.3f));
        }
    }

    void CreateUpgradeCard(Transform parent, string title, string icon, UpgradeType type, string desc, Color accentColor)
    {
        GameObject card = new GameObject("Card_" + type.ToString());
        card.transform.SetParent(parent, false);

        // Yatay kompakt kart
        Image cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);
        
        // Yatay layout
        HorizontalLayoutGroup cardLayout = card.AddComponent<HorizontalLayoutGroup>();
        cardLayout.spacing = 8;
        cardLayout.padding = new RectOffset(10, 10, 5, 5);
        cardLayout.childAlignment = TextAnchor.MiddleLeft;
        cardLayout.childControlWidth = false;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = false;
        cardLayout.childForceExpandHeight = true;

        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(0, 45); // Yükseklik sabit, genişlik parent'a göre

        // İkon (Sol)
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI iconTxt = iconObj.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            iconTxt.font = TMP_Settings.defaultFontAsset;
        iconTxt.text = icon;
        iconTxt.fontSize = 28;
        iconTxt.alignment = TextAlignmentOptions.Center;
        LayoutElement iconLayout = iconObj.AddComponent<LayoutElement>();
        iconLayout.minWidth = 40;
        iconLayout.preferredWidth = 40;

        // Başlık ve Seviye (Orta)
        GameObject infoObj = new GameObject("Info");
        infoObj.transform.SetParent(card.transform, false);
        TextMeshProUGUI infoTxt = infoObj.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            infoTxt.font = TMP_Settings.defaultFontAsset;
        
        int level = UpgradeManager.instance.GetLevel(type);
        float value = UpgradeManager.instance.GetValue(type);
        int cost = UpgradeManager.instance.GetCost(type);
        
        infoTxt.text = $"<color=#{ColorUtility.ToHtmlStringRGB(accentColor)}>{title}</color>\n<size=12>Lv.{level} | {value:F1}</size>";
        infoTxt.fontSize = 16;
        infoTxt.alignment = TextAlignmentOptions.Left;
        infoTxt.color = Color.white;
        LayoutElement infoLayout = infoObj.AddComponent<LayoutElement>();
        infoLayout.flexibleWidth = 1;

        // Satın Al Butonu (Sağ)
        GameObject btnObj = new GameObject("BuyButton");
        btnObj.transform.SetParent(card.transform, false);
        
        Image btnImg = btnObj.AddComponent<Image>();
        Button btn = btnObj.AddComponent<Button>();

        bool canAfford = cost > 0 && GameManager.instance != null && GameManager.instance.money >= cost;
        bool isMaxed = cost < 0;

        if (isMaxed)
        {
            btnImg.color = new Color(0.25f, 0.25f, 0.3f);
        }
        else if (canAfford)
        {
            btnImg.color = new Color(0.15f, 0.5f, 0.15f);
        }
        else
        {
            btnImg.color = new Color(0.4f, 0.15f, 0.15f);
        }

        LayoutElement btnLayout = btnObj.AddComponent<LayoutElement>();
        btnLayout.minWidth = 70;
        btnLayout.preferredWidth = 70;

        GameObject btnTxtObj = new GameObject("Text");
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        TextMeshProUGUI btnTxt = btnTxtObj.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            btnTxt.font = TMP_Settings.defaultFontAsset;
        btnTxt.text = isMaxed ? "MAX" : $"${cost}";
        btnTxt.fontSize = 14;
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.fontStyle = FontStyles.Bold;
        RectTransform btnTxtRect = btnTxtObj.GetComponent<RectTransform>();
        btnTxtRect.anchorMin = Vector2.zero;
        btnTxtRect.anchorMax = Vector2.one;
        btnTxtRect.offsetMin = Vector2.zero;
        btnTxtRect.offsetMax = Vector2.zero;

        if (canAfford && !isMaxed)
        {
            UpgradeType capturedType = type;
            btn.onClick.AddListener(() =>
            {
                if (UpgradeManager.instance.TryUpgrade(capturedType))
                {
                    RefreshUpgradeUI();
                    if (SoundManager.instance != null)
                        SoundManager.instance.PlaySFX(SoundManager.instance.catchSound);
                }
            });
        }
        else
        {
            btn.interactable = false;
        }

        // Kart içindeki tüm TMP yazılarını seçili fontla güncelle
        if (GameManager.instance != null)
            GameManager.instance.ApplyFont(card);
    }

    void RefreshStatsUI()
    {
        if (totalFishText != null)
            totalFishText.SetText("Balik: {0}", totalFishCaught);
        
        if (totalMoneyEarnedText != null)
            totalMoneyEarnedText.SetText("Kazanc: ${0}", totalMoneyEarned);
        
        if (playTimeText != null)
        {
            int minutes = Mathf.FloorToInt(playTime / 60);
            int seconds = Mathf.FloorToInt(playTime % 60);
            playTimeText.SetText("Oynama Suresi: {0}:{1:00}", minutes, seconds);
        }
    }

    public void OnFishCaught(int value)
    {
        totalFishCaught++;
        totalMoneyEarned += value;
        statsDirty = true;
        FlushStatsIfDue();

        if (fishCountText != null)
            fishCountText.SetText("x {0}", totalFishCaught);
    }

    void SaveStatsInternal()
    {
        PlayerPrefs.SetInt(PREF_TOTAL_FISH_CAUGHT, totalFishCaught);
        PlayerPrefs.SetInt(PREF_TOTAL_MONEY_EARNED, totalMoneyEarned);
        PlayerPrefs.SetFloat(PREF_PLAY_TIME, playTime);
        PlayerPrefs.Save();
    }

    void LoadStats()
    {
        totalFishCaught = PlayerPrefs.GetInt(PREF_TOTAL_FISH_CAUGHT, 0);
        totalMoneyEarned = PlayerPrefs.GetInt(PREF_TOTAL_MONEY_EARNED, 0);
        playTime = PlayerPrefs.GetFloat(PREF_PLAY_TIME, 0f);
    }
}
