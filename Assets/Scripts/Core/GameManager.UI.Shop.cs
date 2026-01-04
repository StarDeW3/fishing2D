using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class GameManager
{
    private static readonly string[] SHOP_TAB_KEYS = { "shop.tab.line", "shop.tab.boat", "shop.tab.fishMarket" };
    private static readonly string[] SHOP_TAB_FALLBACKS = { "LINE", "BOAT", "FISH MARKET" };

    private int currentShopTab = 0; // 0: Misina, 1: Tekne, 2: Balık Pazarı

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

        for (int i = 0; i < SHOP_TAB_KEYS.Length; i++)
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
                T(SHOP_TAB_KEYS[i], SHOP_TAB_FALLBACKS[i]),
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
                btn.onClick.AddListener(() =>
                {
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
        foreach (var f in fishList) totalWeight += f.spawnWeight;
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
            priceTxt.text = string.Format(T("shop.priceRichFmt", "<color=#90EE90>${0}</color>"), fish.score);
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

    private void UpdateShopLocalizedText()
    {
        if (shopPanel == null) return;

        var title = shopPanel.transform.Find("TitleBar/Title")?.GetComponent<TextMeshProUGUI>();
        if (title != null) title.text = T("shop.title", title.text);

        // Tabs
        var tabBar = shopPanel.transform.Find("TabBar");
        if (tabBar != null)
        {
            for (int i = 0; i < tabBar.childCount && i < SHOP_TAB_KEYS.Length; i++)
            {
                var tabText = tabBar.GetChild(i).Find("Text")?.GetComponent<TextMeshProUGUI>();
                if (tabText != null) tabText.text = T(SHOP_TAB_KEYS[i], SHOP_TAB_FALLBACKS[i]);
            }
        }

        // If open, rebuild content so market/rarity strings refresh.
        if (shopPanel.activeSelf)
            UpdateShopUI();
    }
}
