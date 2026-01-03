using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

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

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        LoadStats();
    }

    void Start()
    {
        CreateAllUI();
    }

    void Update()
    {
        playTime += Time.deltaTime;

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

        UpdateHUD();
    }

    void CreateAllUI()
    {
        // Canvas Bul veya Oluştur
        GameObject canvasObj = GameObject.Find("Canvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("Canvas");
            mainCanvas = canvasObj.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
            mainCanvas = canvasObj.GetComponent<Canvas>();
        }

        // Eski UI objelerini temizle
        DestroyOldUI("MainHUD");
        DestroyOldUI("UpgradePanel");
        DestroyOldUI("StatsPanel");

        CreateMainHUD();
        CreateUpgradePanel();
        CreateStatsPanel();
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
        Outline panelOutline = upgradePanel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.2f, 0.4f, 0.6f, 0.6f);
        panelOutline.effectDistance = new Vector2(3, -3);

        // Başlık
        CreateTextElement(upgradePanel.transform, "Title", "GELISTIRMELER",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -25), 36, TextAlignmentOptions.Center, new Color(0.9f, 0.95f, 1f));

        // Para Gösterimi
        GameObject moneyObj = CreateTextElement(upgradePanel.transform, "UpgradeMoney", "$0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -70), 28, TextAlignmentOptions.Center, new Color(0.4f, 1f, 0.4f));
        upgradeMoneyText = moneyObj.GetComponent<TextMeshProUGUI>();

        // Kapat Butonu
        CreateCloseButton(upgradePanel.transform, () => upgradePanel.SetActive(false));

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
        Outline statsOutline = statsPanel.AddComponent<Outline>();
        statsOutline.effectColor = new Color(0.5f, 0.3f, 0.6f, 0.5f);
        statsOutline.effectDistance = new Vector2(-3, -3);

        // Başlık
        CreateTextElement(statsPanel.transform, "Title", "ISTATISTIKLER",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), 32, TextAlignmentOptions.Center, new Color(0.9f, 0.85f, 1f));

        // Kapat Butonu
        CreateCloseButton(statsPanel.transform, () => statsPanel.SetActive(false));

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
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color ?? Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.outlineWidth = 0.15f;
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
        txt.text = "✕";
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
        Time.timeScale = 0f;
        if (GameManager.instance != null) GameManager.instance.isPaused = true;
    }

    void ResumeGame()
    {
        Time.timeScale = 1f;
        if (GameManager.instance != null) GameManager.instance.isPaused = false;
    }

    void UpdateHUD()
    {
        // Derinlik
        if (depthText != null)
        {
            float depth = 0f;
            FishingRod rod = FindFirstObjectByType<FishingRod>();
            if (rod != null && rod.transform.childCount > 0)
            {
                // Hook derinliği
                Hook hook = FindFirstObjectByType<Hook>();
                if (hook != null && WaveManager.instance != null)
                {
                    float waterLevel = WaveManager.instance.GetWaveHeight(hook.transform.position.x);
                    depth = Mathf.Max(0, waterLevel - hook.transform.position.y);
                }
            }
            depthText.text = $"Derinlik: {depth:F1}m";
        }
    }

    public void RefreshUpgradeUI()
    {
        if (upgradeMoneyText != null && GameManager.instance != null)
        {
            upgradeMoneyText.text = $"${GameManager.instance.money}";
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
    }

    void RefreshStatsUI()
    {
        if (totalFishText != null)
            totalFishText.text = $"🐟 Balık: {totalFishCaught}";
        
        if (totalMoneyEarnedText != null)
            totalMoneyEarnedText.text = $"💰 Kazanç: ${totalMoneyEarned}";
        
        if (playTimeText != null)
        {
            int minutes = Mathf.FloorToInt(playTime / 60);
            int seconds = Mathf.FloorToInt(playTime % 60);
            playTimeText.text = $"⏱️ Oynama Süresi: {minutes}:{seconds:00}";
        }
    }

    public void OnFishCaught(int value)
    {
        totalFishCaught++;
        totalMoneyEarned += value;
        SaveStats();

        if (fishCountText != null)
            fishCountText.text = $"🐟 x {totalFishCaught}";
    }

    void SaveStats()
    {
        PlayerPrefs.SetInt("TotalFishCaught", totalFishCaught);
        PlayerPrefs.SetInt("TotalMoneyEarned", totalMoneyEarned);
        PlayerPrefs.SetFloat("PlayTime", playTime);
        PlayerPrefs.Save();
    }

    void LoadStats()
    {
        totalFishCaught = PlayerPrefs.GetInt("TotalFishCaught", 0);
        totalMoneyEarned = PlayerPrefs.GetInt("TotalMoneyEarned", 0);
        playTime = PlayerPrefs.GetFloat("PlayTime", 0f);
    }
}
