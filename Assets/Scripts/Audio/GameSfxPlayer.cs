using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 运行时音效播放入口，统一从游戏音频资源目录加载并缓存音频资源
/// </summary>
public static class GameSfxPlayer
{
    private const string SfxResourceRoot = "GameAudio/SFX/";
    private const string BgmResourceRoot = "GameAudio/BGM/";
    private const float DefaultVolume = 0.85f;
    private static readonly Dictionary<string, AudioClip> ClipCache = new Dictionary<string, AudioClip>();
    private static AudioSource _uiSource;
    private static AudioSource _musicSource;

    // 场景加载后自动启动玩法背景音乐，但跳过开始菜单
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void PlayGameplayBgmOnSceneLoad()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (string.Equals(sceneName, "Scene_StartMenu", System.StringComparison.OrdinalIgnoreCase))
            return;

        PlayGameplayBgm();
    }

    /// <summary>
    /// 播放玩法场景循环背景音乐
    /// </summary>
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

    /// <summary>
    /// 播放背包打开音效
    /// </summary>
    public static void PlayInventoryOpen()
    {
        Play2D("inventory_open_wav", 0.85f);
    }

    /// <summary>
    /// 播放背包关闭音效
    /// </summary>
    public static void PlayInventoryClose()
    {
        Play2D("inventory_close_wav", 0.85f);
    }

    /// <summary>
    /// 播放通用界面点击音效
    /// </summary>
    public static void PlayUiClick()
    {
        Play2D("ui_click_wav", 0.65f);
    }

    /// <summary>
    /// 在指定位置播放玩家火焰攻击音效
    /// </summary>
    /// <param name="position">世界坐标播放位置</param>
    public static void PlayPlayerFireAttack(Vector3 position)
    {
        PlayAt("player_fire_attack_wav", position, 0.75f);
    }

    /// <summary>
    /// 在指定位置播放冰系技能音效
    /// </summary>
    /// <param name="position">世界坐标播放位置</param>
    public static void PlayIceSkill(Vector3 position)
    {
        PlayAt("skill_ice_wav", position, 0.9f);
    }

    /// <summary>
    /// 在指定位置播放土系技能音效
    /// </summary>
    /// <param name="position">世界坐标播放位置</param>
    public static void PlayEarthSkill(Vector3 position)
    {
        PlayAt("skill_earth_wav", position, 0.9f);
    }

    /// <summary>
    /// 在指定位置播放冲击技能音效
    /// </summary>
    /// <param name="position">世界坐标播放位置</param>
    public static void PlayImpactSkill(Vector3 position)
    {
        PlayAt("skill_impact_wav", position, 0.85f);
    }

    /// <summary>
    /// 在指定位置播放短搜索音效
    /// </summary>
    /// <param name="position">世界坐标播放位置</param>
    public static void PlaySearchShort(Vector3 position)
    {
        PlayAt("search_short_wav", position, 0.75f);
    }

    /// <summary>
    /// 在指定位置播放智能体普通攻击音效
    /// </summary>
    /// <param name="position">世界坐标播放位置</param>
    public static void PlayAiBasicAttack(Vector3 position)
    {
        PlayAt("ai_basic_attack_wav", position, 0.8f);
    }

    /// <summary>
    /// 在指定位置播放智能体死亡音效
    /// </summary>
    /// <param name="position">世界坐标播放位置</param>
    public static void PlayAiDeath(Vector3 position)
    {
        PlayAt("ai_death_wav", position, 0.9f);
    }

    /// <summary>
    /// 在指定位置播放智能体升级音效
    /// </summary>
    /// <param name="position">世界坐标播放位置</param>
    public static void PlayAiUpgrade(Vector3 position)
    {
        PlayAt("ai_upgrade", position, 0.9f);
    }

    /// <summary>
    /// 在指定位置播放智能体撤离音效
    /// </summary>
    /// <param name="position">世界坐标播放位置</param>
    public static void PlayAiExtract(Vector3 position)
    {
        PlayAt("ai_extract", position, 0.9f);
    }

    /// <summary>
    /// 加载并缓存指定名称的音效片段
    /// </summary>
    /// <param name="clipName">音效资源名</param>
    /// <returns>找到的音频片段，缺失时返回空值</returns>
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

        // 资源路径作为缓存键，避免音效和背景音乐同名时互相覆盖
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
