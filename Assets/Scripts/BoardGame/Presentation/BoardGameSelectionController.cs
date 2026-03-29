using BoardGame.Runtime.Controllers;
using BoardGame.Views;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BoardGame.Presentation
{
    /// <summary>
    /// 鼠标悬停高亮、直接改写目标、空白拖拽相机与滚轮缩放输入控制器
    /// </summary>
    public sealed class BoardGameSelectionController : MonoBehaviour
    {
        // 空白处按下后，鼠标移动超过该像素阈值才开始拖拽相机
        [SerializeField] private float _cameraDragStartPixelThreshold = 8f;
        // 滑轮缩放步长
        [SerializeField] private float _mouseWheelZoomStep = 1.2f;
        // 正交相机最小尺寸
        [SerializeField] private float _minOrthographicSize = 3f;
        // 正交相机最大尺寸
        [SerializeField] private float _maxOrthographicSize = 18f;
        // 透视相机最小视野角
        [SerializeField] private float _minPerspectiveFieldOfView = 25f;
        // 透视相机最大视野角
        [SerializeField] private float _maxPerspectiveFieldOfView = 70f;

        private BoardGamePrototypeController _prototypeController;
        private Camera _worldCamera;
        private bool _isPointerDownOnEmptySpace;
        private bool _isDraggingCamera;
        private Vector3 _pointerDownScreenPosition;
        private Vector3 _cameraDragStartPosition;

        /// <summary>
        /// 绑定运行时总控与场景相机
        /// </summary>
        public void Bind(BoardGamePrototypeController prototypeController, Camera worldCamera)
        {
            _prototypeController = prototypeController;
            _worldCamera = worldCamera;
        }

        private void Update()
        {
            if (_prototypeController == null || _worldCamera == null)
            {
                return;
            }

            UpdateHoveredNode();

            if (Input.GetMouseButtonDown(0))
            {
                HandlePointerDown();
            }

            HandleMouseWheelZoom();

            if (_isPointerDownOnEmptySpace && Input.GetMouseButton(0))
            {
                HandlePointerHold();
            }

            if (_isPointerDownOnEmptySpace && Input.GetMouseButtonUp(0))
            {
                HandlePointerUp();
            }
        }

        /// <summary>
        /// 处理鼠标按下
        /// 点击节点时直接改写目标
        /// 点击 AI 时不触发任何模式切换
        /// 点击空白处时进入候选拖拽状态
        /// </summary>
        private void HandlePointerDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector3 worldPosition = GetMouseWorldPosition();
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);

            if (TryGetHoveredNode(hits, out BoardGameNodeView nodeView))
            {
                ResetCameraDragState();
                _prototypeController.TryRedirectToNode(nodeView.NodeId);
                return;
            }

            if (TryHitAgent(hits))
            {
                ResetCameraDragState();
                return;
            }

            _isPointerDownOnEmptySpace = true;
            _isDraggingCamera = false;
            _pointerDownScreenPosition = Input.mousePosition;
            _cameraDragStartPosition = _worldCamera.transform.position;
        }

        /// <summary>
        /// 处理空白处按住时的相机拖拽
        /// 拖拽开始后，相机会跟随鼠标反向平移
        /// </summary>
        private void HandlePointerHold()
        {
            if (!_isDraggingCamera)
            {
                float screenDistance = (Input.mousePosition - _pointerDownScreenPosition).magnitude;

                if (screenDistance < _cameraDragStartPixelThreshold)
                {
                    return;
                }

                _isDraggingCamera = true;
            }

            Vector3 screenDelta = Input.mousePosition - _pointerDownScreenPosition;
            Vector3 worldDelta = GetCameraDragWorldDelta(screenDelta);
            Vector3 nextCameraPosition = _cameraDragStartPosition + worldDelta;
            nextCameraPosition.z = _cameraDragStartPosition.z;
            _worldCamera.transform.position = nextCameraPosition;
        }

        /// <summary>
        /// 处理空白处抬起
        /// </summary>
        private void HandlePointerUp()
        {
            ResetCameraDragState();
        }

        private void ResetCameraDragState()
        {
            _isPointerDownOnEmptySpace = false;
            _isDraggingCamera = false;
        }

        /// <summary>
        /// 持续更新当前鼠标悬停的节点
        /// 用于地图上的蓝色悬停高亮
        /// </summary>
        private void UpdateHoveredNode()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
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

        /// <summary>
        /// 把鼠标屏幕坐标投到地图所在的世界平面
        /// 当前原型为 2D 地图，因此统一落到 z 等于 0 的平面
        /// </summary>
        private Vector3 GetMouseWorldPosition()
        {
            Vector3 screenPosition = Input.mousePosition;
            screenPosition.z = Mathf.Abs(_worldCamera.transform.position.z);
            Vector3 worldPosition = _worldCamera.ScreenToWorldPoint(screenPosition);
            worldPosition.z = 0f;
            return worldPosition;
        }

        /// <summary>
        /// 解析当前鼠标下的节点
        /// 若同时命中 AI 与节点，优先返回节点
        /// </summary>
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

        /// <summary>
        /// 检查当前鼠标下是否命中 AI
        /// 用于避免点击 AI 时启动空白拖拽
        /// </summary>
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

        /// <summary>
        /// 把屏幕位移换算为相机应平移的世界位移
        /// 当前原型使用 2D 相机，优先按正交相机换算，避免拖拽过程抖动
        /// 透视相机则退回到近似换算
        /// </summary>
        private Vector3 GetCameraDragWorldDelta(Vector3 screenDelta)
        {
            float worldUnitsPerPixelY;
            float worldUnitsPerPixelX;

            if (_worldCamera.orthographic)
            {
                worldUnitsPerPixelY = _worldCamera.orthographicSize * 2f / Mathf.Max(1, Screen.height);
                worldUnitsPerPixelX = worldUnitsPerPixelY * _worldCamera.aspect;
            }
            else
            {
                float distance = Mathf.Abs(_cameraDragStartPosition.z);
                float halfHeight = Mathf.Tan(_worldCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * distance;
                worldUnitsPerPixelY = halfHeight * 2f / Mathf.Max(1, Screen.height);
                worldUnitsPerPixelX = worldUnitsPerPixelY * _worldCamera.aspect;
            }

            return new Vector3(
                -screenDelta.x * worldUnitsPerPixelX,
                -screenDelta.y * worldUnitsPerPixelY,
                0f);
        }

        /// <summary>
        /// 处理鼠标滑轮缩放
        /// 默认以鼠标所在世界点为缩放锚点
        /// </summary>
        private void HandleMouseWheelZoom()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            float scrollDelta = Input.mouseScrollDelta.y;

            if (Mathf.Abs(scrollDelta) <= Mathf.Epsilon)
            {
                return;
            }

            Vector3 mouseWorldBeforeZoom = GetMouseWorldPosition();

            if (_worldCamera.orthographic)
            {
                float nextSize = _worldCamera.orthographicSize - scrollDelta * _mouseWheelZoomStep;
                _worldCamera.orthographicSize = Mathf.Clamp(nextSize, _minOrthographicSize, _maxOrthographicSize);
            }
            else
            {
                float nextFieldOfView = _worldCamera.fieldOfView - scrollDelta * _mouseWheelZoomStep * 3f;
                _worldCamera.fieldOfView = Mathf.Clamp(nextFieldOfView, _minPerspectiveFieldOfView, _maxPerspectiveFieldOfView);
            }

            Vector3 mouseWorldAfterZoom = GetMouseWorldPosition();
            Vector3 anchorDelta = mouseWorldBeforeZoom - mouseWorldAfterZoom;
            Vector3 nextCameraPosition = _worldCamera.transform.position + anchorDelta;
            nextCameraPosition.z = _worldCamera.transform.position.z;
            _worldCamera.transform.position = nextCameraPosition;
        }
    }
}
