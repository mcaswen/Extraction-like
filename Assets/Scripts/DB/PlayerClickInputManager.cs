using UnityEngine;
using Gameplay.Agent.Core;
using Gameplay.Agent.Runtime;
using Gameplay.Agent.Data;

/// <summary>
/// 负责捕获玩家鼠标点击群目标（圈），并向 Player 下发移动指令
/// </summary>
public class PlayerClickInputManager : MonoBehaviour
{
    [Header("检测层级设置")]
    [SerializeField] private LayerMask _clusterLayerMask; // 专门用于检测群目标的 Layer (如 TargetArea)
    [SerializeField] private float _maxRaycastDistance = 100f;

    [Header("当前控制的目标")]
    [SerializeField] private AgentPawnRoot _targetPlayerPawn;

    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;

        if (_targetPlayerPawn == null)
        {
            _targetPlayerPawn = Object.FindFirstObjectByType<AgentPawnRoot>();
        }
    }

    private void Update()
    {
        // 捕获鼠标左键点击
        if (Input.GetMouseButtonDown(0))
        {
            // 防御 UI 遮挡
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            HandleClusterClick();
        }
    }

    private void HandleClusterClick()
    {
        if (_targetPlayerPawn == null || _targetPlayerPawn.IsDead)
            return;

        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // 射线检测只对 TargetArea 层起效
        if (Physics.Raycast(ray, out hit, _maxRaycastDistance, _clusterLayerMask))
        {
            // 核心修复：自动识别你最开始写的那套正统 TargetCluster 脚本
            TargetCluster clickedCluster = hit.collider.GetComponent<TargetCluster>();

            // 确保点中的群目标还没有被全部摸完/消灭 (对齐你的已完成规则)
            if (clickedCluster != null && !clickedCluster.isCompleted)
            {
                // 获取群目标的中心点作为移动终点
                Vector3 clusterCenter = hit.collider.bounds.center;

                // 对齐原始脚本的命名，直接抓取物体名字
                Debug.Log($"[ClickInput] 成功点击群目标: {clickedCluster.gameObject.name}，下发移动指令前往中心点。");

                // 封装高级意图指令并打送给黑板
                string uniqueId = $"Cluster_{clickedCluster.gameObject.GetInstanceID()}";
                AgentTargetRef targetRef = AgentTargetRef.FromAbstractPoint(
                    AgentTargetKind.Location,
                    uniqueId,
                    clusterCenter
                );

                AgentDirectiveRequest movementDirective = new AgentDirectiveRequest(
                    AgentDirectiveType.MoveTo,
                    targetRef
                );

                _targetPlayerPawn.SubmitDirective(movementDirective);
            }
        }
    }
} // <--- 报错就是因为可能不小心把这个最后的括号删掉了