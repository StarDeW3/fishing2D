using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class GameManager
{
    private void ToggleSettingsUI()
    {
        if (settingsPanel == null)
        {
            CreateSettingsUI();
            if (settingsPanel == null) return;
            SetPause(PauseSource.UIPanel, true);
            this.UpdateSettingsUI();
            return;
        }

        bool show = !settingsPanel.activeSelf;
        settingsPanel.SetActive(show);
        SetPause(PauseSource.UIPanel, show);
        if (show) this.UpdateSettingsUI();
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

        // Subtle card background behind menu items (non-raycast)
        Image contentBg = content.AddComponent<Image>();
        contentBg.color = new Color(0.05f, 0.08f, 0.12f, 0.78f);
        contentBg.raycastTarget = false;

        // Rounded + soft shadow (more modern than a hard outline)
        ApplyPanelShadow(content);

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

        // Font değişince başlık taşmasın
        title.enableAutoSizing = true;
        title.fontSizeMax = 72;
        title.fontSizeMin = 44;
        title.textWrappingMode = TextWrappingModes.NoWrap;
        title.overflowMode = TextOverflowModes.Overflow;
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
        CreateMenuButton(content.transform, "Settings", T("menu.settings", "SETTINGS"), new Vector2(0, settingsY), new Color(0.3f, 0.3f, 0.35f), () => ToggleSettingsUI());

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

        // Cleaner, consistent tinting (avoid overshooting 0..1)
        ApplyButtonTint(btn, btnImg, color);

        Shadow btnShadow = btnObj.AddComponent<Shadow>();
        btnShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        btnShadow.effectDistance = new Vector2(0, -3);
        btnShadow.useGraphicAlpha = true;

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 1);
        btnRect.anchorMax = new Vector2(0.5f, 1);
        btnRect.pivot = new Vector2(0.5f, 1);
        btnRect.anchoredPosition = position;
        btnRect.sizeDelta = new Vector2(280, 55);

        CreateStretchedLabel(btnObj.transform, "Text", text, 24, Color.white);

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

        // Confirm panel: rounded + soft shadow
        ApplyPanelShadow(confirmPanel);

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

        yesBtnComp.onClick.AddListener(() =>
        {
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

    private void UpdateMainMenuLocalizedText()
    {
        if (mainMenuPanel == null) return;

        var title = mainMenuPanel.transform.Find("Content/Title")?.GetComponent<TextMeshProUGUI>();
        if (title != null) title.text = T("menu.title", title.text);

        var subtitle = mainMenuPanel.transform.Find("Content/Subtitle")?.GetComponent<TextMeshProUGUI>();
        if (subtitle != null) subtitle.text = T("menu.subtitle", subtitle.text);

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
}
