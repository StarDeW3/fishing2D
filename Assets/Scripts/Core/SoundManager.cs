using UnityEngine;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource; // Şablon olarak kullanılır

    [Header("Loop SFX Sources")]
    public AudioSource reelLoopSource;
    public AudioSource engineLoopSource;

    [Header("Clips")]
    public AudioClip backgroundMusic;
    public AudioClip castSound;
    public AudioClip splashSound;
    public AudioClip reelSound;
    public AudioClip catchSound;
    public AudioClip escapeSound;
    public AudioClip boatEngineSound;

    [Header("Settings")]
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public bool isMuted = false;

    // SFX Havuzu (Aynı anda birden fazla ses ve farklı pitch değerleri için)
    private readonly List<AudioSource> sfxPool = new List<AudioSource>();
    private GameObject sfxPoolParent;
    private const int INITIAL_POOL_SIZE = 5;

    private float reelLoopVolumeScale = 0.7f;
    private float engineLoopVolumeScale = 0.7f;

    // Spike anlarında havuz büyüyebilir; uzun vadede sahneyi şişirmemek için idle kaynakları trim et.
    private const int MAX_POOL_SIZE = 32;
    private const float POOL_TRIM_INTERVAL = 10f;
    private Coroutine poolTrimRoutine;

    private bool isSubscribedToSettings = false;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();

            DevLog.Info("Audio", "SoundManager.Awake initialized audio sources");

            // Eğer SettingsManager halihazırda varsa ilk değerleri uygula.
            if (SettingsManager.instance != null)
            {
                ApplyFromSettings(SettingsManager.instance);
            }
        }
        else
        {
            DevLog.Warn("Audio", "Duplicate SoundManager destroyed");
            Destroy(gameObject);
        }
    }

    private void AutoAssignClipsFromResourcesIfMissing()
    {
        // Only fills missing fields; never overwrites inspector assignments.
        // Files are currently under Assets/Resources/ (no subfolder), so the load path is just the filename.

        if (backgroundMusic == null)
            backgroundMusic = Resources.Load<AudioClip>("lick_the_chorus");

        if (castSound == null)
            castSound = Resources.Load<AudioClip>("throw");

        if (splashSound == null)
            splashSound = Resources.Load<AudioClip>("splash");

        if (reelSound == null)
            reelSound = Resources.Load<AudioClip>("reeling");

        if (catchSound == null)
            catchSound = Resources.Load<AudioClip>("catch");

        if (escapeSound == null)
            escapeSound = Resources.Load<AudioClip>("escape");

        if (boatEngineSound == null)
            boatEngineSound = Resources.Load<AudioClip>("engine");
    }

    private void LogMissingClip(string fieldName, string resourcesPath)
    {
        Debug.LogWarning($"SoundManager: Missing clip for '{fieldName}'. Expected Resources path: '{resourcesPath}'.");
    }

    private void ValidateClipAssignments()
    {
        // Keep this noisy output out of release builds.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (backgroundMusic == null) LogMissingClip(nameof(backgroundMusic), "lick_the_chorus");
        if (castSound == null) LogMissingClip(nameof(castSound), "throw");
        if (splashSound == null) LogMissingClip(nameof(splashSound), "splash");
        if (reelSound == null) LogMissingClip(nameof(reelSound), "reeling");
        if (catchSound == null) LogMissingClip(nameof(catchSound), "catch");
        if (escapeSound == null) LogMissingClip(nameof(escapeSound), "escape");
        if (boatEngineSound == null) LogMissingClip(nameof(boatEngineSound), "engine");
#endif
    }

    private void OnEnable()
    {
        TrySubscribeToSettings();
    }

    private void OnDisable()
    {
        TryUnsubscribeFromSettings();
    }

    void InitializeAudioSources()
    {
        // Müzik Kaynağı
        if (musicSource == null)
        {
            GameObject musicObj = new GameObject("MusicSource");
            musicObj.transform.parent = transform;
            musicSource = musicObj.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.spatialBlend = 0f;
        }

        // SFX Kaynağı (Şablon)
        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFXSource_Template");
            sfxObj.transform.parent = transform;
            sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;
        }

        // Loop SFX kaynakları
        if (reelLoopSource == null)
        {
            GameObject reelObj = new GameObject("SFX_ReelLoop");
            reelObj.transform.parent = transform;
            reelLoopSource = reelObj.AddComponent<AudioSource>();
            reelLoopSource.loop = true;
            reelLoopSource.playOnAwake = false;
            reelLoopSource.spatialBlend = 0f;
        }

        if (engineLoopSource == null)
        {
            GameObject engineObj = new GameObject("SFX_EngineLoop");
            engineObj.transform.parent = transform;
            engineLoopSource = engineObj.AddComponent<AudioSource>();
            engineLoopSource.loop = true;
            engineLoopSource.playOnAwake = false;
            engineLoopSource.spatialBlend = 0f;
        }

        // Havuzu Başlat
        sfxPoolParent = new GameObject("SFX_Pool");
        sfxPoolParent.transform.parent = transform;

        for (int i = 0; i < INITIAL_POOL_SIZE; i++)
        {
            CreateNewSFXSource();
        }

        UpdateVolumes();

        if (poolTrimRoutine != null)
        {
            StopCoroutine(poolTrimRoutine);
            poolTrimRoutine = null;
        }

        poolTrimRoutine = StartCoroutine(TrimPoolRoutine());
    }

    AudioSource CreateNewSFXSource()
    {
        GameObject obj = new GameObject($"SFX_Source_{sfxPool.Count}");
        obj.transform.parent = sfxPoolParent.transform;
        AudioSource newSource = obj.AddComponent<AudioSource>();

        // Şablondan ayarları kopyala
        newSource.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
        newSource.spatialBlend = sfxSource.spatialBlend;
        newSource.playOnAwake = false;
        newSource.mute = isMuted;

        sfxPool.Add(newSource);
        return newSource;
    }

    AudioSource GetAvailableSFXSource()
    {
        foreach (var source in sfxPool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }
        if (sfxPool.Count < MAX_POOL_SIZE)
            return CreateNewSFXSource();

        // Havuz sınırına geldiysek sesi düşürmek yerine sessizce vazgeç.
        return null;
    }

    void Start()
    {
        // GameManager SettingsManager'ı sonra oluşturabilir; Start'ta bir daha dene.
        TrySubscribeToSettings();
        if (SettingsManager.instance != null)
            ApplyFromSettings(SettingsManager.instance);

        AutoAssignClipsFromResourcesIfMissing();
        ValidateClipAssignments();

        DevLog.Info(
            "Audio",
            $"Clips assigned: music={(backgroundMusic != null)} cast={(castSound != null)} splash={(splashSound != null)} reeling={(reelSound != null)} catch={(catchSound != null)} escape={(escapeSound != null)} engine={(boatEngineSound != null)}"
        );

        if (backgroundMusic != null)
            PlayMusic(backgroundMusic);
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
        ApplyFromSettings(SettingsManager.instance);
    }

    private void ApplyFromSettings(SettingsManager settings)
    {
        if (settings == null) return;

        musicVolume = settings.MusicVolume;
        sfxVolume = settings.SfxVolume;
        isMuted = settings.Muted;
        UpdateVolumes();
    }

    public void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.volume = isMuted ? 0f : musicVolume;
        musicSource.Play();
    }

    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        // Varsayılan olarak pitch değişimi yok
        PlaySFX(clip, volumeScale, 0f);
    }

    // Pitch varyasyonu eklenmiş overload
    public void PlaySFX(AudioClip clip, float volumeScale, float pitchVariance)
    {
        if (clip == null || isMuted) return;

        pitchVariance = Mathf.Abs(pitchVariance);

        AudioSource source = GetAvailableSFXSource();
        if (source == null) return;

        // Pitch ve Volume ayarla
        source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        source.volume = sfxVolume * volumeScale;

        source.clip = clip;
        source.Play();
    }

    private System.Collections.IEnumerator TrimPoolRoutine()
    {
        var wait = new WaitForSeconds(POOL_TRIM_INTERVAL);
        while (true)
        {
            yield return wait;

            // Initial size'ın altına inmeyelim; sadece boşta olan ekstra kaynakları temizleyelim.
            for (int i = sfxPool.Count - 1; i >= INITIAL_POOL_SIZE; i--)
            {
                var source = sfxPool[i];
                if (source == null)
                {
                    sfxPool.RemoveAt(i);
                    continue;
                }

                if (!source.isPlaying)
                {
                    sfxPool.RemoveAt(i);
                    Destroy(source.gameObject);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (poolTrimRoutine != null)
        {
            StopCoroutine(poolTrimRoutine);
            poolTrimRoutine = null;
        }
    }

    public void PlayRandomSFX(AudioClip[] clips, float volumeScale = 1f, float pitchVariance = 0.1f)
    {
        if (clips == null || clips.Length == 0) return;
        int index = Random.Range(0, clips.Length);
        PlaySFX(clips[index], volumeScale, pitchVariance);
    }

    public void SetReelLoop(bool playing, float volumeScale = 0.7f, float pitch = 1f)
    {
        if (reelLoopSource == null)
            return;

        reelLoopVolumeScale = Mathf.Clamp01(volumeScale);

        // FishingMiniGame may request the loop before Start() runs; ensure clips are available.
        if (playing && reelSound == null)
            AutoAssignClipsFromResourcesIfMissing();

        if (playing && reelSound == null)
            DevLog.Warn("Audio", "ReelLoop requested but reelSound is missing (Resources: 'reeling')");

        if (!playing || isMuted || reelSound == null)
        {
            if (reelLoopSource.isPlaying)
            {
                DevLog.Info("Audio", "ReelLoop STOP");
                reelLoopSource.Stop();
            }
            reelLoopSource.clip = reelSound;
            UpdateVolumes();
            return;
        }

        reelLoopSource.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        if (reelLoopSource.clip != reelSound)
            reelLoopSource.clip = reelSound;

        UpdateVolumes();
        if (!reelLoopSource.isPlaying)
        {
            DevLog.Info("Audio", $"ReelLoop START volScale={reelLoopVolumeScale:0.00} pitch={reelLoopSource.pitch:0.00}");
            reelLoopSource.Play();
        }
    }

    public void SetBoatEngineLoop(bool playing, float volumeScale = 0.7f, float pitch = 1f)
    {
        if (engineLoopSource == null)
            return;

        engineLoopVolumeScale = Mathf.Clamp01(volumeScale);

        // BoatController may request the loop before Start() runs; ensure clips are available.
        if (playing && boatEngineSound == null)
            AutoAssignClipsFromResourcesIfMissing();

        if (playing && boatEngineSound == null)
            DevLog.Warn("Audio", "EngineLoop requested but boatEngineSound is missing (Resources: 'engine')");

        if (!playing || isMuted || boatEngineSound == null)
        {
            if (engineLoopSource.isPlaying)
            {
                DevLog.Info("Audio", "EngineLoop STOP");
                engineLoopSource.Stop();
            }
            engineLoopSource.clip = boatEngineSound;
            UpdateVolumes();
            return;
        }

        engineLoopSource.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        if (engineLoopSource.clip != boatEngineSound)
            engineLoopSource.clip = boatEngineSound;

        UpdateVolumes();
        if (!engineLoopSource.isPlaying)
        {
            DevLog.Info("Audio", $"EngineLoop START volScale={engineLoopVolumeScale:0.00} pitch={engineLoopSource.pitch:0.00}");
            engineLoopSource.Play();
        }
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateVolumes();
    }

    public void ToggleMute()
    {
        isMuted = !isMuted;
        UpdateVolumes();
    }

    public void SetMuted(bool muted)
    {
        isMuted = muted;
        UpdateVolumes();
    }

    private void UpdateVolumes()
    {
        if (musicSource != null)
        {
            musicSource.volume = isMuted ? 0f : musicVolume;
            musicSource.mute = isMuted;
        }

        if (reelLoopSource != null)
        {
            reelLoopSource.mute = isMuted;
            reelLoopSource.volume = isMuted ? 0f : (sfxVolume * reelLoopVolumeScale);
        }

        if (engineLoopSource != null)
        {
            engineLoopSource.mute = isMuted;
            engineLoopSource.volume = isMuted ? 0f : (sfxVolume * engineLoopVolumeScale);
        }

        // Çalan SFX'leri güncelle (Sadece mute durumu için, volume anlık değişmeyebilir)
        foreach (var source in sfxPool)
        {
            source.mute = isMuted;
        }
    }
}
