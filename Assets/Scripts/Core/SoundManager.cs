using UnityEngine;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager instance;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource; // Şablon olarak kullanılır

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
    private List<AudioSource> sfxPool = new List<AudioSource>();
    private GameObject sfxPoolParent;
    private const int INITIAL_POOL_SIZE = 5;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
        }
        else
        {
            Destroy(gameObject);
        }
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
        }

        // SFX Kaynağı (Şablon)
        if (sfxSource == null)
        {
            GameObject sfxObj = new GameObject("SFXSource_Template");
            sfxObj.transform.parent = transform;
            sfxSource = sfxObj.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        // Havuzu Başlat
        sfxPoolParent = new GameObject("SFX_Pool");
        sfxPoolParent.transform.parent = transform;

        for (int i = 0; i < INITIAL_POOL_SIZE; i++)
        {
            CreateNewSFXSource();
        }

        UpdateVolumes();
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
        return CreateNewSFXSource();
    }

    void Start()
    {
        if (backgroundMusic != null)
            PlayMusic(backgroundMusic);
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

        AudioSource source = GetAvailableSFXSource();
        
        // Pitch ve Volume ayarla
        source.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        source.volume = sfxVolume * volumeScale;
        
        source.clip = clip;
        source.Play();
    }

    public void PlayRandomSFX(AudioClip[] clips, float volumeScale = 1f, float pitchVariance = 0.1f)
    {
        if (clips == null || clips.Length == 0) return;
        int index = Random.Range(0, clips.Length);
        PlaySFX(clips[index], volumeScale, pitchVariance);
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

    private void UpdateVolumes()
    {
        if (musicSource != null)
        {
            musicSource.volume = isMuted ? 0f : musicVolume;
            musicSource.mute = isMuted;
        }

        // Çalan SFX'leri güncelle (Sadece mute durumu için, volume anlık değişmeyebilir)
        foreach (var source in sfxPool)
        {
            source.mute = isMuted;
        }
    }
}
