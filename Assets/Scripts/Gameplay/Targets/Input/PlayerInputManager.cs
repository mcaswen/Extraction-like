using Gameplay.Agent.Data;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

/// <summary>
/// Thin mouse input entry point for issuing commands to agents by clicking visible target clusters.
/// </summary>
public sealed class PlayerInputManager : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera _targetCamera;

    [Header("Target Filtering")]
    [FormerlySerializedAs("targetLayerMask")]
    [SerializeField] private LayerMask _clusterLayerMask = ~0;
    [SerializeField] private bool _useClusterLayerMask;
    [SerializeField] private bool _ignoreCompletedClusters = true;

    [Header("Agent")]
    [SerializeField] private string _targetAgentId;

    [Header("Debug")]
    [SerializeField] private bool _logClicks;

    private readonly VisibleTargetClusterPicker _clusterPicker =
        new VisibleTargetClusterPicker();

    private readonly AgentTargetCommandDispatcher _commandDispatcher =
        new AgentTargetCommandDispatcher();

    private Camera TargetCamera
    {
        get
        {
            if (_targetCamera == null)
                _targetCamera = Camera.main;

            return _targetCamera;
        }
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        if (IsPointerBlockedByUi())
            return;

        TryHandleClick(Input.mousePosition);
    }

    private void TryHandleClick(Vector2 screenPosition)
    {
        Camera camera = TargetCamera;
        if (camera == null)
            return;

        TargetClusterPickOptions pickOptions = new TargetClusterPickOptions(
            _clusterLayerMask,
            _useClusterLayerMask,
            _ignoreCompletedClusters);

        if (!_clusterPicker.TryPick(
                camera,
                screenPosition,
                pickOptions,
                out GameplayTargetClusterAuthoringBase cluster))
        {
            return;
        }

        if (!_commandDispatcher.TrySubmitClusterCommand(
                cluster,
                _targetAgentId,
                out AgentDirectiveRequest directiveRequest))
        {
            return;
        }

        if (_logClicks)
            Debug.Log($"[TargetInput] Clicked {cluster.TargetKind} cluster [{cluster.name}] -> {directiveRequest.DirectiveType}", cluster);
    }

    private static bool IsPointerBlockedByUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
