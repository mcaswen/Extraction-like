using BoardGame.Config;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoardGame.Runtime
{
    /// <summary>
    /// 场景 Agent 出生位标记
    /// 用于把 AgentId 对应的出生节点写回 roster
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardGameSceneAgentSpawnMarker : MonoBehaviour
    {
        [SerializeField] private string _agentId;
        [SerializeField] private string _nodeId;

        public string AgentId => _agentId;
        public string NodeId => ResolveNodeId();

        /// <summary>
        /// 导出给 roster 的场景出生位快照
        /// </summary>
        public BoardGameAgentSpawnImportEntry BuildImportEntry()
        {
            return new BoardGameAgentSpawnImportEntry(_agentId, ResolveNodeId());
        }

        [ContextMenu("Use Object Name As Agent ID")]
        private void UseObjectNameAsAgentId()
        {
            _agentId = gameObject.name;
        }

        [ContextMenu("Use Attached Node Marker Node ID")]
        private void UseAttachedNodeMarkerNodeId()
        {
            _nodeId = ResolveAttachedNodeMarkerId();
        }

        private void Reset()
        {
            if (string.IsNullOrEmpty(_agentId))
            {
                _agentId = gameObject.name;
            }

            if (string.IsNullOrEmpty(_nodeId))
            {
                _nodeId = ResolveAttachedNodeMarkerId();
            }
        }

        /// <summary>
        /// 解析当前出生位标记引用的节点 ID
        /// 若当前物体上已经挂了节点 marker，则优先复用它的 NodeId
        /// </summary>
        private string ResolveNodeId()
        {
            if (!string.IsNullOrEmpty(_nodeId))
            {
                return _nodeId;
            }

            return ResolveAttachedNodeMarkerId();
        }

        private string ResolveAttachedNodeMarkerId()
        {
            BoardGameSceneNodeMarker nodeMarker = GetComponent<BoardGameSceneNodeMarker>();
            return nodeMarker != null ? nodeMarker.NodeId : string.Empty;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color gizmoColor = new Color(0.98f, 0.5f, 0.18f, 1f);
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(transform.position, Vector3.one * 0.18f);

            string nodeId = ResolveNodeId();

            if (!string.IsNullOrEmpty(_agentId) || !string.IsNullOrEmpty(nodeId))
            {
                GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal =
                    {
                        textColor = gizmoColor
                    }
                };

                Handles.Label(
                    transform.position + Vector3.up * 0.34f,
                    $"{_agentId} -> {nodeId}",
                    labelStyle);
            }
        }
#endif
    }
}
