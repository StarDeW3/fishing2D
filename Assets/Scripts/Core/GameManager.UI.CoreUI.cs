using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class GameManager
{
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
            CleanupOldUI(canvas.transform, "DepthText");
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
        depthText = null;
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
                new Vector2(160, 80));

            timeText = CreateTMPTextObject("TimeText", timePanel.transform);

            timeText.fontSize = 28;
            timeText.color = Color.white;
            timeText.alignment = TextAlignmentOptions.Center;
            timeText.fontStyle = FontStyles.Bold;
            timeText.outlineWidth = 0.05f;
            timeText.outlineColor = new Color(0, 0, 0, 0.6f);

            ApplyFont(timePanel);

            RectTransform rect = timeText.rectTransform;

            // Üst yarı: Saat
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(5, 0);
            rect.offsetMax = new Vector2(-5, -2);

            // Alt yarı: Derinlik (olta atılınca görünür)
            depthText = CreateTMPTextObject("DepthText", timePanel.transform);
            depthText.fontSize = 16;
            depthText.color = new Color(0.85f, 0.9f, 1f);
            depthText.alignment = TextAlignmentOptions.Center;
            depthText.fontStyle = FontStyles.Normal;
            depthText.outlineWidth = 0.05f;
            depthText.outlineColor = new Color(0, 0, 0, 0.6f);
            depthText.gameObject.SetActive(false);

            RectTransform depthRect = depthText.rectTransform;
            depthRect.anchorMin = new Vector2(0, 0);
            depthRect.anchorMax = new Vector2(1, 0.5f);
            depthRect.offsetMin = new Vector2(5, 2);
            depthRect.offsetMax = new Vector2(-5, 0);
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

    private void UpdatePauseLocalizedText()
    {
        if (pausePanel == null) return;
        var t = pausePanel.transform.Find("PauseText")?.GetComponent<TextMeshProUGUI>();
        if (t != null) t.text = T("pause.title", t.text);
    }

    private void UpdateGameOverLocalizedText()
    {
        if (gameOverPanel == null) return;
        var title = gameOverPanel.transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
        if (title != null) title.text = T("gameOver.title", title.text);

        if (finalScoreText != null)
            finalScoreText.SetText(T("gameOver.totalMoneyFmt", "Toplam Para: ${0}"), money);
    }
}
