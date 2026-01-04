using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class GameManager
{
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

    void CreateSettingsUI()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
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
        closeTxtRect.offsetMin = Vector2.zero;
        closeTxtRect.offsetMax = Vector2.zero;

        closeBtn.onClick.AddListener(() =>
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            SetPause(PauseSource.UIPanel, false);
        });

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
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
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
        txtRect.offsetMin = Vector2.zero;
        txtRect.offsetMax = Vector2.zero;

        btn.onClick.AddListener(action);
    }
}
