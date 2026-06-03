using BoardGame.Config;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BoardGame.Runtime
{
    /// <summary>
    /// 场景摆点标记
    /// 用于在编辑器场景中摆放节点位置并写回地图 SO
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardGameSceneNodeMarker : MonoBehaviour
    {
        [SerializeField] private string _nodeId;
        [SerializeField] private bool _isStartNode;

        public string NodeId => _nodeId;
        public bool IsStartNode => _isStartNode;

        /// <summary>
        /// 读取当前物体的 2D 世界坐标
        /// 当前运行时地图也使用世界坐标
        /// </summary>
        public Vector2 GetWorldPosition2D()
        {
            Vector3 position = transform.position;
            return new Vector2(position.x, position.y);
        }

        /// <summary>
        /// 导出给地图 SO 的场景快照
        /// </summary>
        public BoardMapSceneNodeImportEntry BuildImportEntry()
        {
            return new BoardMapSceneNodeImportEntry(_nodeId, GetWorldPosition2D(), _isStartNode);
        }

        [ContextMenu("Use Object Name As Node ID")]
        private void UseObjectNameAsNodeId()
        {
            _nodeId = gameObject.name;
        }

        private void Reset()
        {
            if (string.IsNullOrEmpty(_nodeId))
            {
                _nodeId = gameObject.name;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Color gizmoColor = _isStartNode
                ? new Color(0.98f, 0.84f, 0.22f, 1f)
                : new Color(0.24f, 0.82f, 0.94f, 1f);

            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(transform.position, 0.18f);

            if (!string.IsNullOrEmpty(_nodeId))
            {
                GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal =
                    {
                        textColor = gizmoColor
                    }
                };

                Handles.Label(transform.position + Vector3.up * 0.28f, _nodeId, labelStyle);
            }
        }
#endif
    }
}
