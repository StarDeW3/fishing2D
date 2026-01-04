using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum QuestType
{
    CatchFishCount = 0,
    EarnMoneyFromFishing = 1,
    CatchFishDifficultyAtLeast = 2,
    BuyUpgradesCount = 3,
    ReachDepthMeters = 4,
    PlayTimeSeconds = 5,
}

[Serializable]
public class QuestDefinition
{
    [Header("Kimlik")]
    [Tooltip("Unique id used for saving progress. Keep stable once shipped.")]
    public string id = "quest_1";

    [Header("Metin")]
    public string titleTR = "Görev";
    public string titleEN = "Quest";
    [TextArea] public string descTR = "";
    [TextArea] public string descEN = "";

    [Header("Hedef")]
    public QuestType type = QuestType.CatchFishCount;

    [Tooltip("Count/money/seconds depending on type")]
    public int target = 5;

    [Tooltip("For ReachDepthMeters this is meters (e.g. 10). For difficulty quest this is min difficulty (1..5).")]
    public int parameter = 0;

    [Header("Ödül")]
    public int rewardMoney = 50;

    public string GetTitle()
    {
        if (SettingsManager.instance != null && SettingsManager.instance.Language == GameLanguage.English)
            return string.IsNullOrEmpty(titleEN) ? titleTR : titleEN;
        return string.IsNullOrEmpty(titleTR) ? titleEN : titleTR;
    }

    public string GetDesc()
    {
        if (SettingsManager.instance != null && SettingsManager.instance.Language == GameLanguage.English)
            return string.IsNullOrEmpty(descEN) ? descTR : descEN;
        return string.IsNullOrEmpty(descTR) ? descEN : descTR;
    }
}

public class QuestManager : MonoBehaviour
{
    public static QuestManager instance;

    [Header("Görevler")]
    public List<QuestDefinition> quests = new List<QuestDefinition>();

    [Header("Akış")]
    [Tooltip("Açıksa görevler sırayla açılır: sadece ilk N görev görünür, ödül aldıkça yeni görevler açılır.")]
    [SerializeField] private bool sequentialUnlock = true;
    [Min(1)] [SerializeField] private int initialUnlockedCount = 5;
    [Min(1)] [SerializeField] private int unlockOnClaim = 1;

    [Header("Editor")]
    [SerializeField] private bool populateDefaultQuestsInEditor = true;

    private readonly Dictionary<string, float> progressById = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly HashSet<string> claimed = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> completedNotified = new HashSet<string>(StringComparer.Ordinal);

    // UI
    private GameObject panel;
    private GameObject backdrop;
    private RectTransform listRoot;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI hintText;

    private bool IsEnglish()
    {
        return SettingsManager.instance != null && SettingsManager.instance.Language == GameLanguage.English;
    }

    private string UI_QuestsTitle()
    {
        return LocalizationManager.T("ui.quests.title", IsEnglish() ? "QUESTS" : "GÖREVLER");
    }

    private string UI_PressTabToClose()
    {
        return LocalizationManager.T("ui.quests.hintTabClose", IsEnglish() ? "Press TAB to close" : "Kapatmak için TAB");
    }

    private string UI_NoQuests()
    {
        return LocalizationManager.T("ui.quests.empty", IsEnglish() ? "No quests" : "Görev yok");
    }

    private string UI_Claimed()
    {
        return LocalizationManager.T("ui.quests.claimed", IsEnglish() ? "CLAIMED" : "ALINDI");
    }

    private string UI_Claim()
    {
        return LocalizationManager.T("ui.quests.claim", IsEnglish() ? "CLAIM" : "ÖDÜLÜ AL");
    }

    private string UI_QuestCompletedFmt()
    {
        return LocalizationManager.T("ui.quests.completedFmt", IsEnglish() ? "Quest completed: {0}" : "Görev tamamlandı: {0}");
    }

    private void RefreshPanelChromeTexts()
    {
        if (titleText != null) titleText.text = UI_QuestsTitle();
        if (hintText != null) hintText.text = UI_PressTabToClose();
    }

    private const string PREF_IDS = "Quest_AllIds";
    private const string PREF_PROGRESS_PREFIX = "Quest_Progress_";
    private const string PREF_CLAIMED_PREFIX = "Quest_Claimed_";
    private const string PREF_UNLOCKED_COUNT = "Quest_UnlockedCount";

    private int unlockedCount = 0;

    private const string LOG_CAT = "QuestManager";

    private int GetActiveQuestCount()
    {
        int total = quests != null ? quests.Count : 0;
        if (!sequentialUnlock) return total;
        return Mathf.Clamp(unlockedCount, 0, total);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        DevLog.Info(LOG_CAT, $"Awake (quests={quests?.Count ?? 0}, sequentialUnlock={sequentialUnlock}, initialUnlockedCount={initialUnlockedCount}, unlockOnClaim={unlockOnClaim})");

        EnsureDefaultQuestsIfEmpty();

        DevLog.Info(LOG_CAT, $"Defaults ensured (quests={quests?.Count ?? 0})");

        LoadState();

        completedNotified.Clear();

        if (LocalizationManager.instance != null)
            LocalizationManager.instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        if (LocalizationManager.instance != null)
            LocalizationManager.instance.LanguageChanged -= OnLanguageChanged;

        DevLog.Info(LOG_CAT, "OnDestroy");
    }

    private void OnLanguageChanged()
    {
        DevLog.Info(LOG_CAT, "LanguageChanged -> refresh quests UI");
        RefreshPanelChromeTexts();
        if (panel != null && panel.activeSelf)
            RefreshUI();
    }

    private void EnsureDefaultQuestsIfEmpty()
    {
        if (!populateDefaultQuestsInEditor) return;
        if (quests == null) quests = new List<QuestDefinition>();
        if (quests.Count > 0) return;

        quests.Add(new QuestDefinition
        {
            id = "catch_5_fish",
            titleTR = "5 balık yakala",
            titleEN = "Catch 5 fish",
            descTR = "5 balık yakala ve ödülü al.",
            descEN = "Catch 5 fish and claim the reward.",
            type = QuestType.CatchFishCount,
            target = 5,
            rewardMoney = 75
        });

        quests.Add(new QuestDefinition
        {
            id = "earn_200",
            titleTR = "200$ kazan",
            titleEN = "Earn $200",
            descTR = "Balık tutarak toplam 200$ kazan.",
            descEN = "Earn a total of $200 from fishing.",
            type = QuestType.EarnMoneyFromFishing,
            target = 200,
            rewardMoney = 100
        });

        quests.Add(new QuestDefinition
        {
            id = "catch_hard_fish",
            titleTR = "Zor balık yakala",
            titleEN = "Catch a hard fish",
            descTR = "Zorluk seviyesi en az 4 olan 1 balık yakala.",
            descEN = "Catch 1 fish with difficulty at least 4.",
            type = QuestType.CatchFishDifficultyAtLeast,
            target = 1,
            parameter = 4,
            rewardMoney = 150
        });

        quests.Add(new QuestDefinition
        {
            id = "buy_1_upgrade",
            titleTR = "1 geliştirme satın al",
            titleEN = "Buy 1 upgrade",
            descTR = "Market'ten 1 geliştirme satın al.",
            descEN = "Buy 1 upgrade from the shop.",
            type = QuestType.BuyUpgradesCount,
            target = 1,
            rewardMoney = 75
        });

        quests.Add(new QuestDefinition
        {
            id = "reach_depth_10",
            titleTR = "10m derinliğe in",
            titleEN = "Reach 10m depth",
            descTR = "Olta ile en az 10 metre derinliğe in.",
            descEN = "Reach at least 10 meters depth with your hook.",
            type = QuestType.ReachDepthMeters,
            target = 10,
            rewardMoney = 125
        });
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
        if (!populateDefaultQuestsInEditor) return;
        EnsureDefaultQuestsIfEmpty();
    }

    private void Update()
    {
        // Quests UI should only be visible during active gameplay.
        // If the player opens any overlay (pause/menu/shop) or the game is not active, force-close it.
        if (panel != null && panel.activeSelf)
        {
            bool inPlayableState = GameManager.instance != null && GameManager.instance.isGameActive && !GameManager.instance.isPaused;
            if (!inPlayableState)
                ForceClosePanel();
        }

        var kb = Keyboard.current;
        if (kb != null && kb.tabKey.wasPressedThisFrame)
        {
            DevLog.Info(LOG_CAT, "TAB pressed -> TogglePanel");
            TogglePanel();
        }

        TickPlaytimeQuests();
    }

    private void TickPlaytimeQuests()
    {
        if (GameManager.instance == null) return;
        if (!GameManager.instance.isGameActive) return;
        if (GameManager.instance.isPaused) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        bool changed = false;
        int activeCount = GetActiveQuestCount();
        for (int i = 0; i < activeCount; i++)
        {
            var q = quests[i];
            if (q == null) continue;
            if (q.type != QuestType.PlayTimeSeconds) continue;
            if (IsClaimed(q.id)) continue;

            float prev = GetProgress(q.id);
            float next = prev + dt;
            SetProgress(q.id, next);
            TryNotifyQuestCompleted(q, prev, next);
            changed = true;
        }

        if (changed) SaveState();
        if (panel != null && panel.activeSelf && changed) RefreshUI();
    }

    private bool CanTogglePanel()
    {
        // Allow closing anytime if it's already open.
        if (panel != null && panel.activeSelf) return true;

        // Only allow opening during active gameplay and not while paused (e.g. ESC menu).
        if (GameManager.instance == null) return false;
        if (!GameManager.instance.isGameActive) return false;
        if (GameManager.instance.isPaused) return false;
        return true;
    }

    private void TryNotifyQuestCompleted(QuestDefinition q, float prev, float next)
    {
        if (q == null) return;
        if (string.IsNullOrEmpty(q.id)) return;
        if (IsClaimed(q.id)) return;
        if (completedNotified.Contains(q.id)) return;

        int target = Mathf.Max(1, q.target);
        if (prev < target && next >= target)
        {
            completedNotified.Add(q.id);
            if (GameManager.instance != null)
            {
                string msg = LocalizationManager.Format("ui.quests.completedFmt", UI_QuestCompletedFmt(), q.GetTitle());
                GameManager.instance.ShowFeedback(msg, new Color(0.5f, 1f, 0.7f));
            }
        }
    }

    private void TogglePanel()
    {
        if (!CanTogglePanel())
        {
            DevLog.Info(LOG_CAT, "TogglePanel blocked (not in playable state)");
            return;
        }

        EnsureUI();

        bool show = !panel.activeSelf;
        panel.SetActive(show);

        if (backdrop != null)
            backdrop.SetActive(show);

        if (show)
            panel.transform.SetAsLastSibling();

        if (show && backdrop != null)
        {
            backdrop.transform.SetAsLastSibling();
            panel.transform.SetAsLastSibling();
        }

        if (GameManager.instance != null)
        {
            // Quest panel should not freeze the game; it is a HUD-style overlay.
            // Opening is still restricted to active gameplay via CanTogglePanel().
        }

        if (show)
            RefreshPanelChromeTexts();

        if (show)
            RefreshUI();

        DevLog.Info(LOG_CAT, $"Panel {(show ? "opened" : "closed")}");
    }

    public void ForceClosePanel()
    {
        if (panel != null) panel.SetActive(false);
        if (backdrop != null) backdrop.SetActive(false);

        DevLog.Info(LOG_CAT, "ForceClosePanel");
    }

    private void EnsureUI()
    {
        if (panel != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasObj = new GameObject("QuestCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Dim backdrop behind the panel
        backdrop = new GameObject("QuestBackdrop");
        backdrop.transform.SetParent(canvas.transform, false);
        var backdropRect = backdrop.AddComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;
        var backdropImg = backdrop.AddComponent<Image>();
        backdropImg.color = new Color(0f, 0f, 0f, 0.55f);
        backdropImg.raycastTarget = true;

        panel = new GameObject("QuestPanel");
        panel.transform.SetParent(canvas.transform, false);
        panel.transform.SetAsLastSibling();
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(620, 480);
        panelRect.anchoredPosition = Vector2.zero;

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.12f, 0.98f);

        var panelShadow = panel.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        panelShadow.effectDistance = new Vector2(0, -6);

        // Title
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(panel.transform, false);
        titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = UI_QuestsTitle();
        titleText.fontSize = 28;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;
        var titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -16);
        titleRect.sizeDelta = new Vector2(0, 40);

        // Divider
        GameObject dividerObj = new GameObject("Divider");
        dividerObj.transform.SetParent(panel.transform, false);
        var dividerRect = dividerObj.AddComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0, 1);
        dividerRect.anchorMax = new Vector2(1, 1);
        dividerRect.pivot = new Vector2(0.5f, 1);
        dividerRect.anchoredPosition = new Vector2(0, -60);
        dividerRect.sizeDelta = new Vector2(0, 2);
        var dividerImg = dividerObj.AddComponent<Image>();
        dividerImg.color = new Color(1f, 1f, 1f, 0.06f);

        // Scroll view
        GameObject scrollObj = new GameObject("Scroll");
        scrollObj.transform.SetParent(panel.transform, false);
        var scrollRect = scrollObj.AddComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0, 0);
        scrollRect.anchorMax = new Vector2(1, 1);
        scrollRect.offsetMin = new Vector2(18, 24);
        scrollRect.offsetMax = new Vector2(-18, -80);

        var scroll = scrollObj.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        var scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(1f, 1f, 1f, 0.04f);

        GameObject viewportObj = new GameObject("Viewport");
        viewportObj.transform.SetParent(scrollObj.transform, false);
        var viewportRect = viewportObj.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(10, 10);
        viewportRect.offsetMax = new Vector2(-10, -10);
        viewportObj.AddComponent<RectMask2D>();
        var viewportImg = viewportObj.AddComponent<Image>();
        viewportImg.color = Color.clear;
        viewportImg.raycastTarget = false;

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewportObj.transform, false);
        listRoot = contentObj.AddComponent<RectTransform>();
        listRoot.anchorMin = new Vector2(0, 1);
        listRoot.anchorMax = new Vector2(1, 1);
        listRoot.pivot = new Vector2(0.5f, 1);
        listRoot.anchoredPosition = Vector2.zero;
        listRoot.sizeDelta = new Vector2(0, 0);

        var vLayout = contentObj.AddComponent<VerticalLayoutGroup>();
        vLayout.childAlignment = TextAnchor.UpperCenter;
        vLayout.spacing = 12;
        vLayout.padding = new RectOffset(10, 10, 10, 10);
        vLayout.childControlHeight = true;
        vLayout.childForceExpandHeight = false;
        vLayout.childControlWidth = true;

        var fitter = contentObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRect;
        scroll.content = listRoot;

        // Wire up hierarchy
        scrollObj.GetComponent<ScrollRect>().viewport = viewportRect;
        scrollObj.GetComponent<ScrollRect>().content = listRoot;

        // Close hint
        GameObject hintObj = new GameObject("Hint");
        hintObj.transform.SetParent(panel.transform, false);
        hintText = hintObj.AddComponent<TextMeshProUGUI>();
        hintText.text = UI_PressTabToClose();
        hintText.fontSize = 14;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color = new Color(1f, 1f, 1f, 0.7f);
        var hintRect = hintText.rectTransform;
        hintRect.anchorMin = new Vector2(0, 0);
        hintRect.anchorMax = new Vector2(1, 0);
        hintRect.pivot = new Vector2(0.5f, 0);
        hintRect.anchoredPosition = new Vector2(0, 12);
        hintRect.sizeDelta = new Vector2(0, 22);

        if (GameManager.instance != null)
            GameManager.instance.ApplyFont(panel);

        backdrop.SetActive(false);
        panel.SetActive(false);

        DevLog.Info(LOG_CAT, "QuestPanel UI created");
    }

    private void ClearListUI()
    {
        if (listRoot == null) return;
        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);
    }

    private void RefreshUI()
    {
        if (panel == null || listRoot == null) return;

        RefreshPanelChromeTexts();

        ClearListUI();

        int activeCount = GetActiveQuestCount();

        if (quests == null || quests.Count == 0 || activeCount == 0)
        {
            GameObject empty = new GameObject("Empty");
            empty.transform.SetParent(listRoot, false);
            var emptyTxt = empty.AddComponent<TextMeshProUGUI>();
            emptyTxt.alignment = TextAlignmentOptions.Center;
            emptyTxt.fontSize = 16;
            emptyTxt.color = new Color(1f, 1f, 1f, 0.75f);
            emptyTxt.text = UI_NoQuests();

            var emptyLayout = empty.AddComponent<LayoutElement>();
            emptyLayout.minHeight = 40;
            emptyLayout.preferredHeight = 40;
            return;
        }
        for (int i = 0; i < activeCount; i++)
        {
            var q = quests[i];
            if (q == null) continue;

            bool claimedNow = IsClaimed(q.id);

            GameObject row = new GameObject("Quest_" + q.id);
            row.transform.SetParent(listRoot, false);
            var rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 86);

            // IMPORTANT: VerticalLayoutGroup needs an ILayoutElement for height.
            var rowLayoutEl = row.AddComponent<LayoutElement>();
            rowLayoutEl.minHeight = 86;
            rowLayoutEl.preferredHeight = 86;

            var rowBg = row.AddComponent<Image>();
            rowBg.color = claimedNow
                ? new Color(0.09f, 0.10f, 0.12f, 0.85f)
                : new Color(0.10f, 0.12f, 0.16f, 0.95f);

            var rowShadow = row.AddComponent<Shadow>();
            rowShadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            rowShadow.effectDistance = new Vector2(0, -2);

            var layout = row.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            // Title
            var titleObj = new GameObject("Title");
            titleObj.transform.SetParent(row.transform, false);
            var title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = q.GetTitle();
            title.fontSize = 18;
            title.fontStyle = FontStyles.Bold;
            title.color = claimedNow ? new Color(1f, 1f, 1f, 0.75f) : Color.white;

            // Desc
            string desc = q.GetDesc();
            if (!string.IsNullOrEmpty(desc))
            {
                var descObj = new GameObject("Desc");
                descObj.transform.SetParent(row.transform, false);
                var descTxt = descObj.AddComponent<TextMeshProUGUI>();
                descTxt.text = desc;
                descTxt.fontSize = 13;
                descTxt.color = claimedNow ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.78f);
            }

            // Footer row
            var footerObj = new GameObject("Footer");
            footerObj.transform.SetParent(row.transform, false);
            var footerLayout = footerObj.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 10;
            footerLayout.childAlignment = TextAnchor.MiddleLeft;
            footerLayout.childControlHeight = true;
            footerLayout.childControlWidth = true;
            footerLayout.childForceExpandWidth = false;
            footerLayout.childForceExpandHeight = false;

            float prog = GetProgress(q.id);
            int target = Mathf.Max(1, q.target);
            bool done = prog >= target;

            // Progress text
            var progObj = new GameObject("Progress");
            progObj.transform.SetParent(footerObj.transform, false);
            var progTxt = progObj.AddComponent<TextMeshProUGUI>();
            progTxt.fontSize = 14;
            progTxt.color = done ? new Color(0.5f, 1f, 0.7f) : new Color(1f, 1f, 1f, 0.9f);
            progTxt.text = FormatProgress(q, prog);

            var progLayout = progObj.AddComponent<LayoutElement>();
            progLayout.minWidth = 160;

            // Reward text
            var rewObj = new GameObject("Reward");
            rewObj.transform.SetParent(footerObj.transform, false);
            var rewTxt = rewObj.AddComponent<TextMeshProUGUI>();
            rewTxt.fontSize = 14;
            rewTxt.color = new Color(0.7f, 1f, 0.7f);
            rewTxt.text = $"+${q.rewardMoney}";

            var rewLayout = rewObj.AddComponent<LayoutElement>();
            rewLayout.minWidth = 90;

            // Claim button
            var btnObj = new GameObject("Claim");
            btnObj.transform.SetParent(footerObj.transform, false);
            var btnLayoutEl = btnObj.AddComponent<LayoutElement>();
            btnLayoutEl.minWidth = 140;
            btnLayoutEl.preferredWidth = 140;
            btnLayoutEl.minHeight = 28;
            btnLayoutEl.preferredHeight = 28;
            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = done ? new Color(0.15f, 0.5f, 0.2f) : new Color(0.25f, 0.25f, 0.25f);

            var btn = btnObj.AddComponent<Button>();
            btn.interactable = done && !claimedNow;

            Color baseCol = new Color(0.15f, 0.5f, 0.2f);
            Color hoverCol = new Color(0.18f, 0.62f, 0.25f);
            Color pressCol = new Color(0.12f, 0.40f, 0.18f);
            Color disabledCol = new Color(0.22f, 0.22f, 0.25f);
            var cb = btn.colors;
            cb.normalColor = btn.interactable ? baseCol : disabledCol;
            cb.highlightedColor = hoverCol;
            cb.pressedColor = pressCol;
            cb.selectedColor = hoverCol;
            cb.disabledColor = disabledCol;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(140, 28);

            var btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);
            var btnTxt = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnTxt.alignment = TextAlignmentOptions.Center;
            btnTxt.textWrappingMode = TextWrappingModes.NoWrap;
            btnTxt.overflowMode = TextOverflowModes.Ellipsis;
            btnTxt.enableAutoSizing = true;
            btnTxt.fontSizeMax = 14;
            btnTxt.fontSizeMin = 10;
            btnTxt.color = Color.white;
            btnTxt.text = claimedNow
                ? UI_Claimed()
                : UI_Claim();

            var btnTextRect = btnTxt.rectTransform;
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            string questId = q.id;
            btn.onClick.AddListener(() => Claim(questId));
        }
    }

    private string FormatProgress(QuestDefinition q, float prog)
    {
        int target = Mathf.Max(1, q.target);

        switch (q.type)
        {
            case QuestType.ReachDepthMeters:
                return $"{Mathf.FloorToInt(prog)} / {target} m";
            case QuestType.EarnMoneyFromFishing:
                return $"${Mathf.FloorToInt(prog)} / ${target}";
            case QuestType.PlayTimeSeconds:
                return $"{Mathf.FloorToInt(prog)} / {target} s";
            default:
                return $"{Mathf.FloorToInt(prog)} / {target}";
        }
    }

    public void ReportFishCaught(Fish fish, int payout)
    {
        bool changed = false;

        DevLog.Info(LOG_CAT, $"ReportFishCaught (fish={(fish != null ? fish.fishName : "null")}, payout={payout})");

        int activeCount = GetActiveQuestCount();
        for (int i = 0; i < activeCount; i++)
        {
            var q = quests[i];
            if (q == null) continue;
            if (IsClaimed(q.id)) continue;

            switch (q.type)
            {
                case QuestType.CatchFishCount:
                    {
                        float prev = GetProgress(q.id);
                        float next = prev + 1f;
                        SetProgress(q.id, next);
                        TryNotifyQuestCompleted(q, prev, next);
                    }
                    changed = true;
                    break;

                case QuestType.EarnMoneyFromFishing:
                    {
                        float prev = GetProgress(q.id);
                        float next = prev + Mathf.Max(0, payout);
                        SetProgress(q.id, next);
                        TryNotifyQuestCompleted(q, prev, next);
                    }
                    changed = true;
                    break;

                case QuestType.CatchFishDifficultyAtLeast:
                    if (fish != null && fish.difficulty >= Mathf.Max(1, q.parameter))
                    {
                        float prev = GetProgress(q.id);
                        float next = prev + 1f;
                        SetProgress(q.id, next);
                        TryNotifyQuestCompleted(q, prev, next);
                        changed = true;
                    }
                    break;
            }
        }

        if (changed) SaveState();
        if (panel != null && panel.activeSelf && changed) RefreshUI();
    }

    public void ReportUpgradeBought(UpgradeType type)
    {
        bool changed = false;

        DevLog.Info(LOG_CAT, $"ReportUpgradeBought (type={type})");

        int activeCount = GetActiveQuestCount();
        for (int i = 0; i < activeCount; i++)
        {
            var q = quests[i];
            if (q == null) continue;
            if (q.type != QuestType.BuyUpgradesCount) continue;
            if (IsClaimed(q.id)) continue;

            float prev = GetProgress(q.id);
            float next = prev + 1f;
            SetProgress(q.id, next);
            TryNotifyQuestCompleted(q, prev, next);
            changed = true;
        }

        if (changed) SaveState();
        if (panel != null && panel.activeSelf && changed) RefreshUI();
    }

    public void ReportDepth(float depthMeters)
    {
        if (depthMeters < 0f) return;

        DevLog.Info(LOG_CAT, $"ReportDepth (depthMeters={depthMeters:0.##})");

        bool changed = false;

        int activeCount = GetActiveQuestCount();
        for (int i = 0; i < activeCount; i++)
        {
            var q = quests[i];
            if (q == null) continue;
            if (q.type != QuestType.ReachDepthMeters) continue;
            if (IsClaimed(q.id)) continue;

            float cur = GetProgress(q.id);
            if (depthMeters > cur)
            {
                SetProgress(q.id, depthMeters);
                TryNotifyQuestCompleted(q, cur, depthMeters);
                changed = true;
            }
        }

        if (changed) SaveState();
        if (panel != null && panel.activeSelf && changed) RefreshUI();
    }

    private void Claim(string questId)
    {
        QuestDefinition q = quests.Find(x => x != null && x.id == questId);
        if (q == null) return;

        float prog = GetProgress(questId);
        if (prog < Mathf.Max(1, q.target)) return;
        if (IsClaimed(questId)) return;

        MarkClaimed(questId);

        // Görev akışı: ödül alındıkça yeni görevleri aç.
        if (sequentialUnlock)
        {
            int total = quests != null ? quests.Count : 0;
            int add = Mathf.Max(1, unlockOnClaim);
            unlockedCount = Mathf.Clamp(unlockedCount + add, 0, total);
        }

        SaveState();

        if (GameManager.instance != null)
            GameManager.instance.AddMoney(q.rewardMoney);

        DevLog.Info(LOG_CAT, $"Claimed quest '{questId}' -> +${q.rewardMoney} (unlockedCount={unlockedCount})");

        if (panel != null && panel.activeSelf)
            RefreshUI();
    }

    private float GetProgress(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return 0f;
        if (progressById.TryGetValue(questId, out float v)) return v;
        return 0f;
    }

    private void SetProgress(string questId, float v)
    {
        if (string.IsNullOrEmpty(questId)) return;
        progressById[questId] = Mathf.Max(0f, v);
    }

    private bool IsClaimed(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return false;
        return claimed.Contains(questId);
    }

    private void MarkClaimed(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        claimed.Add(questId);
    }

    private void LoadState()
    {
        progressById.Clear();
        claimed.Clear();

        int total = quests != null ? quests.Count : 0;
        int defaultUnlocked = Mathf.Clamp(initialUnlockedCount, 1, Mathf.Max(1, total));
        unlockedCount = PlayerPrefs.GetInt(PREF_UNLOCKED_COUNT, defaultUnlocked);
        unlockedCount = Mathf.Clamp(unlockedCount, 0, total);

        // If we have quests but unlock count ended up 0 (e.g. prefs from older version), recover.
        if (sequentialUnlock && total > 0 && unlockedCount < 1)
            unlockedCount = Mathf.Clamp(defaultUnlocked, 1, total);

        // Load known ids for cleanup; also refresh it with current quests.
        string ids = PlayerPrefs.GetString(PREF_IDS, string.Empty);
        if (!string.IsNullOrEmpty(ids))
        {
            string[] parts = ids.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                string id = parts[i];
                if (string.IsNullOrEmpty(id)) continue;

                float p = PlayerPrefs.GetFloat(PREF_PROGRESS_PREFIX + id, 0f);
                int c = PlayerPrefs.GetInt(PREF_CLAIMED_PREFIX + id, 0);
                if (p > 0f) progressById[id] = p;
                if (c == 1) claimed.Add(id);
            }
        }

        SaveState();

        DevLog.Info(LOG_CAT, $"LoadState (unlockedCount={unlockedCount}, progressKeys={progressById.Count}, claimed={claimed.Count})");
    }

    private void SaveState()
    {
        // Write ids list from current quest set.
        if (quests != null)
        {
            List<string> ids = new List<string>();
            for (int i = 0; i < quests.Count; i++)
            {
                var q = quests[i];
                if (q == null) continue;
                if (string.IsNullOrEmpty(q.id)) continue;
                ids.Add(q.id);
            }

            PlayerPrefs.SetString(PREF_IDS, string.Join("|", ids));

            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                PlayerPrefs.SetFloat(PREF_PROGRESS_PREFIX + id, GetProgress(id));
                PlayerPrefs.SetInt(PREF_CLAIMED_PREFIX + id, IsClaimed(id) ? 1 : 0);
            }
        }

        // Persist unlock state
        PlayerPrefs.SetInt(PREF_UNLOCKED_COUNT, unlockedCount);

        PlayerPrefs.Save();

        DevLog.Info(LOG_CAT, $"SaveState (unlockedCount={unlockedCount}, progressKeys={progressById.Count}, claimed={claimed.Count})");
    }

    public static void ResetAllQuestProgressPrefs()
    {
        string ids = PlayerPrefs.GetString(PREF_IDS, string.Empty);
        if (!string.IsNullOrEmpty(ids))
        {
            string[] parts = ids.Split('|');
            for (int i = 0; i < parts.Length; i++)
            {
                string id = parts[i];
                if (string.IsNullOrEmpty(id)) continue;
                PlayerPrefs.DeleteKey(PREF_PROGRESS_PREFIX + id);
                PlayerPrefs.DeleteKey(PREF_CLAIMED_PREFIX + id);
            }
        }

        PlayerPrefs.DeleteKey(PREF_IDS);
        PlayerPrefs.DeleteKey(PREF_UNLOCKED_COUNT);
        PlayerPrefs.Save();

        // QuestManager is DontDestroyOnLoad, so New Game can reset prefs while the in-memory state persists.
        // Force the runtime state to match the cleared prefs.
        if (instance != null)
        {
            instance.LoadState();
            instance.completedNotified.Clear();

            // If the panel is open, update it to show the reset state.
            if (instance.panel != null && instance.panel.activeSelf)
                instance.RefreshUI();
        }
    }
}
