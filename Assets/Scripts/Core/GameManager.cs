using UnityEngine;
using TMPro; // TextMeshPro için gerekli
using UnityEngine.UI; // Canvas ve UI bileşenleri için
using UnityEngine.SceneManagement; // Sahne yönetimi için
using System.Collections; // Coroutines için gerekli

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("UI Referansları")]
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timeText;
    public GameObject gameOverPanel;
    public TextMeshProUGUI finalScoreText;
    public TextMeshProUGUI feedbackText; // Balık yakalandığında çıkan yazı

    [Header("Oyun Durumu")]
    public int currentScore = 0;
    public int highScore = 0; // En yüksek skor
    public bool isGameActive = true;
    public bool isPaused = false; // Duraklatma durumu

    private DayNightCycle dayNightCycle;
    private Coroutine feedbackCoroutine;
    private int lastMinute = -1; // Optimization: Track last minute to avoid string alloc every frame

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        // High Score Yükle
        highScore = PlayerPrefs.GetInt("HighScore", 0);

        CreateUI();
        
        // FishingMiniGame'i kontrol et ve ekle
        if (FindFirstObjectByType<FishingMiniGame>() == null)
        {
            gameObject.AddComponent<FishingMiniGame>();
        }
    }

    void CreateUI()
    {
        // 1. Canvas Oluştur (Eğer yoksa)
        GameObject canvasObj = GameObject.Find("Canvas");
        Canvas canvas;
        if (canvasObj == null)
        {
            canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvas = canvasObj.GetComponent<Canvas>();
        }

        // 2. Score Text Oluştur
        if (scoreText == null)
        {
            GameObject scoreObj = new GameObject("ScoreText");
            scoreObj.transform.SetParent(canvas.transform, false);
            scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
            
            // Stil
            scoreText.fontSize = 48;
            scoreText.color = new Color(1f, 0.8f, 0f); // Altın sarısı
            scoreText.alignment = TextAlignmentOptions.TopLeft;
            scoreText.fontStyle = FontStyles.Bold;
            scoreText.outlineWidth = 0.2f;
            scoreText.outlineColor = Color.black;

            // Konum (Sol Üst)
            RectTransform rect = scoreText.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(50, -50);
            rect.sizeDelta = new Vector2(400, 100);
        }

        // 3. Time Text Oluştur
        if (timeText == null)
        {
            GameObject timeObj = new GameObject("TimeText");
            timeObj.transform.SetParent(canvas.transform, false);
            timeText = timeObj.AddComponent<TextMeshProUGUI>();

            // Stil
            timeText.fontSize = 48;
            timeText.color = Color.white;
            timeText.alignment = TextAlignmentOptions.TopRight;
            timeText.fontStyle = FontStyles.Bold;
            timeText.outlineWidth = 0.2f;
            timeText.outlineColor = Color.black;

            // Konum (Sağ Üst)
            RectTransform rect = timeText.rectTransform;
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-50, -50);
            rect.sizeDelta = new Vector2(300, 100);
        }

        // 4. Feedback Text (YENİ)
        if (feedbackText == null)
        {
            GameObject fbObj = new GameObject("FeedbackText");
            fbObj.transform.SetParent(canvas.transform, false);
            feedbackText = fbObj.AddComponent<TextMeshProUGUI>();
            
            feedbackText.fontSize = 64;
            feedbackText.color = Color.yellow;
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.fontStyle = FontStyles.Bold;
            feedbackText.outlineWidth = 0.2f;
            feedbackText.outlineColor = Color.black;
            
            RectTransform rect = feedbackText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, 200); // Ekranın biraz üstünde
            rect.sizeDelta = new Vector2(800, 100);
            
            feedbackText.gameObject.SetActive(false);
        }

        // 5. Game Over Panel Oluştur
        if (gameOverPanel == null)
        {
            gameOverPanel = new GameObject("GameOverPanel");
            gameOverPanel.transform.SetParent(canvas.transform, false);
            
            // Yarı saydam siyah arka plan
            Image bg = gameOverPanel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.85f);
            
            // Tam ekran yap
            RectTransform rect = gameOverPanel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // "GAME OVER" Başlığı
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(gameOverPanel.transform, false);
            TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = "GAME OVER";
            title.fontSize = 96;
            title.color = new Color(1f, 0.2f, 0.2f); // Kırmızımsı
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;
            
            RectTransform titleRect = title.rectTransform;
            titleRect.anchoredPosition = new Vector2(0, 100);
            titleRect.sizeDelta = new Vector2(800, 200);

            // Final Score Yazısı
            GameObject finalScoreObj = new GameObject("FinalScoreText");
            finalScoreObj.transform.SetParent(gameOverPanel.transform, false);
            finalScoreText = finalScoreObj.AddComponent<TextMeshProUGUI>();
            finalScoreText.fontSize = 64;
            finalScoreText.color = Color.white;
            finalScoreText.alignment = TextAlignmentOptions.Center;
            
            RectTransform fsRect = finalScoreText.rectTransform;
            fsRect.anchoredPosition = new Vector2(0, -50);
            fsRect.sizeDelta = new Vector2(800, 100);

            // Restart Button
            GameObject btnObj = new GameObject("RestartButton");
            btnObj.transform.SetParent(gameOverPanel.transform, false);
            Button btn = btnObj.AddComponent<Button>();
            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = Color.white;
            
            RectTransform btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchoredPosition = new Vector2(0, -200);
            btnRect.sizeDelta = new Vector2(200, 60);
            
            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = "RESTART";
            btnText.fontSize = 32;
            btnText.color = Color.black;
            btnText.alignment = TextAlignmentOptions.Center;
            
            RectTransform btnTextRect = btnText.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            btn.onClick.AddListener(RestartGame);

            gameOverPanel.SetActive(false);
        }
    }

    void Start()
    {
        dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        UpdateScoreUI();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    void Update()
    {
        // Pause Kontrolü (P tuşu)
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
        {
            TogglePause();
        }

        if (!isGameActive || isPaused) return;

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
        feedbackText.rectTransform.anchoredPosition = new Vector2(0, 100); // Reset position

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

    public void AddScore(int amount)
    {
        currentScore += amount;
        
        // High Score Kontrolü
        if (currentScore > highScore)
        {
            highScore = currentScore;
            PlayerPrefs.SetInt("HighScore", highScore);
            PlayerPrefs.Save();
            
            // High Score efekti (Basitçe rengi değiştir)
            if (scoreText != null) scoreText.color = Color.cyan;
        }
        
        UpdateScoreUI();
    }

    void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.SetText("Score: {0}\nHigh: {1}", currentScore, highScore);
    }

    public void GameOver()
    {
        isGameActive = false;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            if (finalScoreText != null)
                finalScoreText.text = "Final Score: " + currentScore;
        }
        
        // Oyunu durdurmak istersen:
        Time.timeScale = 0f; 
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
        
        // Pause UI gösterilebilir (Şimdilik basitçe zamanı durduruyoruz)
        if (feedbackText != null)
        {
            if (isPaused)
            {
                feedbackText.text = "PAUSED";
                feedbackText.gameObject.SetActive(true);
                feedbackText.alpha = 1f;
            }
            else
            {
                feedbackText.gameObject.SetActive(false);
            }
        }
    }
}
