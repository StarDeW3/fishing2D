using System;
using UnityEngine;

public enum GameLanguage
{
    Turkish = 0,
    English = 1,
}

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager instance;

    private const string PREF_MUSIC_VOLUME = "Setting_MusicVolume";
    private const string PREF_SFX_VOLUME = "Setting_SfxVolume";
    private const string PREF_MUTED = "Setting_Muted";
    private const string PREF_SHAKE_INTENSITY = "Setting_ShakeIntensity";
    private const string PREF_SHOW_RARITY = "Setting_ShowRarityOnCatch";
    private const string PREF_LANGUAGE = "Setting_Language";

    public event Action SettingsChanged;

    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
    [SerializeField] private bool muted = false;

    [Range(0f, 1f)] [SerializeField] private float shakeIntensity = 1f;
    [SerializeField] private bool showRarityOnCatch = true;
    [SerializeField] private GameLanguage language = GameLanguage.Turkish;

    public float MusicVolume => musicVolume;
    public float SfxVolume => sfxVolume;
    public bool Muted => muted;
    public float ShakeIntensity => shakeIntensity;
    public bool ShowRarityOnCatch => showRarityOnCatch;
    public GameLanguage Language => language;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    public void Load()
    {
        musicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOLUME, musicVolume);
        sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOLUME, sfxVolume);
        muted = PlayerPrefs.GetInt(PREF_MUTED, muted ? 1 : 0) == 1;

        shakeIntensity = PlayerPrefs.GetFloat(PREF_SHAKE_INTENSITY, shakeIntensity);
        showRarityOnCatch = PlayerPrefs.GetInt(PREF_SHOW_RARITY, showRarityOnCatch ? 1 : 0) == 1;
        language = (GameLanguage)PlayerPrefs.GetInt(PREF_LANGUAGE, (int)language);

        ClampAll();
    }

    private void ClampAll()
    {
        musicVolume = Mathf.Clamp01(musicVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);
        shakeIntensity = Mathf.Clamp01(shakeIntensity);

        if (!Enum.IsDefined(typeof(GameLanguage), language))
            language = GameLanguage.Turkish;
    }

    private void Save()
    {
        PlayerPrefs.SetFloat(PREF_MUSIC_VOLUME, musicVolume);
        PlayerPrefs.SetFloat(PREF_SFX_VOLUME, sfxVolume);
        PlayerPrefs.SetInt(PREF_MUTED, muted ? 1 : 0);

        PlayerPrefs.SetFloat(PREF_SHAKE_INTENSITY, shakeIntensity);
        PlayerPrefs.SetInt(PREF_SHOW_RARITY, showRarityOnCatch ? 1 : 0);
        PlayerPrefs.SetInt(PREF_LANGUAGE, (int)language);

        PlayerPrefs.Save();
    }

    private void NotifyChanged()
    {
        SettingsChanged?.Invoke();
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        Save();
        NotifyChanged();
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        Save();
        NotifyChanged();
    }

    public void SetMuted(bool value)
    {
        muted = value;
        Save();
        NotifyChanged();
    }

    public void SetShakeIntensity(float value)
    {
        shakeIntensity = Mathf.Clamp01(value);
        Save();
        NotifyChanged();
    }

    public void SetShowRarityOnCatch(bool value)
    {
        showRarityOnCatch = value;
        Save();
        NotifyChanged();
    }

    public void SetLanguage(GameLanguage value)
    {
        language = value;
        if (!Enum.IsDefined(typeof(GameLanguage), language))
            language = GameLanguage.Turkish;

        Save();
        NotifyChanged();
    }

    public string L(string turkish, string english)
    {
        return language == GameLanguage.English ? english : turkish;
    }
}
