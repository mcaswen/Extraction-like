using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lightweight runtime SFX helper backed by Resources/GameAudio/SFX clips.
/// </summary>
public static class GameSfxPlayer
{
    private const string SfxResourceRoot = "GameAudio/SFX/";
    private const string BgmResourceRoot = "GameAudio/BGM/";
    private const float DefaultVolume = 0.85f;
    private static readonly Dictionary<string, AudioClip> ClipCache = new Dictionary<string, AudioClip>();
    private static AudioSource _uiSource;
    private static AudioSource _musicSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void PlayGameplayBgmOnSceneLoad()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(sceneName, "Scene_StartMenu", System.StringComparison.OrdinalIgnoreCase))
            return;

        PlayGameplayBgm();
    }

    public static void PlayGameplayBgm()
    {
        AudioClip clip = LoadBgmClip("gameplay_bgm");
        if (clip == null)
            return;

        AudioSource source = GetOrCreateMusicSource();
        if (source == null || source.clip == clip && source.isPlaying)
            return;

        source.clip = clip;
        source.loop = true;
        source.volume = 0.42f;
        source.spatialBlend = 0f;
        source.Play();
    }

    public static void PlayInventoryOpen()
    {
        Play2D("inventory_open_wav", 0.85f);
    }

    public static void PlayInventoryClose()
    {
        Play2D("inventory_close_wav", 0.85f);
    }

    public static void PlayUiClick()
    {
        Play2D("ui_click_wav", 0.65f);
    }

    public static void PlayPlayerFireAttack(Vector3 position)
    {
        PlayAt("player_fire_attack_wav", position, 0.75f);
    }

    public static void PlayIceSkill(Vector3 position)
    {
        PlayAt("skill_ice_wav", position, 0.9f);
    }

    public static void PlayEarthSkill(Vector3 position)
    {
        PlayAt("skill_earth_wav", position, 0.9f);
    }

    public static void PlayImpactSkill(Vector3 position)
    {
        PlayAt("skill_impact_wav", position, 0.85f);
    }

    public static void PlaySearchShort(Vector3 position)
    {
        PlayAt("search_short_wav", position, 0.75f);
    }

    public static void PlayAiBasicAttack(Vector3 position)
    {
        PlayAt("ai_basic_attack_wav", position, 0.8f);
    }

    public static void PlayAiDeath(Vector3 position)
    {
        PlayAt("ai_death_wav", position, 0.9f);
    }

    public static void PlayAiUpgrade(Vector3 position)
    {
        PlayAt("ai_upgrade", position, 0.9f);
    }

    public static void PlayAiExtract(Vector3 position)
    {
        PlayAt("ai_extract", position, 0.9f);
    }

    public static AudioClip LoadSfxClip(string clipName)
    {
        return LoadClip(SfxResourceRoot, clipName);
    }

    private static void Play2D(string clipName, float volume = DefaultVolume)
    {
        AudioClip clip = LoadSfxClip(clipName);
        if (clip == null)
            return;

        AudioSource source = GetOrCreateUiSource();
        if (source == null)
            return;

        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static void PlayAt(string clipName, Vector3 position, float volume = DefaultVolume)
    {
        AudioClip clip = LoadSfxClip(clipName);
        if (clip == null)
            return;

        AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volume));
    }

    private static AudioClip LoadBgmClip(string clipName)
    {
        return LoadClip(BgmResourceRoot, clipName);
    }

    private static AudioClip LoadClip(string resourceRoot, string clipName)
    {
        if (string.IsNullOrWhiteSpace(clipName))
            return null;

        string cacheKey = resourceRoot + clipName;
        if (ClipCache.TryGetValue(cacheKey, out AudioClip cachedClip))
            return cachedClip;

        AudioClip clip = Resources.Load<AudioClip>(cacheKey);
        if (clip == null)
        {
            Debug.LogWarning($"[GameSfxPlayer] Missing audio clip: {cacheKey}");
            return null;
        }

        ClipCache[cacheKey] = clip;
        return clip;
    }

    private static AudioSource GetOrCreateUiSource()
    {
        if (_uiSource != null)
            return _uiSource;

        GameObject sourceObject = new GameObject("GameSfxPlayer");
        Object.DontDestroyOnLoad(sourceObject);
        _uiSource = sourceObject.AddComponent<AudioSource>();
        _uiSource.playOnAwake = false;
        _uiSource.spatialBlend = 0f;
        return _uiSource;
    }

    private static AudioSource GetOrCreateMusicSource()
    {
        if (_musicSource != null)
            return _musicSource;

        GameObject sourceObject = new GameObject("GameBgmPlayer");
        Object.DontDestroyOnLoad(sourceObject);
        _musicSource = sourceObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.spatialBlend = 0f;
        return _musicSource;
    }
}
