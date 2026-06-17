using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在运行时把场景级支持对象绑定到动态生成的玩家或聚焦智能体
/// </summary>
public class SceneRuntimePlayerBinder : MonoBehaviour
{
    [Header("Scene Support")]
    public CameraFollowController CameraFollow;
    public RectTransform FloatingPromptUI;
    public Text FloatingPromptText;

    [Header("Fallback Search")]
    public bool PreferFocusedAgent = true;
    public bool AutoFindSceneSupport = true;
    public float RebindInterval = 0.5f;

    private float _rebindTimer;
    private Transform _cachedPlayerTransform;
    private PlayerInteraction _cachedPlayerInteraction;

    private void Start()
    {
        ResolveSceneSupport();
        if (FloatingPromptUI != null)
        {
            FloatingPromptUI.gameObject.SetActive(false);
        }
        TryBindPlayer();
    }

    private void Update()
    {
        _rebindTimer -= Time.unscaledDeltaTime;
        if (_rebindTimer > 0f)
        {
            return;
        }

        _rebindTimer = RebindInterval;

        if (AutoFindSceneSupport)
        {
            ResolveSceneSupport();
        }

        if (_cachedPlayerTransform == null || _cachedPlayerInteraction == null)
        {
            TryBindPlayer();
            return;
        }

        if (!ReferenceEquals(_cachedPlayerTransform, FindCurrentPlayerTransform()))
        {
            TryBindPlayer();
        }
    }

    private void ResolveSceneSupport()
    {
        if (CameraFollow == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                CameraFollow = mainCamera.GetComponent<CameraFollowController>();
            }

            if (CameraFollow == null)
            {
                CameraFollow = FindObjectOfType<CameraFollowController>();
            }
        }

        if (FloatingPromptUI == null)
        {
            GameObject floatingPromptObject = GameObject.Find("FloatingPrompt");
            if (floatingPromptObject != null)
            {
                FloatingPromptUI = floatingPromptObject.GetComponent<RectTransform>();
                floatingPromptObject.SetActive(false);
            }
        }

        if (FloatingPromptText == null && FloatingPromptUI != null)
        {
            FloatingPromptText = FloatingPromptUI.GetComponentInChildren<Text>(true);
        }
    }

    private void TryBindPlayer()
    {
        Transform playerTransform = FindCurrentPlayerTransform();
        if (playerTransform == null)
        {
            return;
        }

        // 动态生成的玩家可能缺少交互组件，这里统一补齐并接上场景界面
        PlayerInteraction interaction = playerTransform.GetComponent<PlayerInteraction>();
        if (interaction == null)
        {
            interaction = playerTransform.gameObject.AddComponent<PlayerInteraction>();
        }

        if (FloatingPromptUI != null)
        {
            interaction.FloatingPromptUI = FloatingPromptUI;
        }

        if (FloatingPromptText != null)
        {
            interaction.PromptText = FloatingPromptText;
        }

        if (CameraFollow != null)
        {
            CameraFollow.TargetTransform = playerTransform;
        }

        if (_cachedPlayerInteraction != null && _cachedPlayerInteraction != interaction)
        {
            _cachedPlayerInteraction.enabled = false;
        }

        interaction.enabled = true;
        _cachedPlayerTransform = playerTransform;
        _cachedPlayerInteraction = interaction;
    }

    private Transform FindCurrentPlayerTransform()
    {
        if (PreferFocusedAgent)
        {
            // 多智能体模式优先使用当前聚焦对象，让镜头和交互界面跟随玩家控制目标
            AgentRuntimeRegistry registry = AgentRuntimeRegistry.ActiveInstance;
            if (registry != null &&
                registry.TryGetFocusedHandle(out AgentRuntimeHandle focusedHandle) &&
                focusedHandle.CachedTransform != null)
            {
                return focusedHandle.CachedTransform;
            }
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
    }
}
