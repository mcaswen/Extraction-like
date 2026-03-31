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
        private BoardGamePrototypeController _prototypeController;
        private Camera _worldCamera;

        public void Bind(BoardGamePrototypeController prototypeController, Camera worldCamera)
        {
            _prototypeController = prototypeController;
            _worldCamera = worldCamera;
        }

        public void UpdateHoveredNode(bool isPointerOverUi)
        {
            if (_prototypeController == null || _worldCamera == null)
            {
                return;
            }

            if (isPointerOverUi)
            {
                _prototypeController.SelectNode(string.Empty);
                return;
            }

            Vector3 worldPosition = GetMouseWorldPosition();
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);

            if (TryGetHoveredNode(hits, out BoardGameNodeView nodeView))
            {
                _prototypeController.SelectNode(nodeView.NodeId);
                return;
            }

            _prototypeController.SelectNode(string.Empty);
        }

        public bool TryHandlePointerDown()
        {
            if (_prototypeController == null || _worldCamera == null)
            {
                return false;
            }

            Vector3 worldPosition = GetMouseWorldPosition();
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);

            if (TryGetHoveredNode(hits, out BoardGameNodeView nodeView))
            {
                _prototypeController.TryRedirectToNode(nodeView.NodeId);
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
