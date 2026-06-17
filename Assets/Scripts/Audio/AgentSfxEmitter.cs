using UnityEngine;

/// <summary>
/// 挂在智能体身上的空间音效发射器，负责用本地音源播放动作反馈
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class AgentSfxEmitter : MonoBehaviour
{
    [SerializeField] private AudioSource _audioSource;
    [SerializeField, Range(0f, 1f)] private float _volume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float _spatialBlend = 0.25f;
    [SerializeField, Min(1f)] private float _maxDistance = 45f;

    private void Awake()
    {
        ConfigureSource();
    }

    private void Reset()
    {
        ConfigureSource();
    }

    private void OnValidate()
    {
        ConfigureSource();
    }

    /// <summary>
    /// 播放基础攻击音效
    /// </summary>
    public void PlayBasicAttack()
    {
        Play("ai_basic_attack_wav", 0.8f);
    }

    /// <summary>
    /// 播放死亡音效
    /// </summary>
    public void PlayDeath()
    {
        Play("ai_death_wav", 0.95f);
    }

    /// <summary>
    /// 播放升级音效
    /// </summary>
    public void PlayUpgrade()
    {
        Play("ai_upgrade", 0.95f);
    }

    /// <summary>
    /// 播放撤离音效
    /// </summary>
    public void PlayExtract()
    {
        Play("ai_extract", 0.95f);
    }

    /// <summary>
    /// 播放搜索音效
    /// </summary>
    public void PlaySearch()
    {
        Play("search_short_wav", 0.8f);
    }

    private void Play(string clipName, float volumeScale)
    {
        AudioClip clip = GameSfxPlayer.LoadSfxClip(clipName);
        if (clip == null)
            return;

        ConfigureSource();
        if (_audioSource != null)
            _audioSource.PlayOneShot(clip, Mathf.Clamp01(_volume * volumeScale));
    }

    private void ConfigureSource()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        // 运行时兜底补齐音源组件，编辑器下交给依赖特性和重置流程维护
        if (_audioSource == null && Application.isPlaying)
            _audioSource = gameObject.AddComponent<AudioSource>();

        if (_audioSource == null)
            return;

        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = _spatialBlend;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.minDistance = 2f;
        _audioSource.maxDistance = Mathf.Max(1f, _maxDistance);
    }
}
