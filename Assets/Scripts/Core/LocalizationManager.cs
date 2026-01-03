using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LocalizationEntry
{
    public string key;
    public string value;
}

[Serializable]
public class LocalizationFile
{
    public LocalizationEntry[] entries;
}

public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager instance;

    public event Action LanguageChanged;

    private readonly Dictionary<string, string> table = new Dictionary<string, string>(StringComparer.Ordinal);
    private GameLanguage currentLanguage = GameLanguage.Turkish;

    private bool isSubscribedToSettings = false;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Prefer SettingsManager language if available.
        if (SettingsManager.instance != null)
            currentLanguage = SettingsManager.instance.Language;

        ReloadTable();
    }

    private void OnEnable()
    {
        TrySubscribeToSettings();
    }

    private void Start()
    {
        TrySubscribeToSettings();

        // If SettingsManager came up after Awake, resync.
        if (SettingsManager.instance != null && SettingsManager.instance.Language != currentLanguage)
        {
            currentLanguage = SettingsManager.instance.Language;
            ReloadTable();
            LanguageChanged?.Invoke();
        }
    }

    private void OnDisable()
    {
        TryUnsubscribeFromSettings();
    }

    private void TrySubscribeToSettings()
    {
        if (isSubscribedToSettings) return;
        if (SettingsManager.instance == null) return;

        SettingsManager.instance.SettingsChanged -= OnSettingsChanged;
        SettingsManager.instance.SettingsChanged += OnSettingsChanged;
        isSubscribedToSettings = true;
    }

    private void TryUnsubscribeFromSettings()
    {
        if (!isSubscribedToSettings) return;
        if (SettingsManager.instance != null)
            SettingsManager.instance.SettingsChanged -= OnSettingsChanged;
        isSubscribedToSettings = false;
    }

    private void OnSettingsChanged()
    {
        if (SettingsManager.instance == null) return;

        var next = SettingsManager.instance.Language;
        if (next == currentLanguage) return;

        currentLanguage = next;
        ReloadTable();
        LanguageChanged?.Invoke();
    }

    private string GetResourceNameForLanguage(GameLanguage language)
    {
        return language == GameLanguage.English ? "Localization/en" : "Localization/tr";
    }

    private void ReloadTable()
    {
        table.Clear();

        string resourceName = GetResourceNameForLanguage(currentLanguage);
        TextAsset textAsset = Resources.Load<TextAsset>(resourceName);

        if (textAsset == null)
        {
            Debug.LogWarning($"LocalizationManager: Missing resource '{resourceName}'.");
            return;
        }

        LocalizationFile file;
        try
        {
            file = JsonUtility.FromJson<LocalizationFile>(textAsset.text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"LocalizationManager: Failed to parse '{resourceName}': {ex.Message}");
            return;
        }

        if (file?.entries == null) return;

        foreach (var e in file.entries)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.key)) continue;
            table[e.key] = e.value ?? string.Empty;
        }
    }

    public static string T(string key, string fallback = null)
    {
        if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;

        if (instance == null)
            return fallback ?? key;

        if (instance.table.TryGetValue(key, out string value))
            return value;

        return fallback ?? key;
    }

    public static string Format(string key, string fallbackFormat, params object[] args)
    {
        string format = T(key, fallbackFormat);
        try
        {
            return string.Format(format, args);
        }
        catch
        {
            // If placeholders mismatch, fall back gracefully.
            return format;
        }
    }
}
