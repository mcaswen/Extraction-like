using BoardGame.Runtime.Controllers;
using BoardGame.Views;
using UnityEngine;

namespace BoardGame.Presentation
{
    /// <summary>
    /// 地图节点相关输入控制
    /// 负责悬停高亮、点击节点改目标和点击 AI 的命中判定
    /// </summary>
    internal sealed class BoardGameNodeInputController
    {
        private BoardGameRuntimeQueryController _runtimeQueryController;
        private BoardGameSelectionStateController _selectionStateController;
        private BoardGameTargetRedirectController _targetRedirectController;
        private Camera _worldCamera;

        /// <summary>
        /// 绑定节点输入所需的模块控制器与世界相机
        /// </summary>
        public void Bind(
            BoardGameRuntimeQueryController runtimeQueryController,
            BoardGameSelectionStateController selectionStateController,
            BoardGameTargetRedirectController targetRedirectController,
            Camera worldCamera)
        {
            _runtimeQueryController = runtimeQueryController;
            _selectionStateController = selectionStateController;
            _targetRedirectController = targetRedirectController;
            _worldCamera = worldCamera;
        }

        /// <summary>
        /// 刷新鼠标当前悬停的节点高亮
        /// </summary>
        /// <param name="isPointerOverUi"></param>
        public void UpdateHoveredNode(bool isPointerOverUi)
        {
            if (_runtimeQueryController == null ||
                _selectionStateController == null ||
                _worldCamera == null)
            {
                return;
            }

            if (isPointerOverUi)
            {
                _selectionStateController.SelectNode(string.Empty);
                return;
            }

            Vector3 worldPosition = GetMouseWorldPosition();
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);

            if (TryGetHoveredNode(hits, out BoardGameNodeView nodeView))
            {
                _selectionStateController.SelectNode(nodeView.NodeId);
                return;
            }

            _selectionStateController.SelectNode(string.Empty);
        }

        /// <summary>
        /// 处理一次鼠标按下命中，优先尝试节点改写，其次尝试命中 AI 本体
        /// </summary>
        /// <returns></returns>
        public bool TryHandlePointerDown()
        {
            if (_targetRedirectController == null || _worldCamera == null)
            {
                return false;
            }

            Vector3 worldPosition = GetMouseWorldPosition();
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);

            if (TryGetHoveredNode(hits, out BoardGameNodeView nodeView))
            {
                _targetRedirectController.TryRedirectToNode(nodeView.NodeId);
                return true;
            }

            return TryHitAgent(hits);
        }

        private Vector3 GetMouseWorldPosition()
        {
            Vector3 screenPosition = Input.mousePosition;
            screenPosition.z = Mathf.Abs(_worldCamera.transform.position.z);
            Vector3 worldPosition = _worldCamera.ScreenToWorldPoint(screenPosition);
            worldPosition.z = 0f;
            return worldPosition;
        }

        private static bool TryGetHoveredNode(Collider2D[] hits, out BoardGameNodeView nodeView)
        {
            nodeView = null;

            if (hits == null)
            {
                return false;
            }

            foreach (Collider2D hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                nodeView = hit.GetComponentInParent<BoardGameNodeView>();

                if (nodeView != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryHitAgent(Collider2D[] hits)
        {
            if (hits == null)
            {
                return false;
            }

            foreach (Collider2D hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                if (hit.GetComponentInParent<BoardGameAgentView>() != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
