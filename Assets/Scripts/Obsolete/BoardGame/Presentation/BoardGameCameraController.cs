using UnityEngine;

namespace BoardGame.Presentation
{
    /// <summary>
    /// 地图相机输入控制
    /// 负责空白拖拽和滚轮缩放
    /// </summary>
    internal sealed class BoardGameCameraController
    {
        private readonly float _cameraDragStartPixelThreshold;
        private readonly float _mouseWheelZoomStep;
        private readonly float _minOrthographicSize;
        private readonly float _maxOrthographicSize;
        private readonly float _minPerspectiveFieldOfView;
        private readonly float _maxPerspectiveFieldOfView;

        private Camera _worldCamera;
        private bool _isPointerDownOnEmptySpace;
        private bool _isDraggingCamera;
        private Vector3 _pointerDownScreenPosition;
        private Vector3 _cameraDragStartPosition;

        /// <summary>
        /// 创建地图相机输入控制器
        /// </summary>
        /// <param name="cameraDragStartPixelThreshold"></param>
        /// <param name="mouseWheelZoomStep"></param>
        /// <param name="minOrthographicSize"></param>
        /// <param name="maxOrthographicSize"></param>
        /// <param name="minPerspectiveFieldOfView"></param>
        /// <param name="maxPerspectiveFieldOfView"></param>
        public BoardGameCameraController(
            float cameraDragStartPixelThreshold,
            float mouseWheelZoomStep,
            float minOrthographicSize,
            float maxOrthographicSize,
            float minPerspectiveFieldOfView,
            float maxPerspectiveFieldOfView)
        {
            _cameraDragStartPixelThreshold = cameraDragStartPixelThreshold;
            _mouseWheelZoomStep = mouseWheelZoomStep;
            _minOrthographicSize = minOrthographicSize;
            _maxOrthographicSize = maxOrthographicSize;
            _minPerspectiveFieldOfView = minPerspectiveFieldOfView;
            _maxPerspectiveFieldOfView = maxPerspectiveFieldOfView;
        }

        /// <summary>
        /// 绑定地图相机
        /// </summary>
        /// <param name="worldCamera"></param>
        public void Bind(Camera worldCamera)
        {
            _worldCamera = worldCamera;
        }

        /// <summary>
        /// 记录一次可能开始的空白拖拽
        /// </summary>
        public void BeginPotentialDrag()
        {
            if (_worldCamera == null)
            {
                return;
            }

            _isPointerDownOnEmptySpace = true;
            _isDraggingCamera = false;
            _pointerDownScreenPosition = Input.mousePosition;
            _cameraDragStartPosition = _worldCamera.transform.position;
        }

        /// <summary>
        /// 在按住鼠标时推进相机拖拽
        /// </summary>
        public void HandlePointerHold()
        {
            if (_worldCamera == null || !_isPointerDownOnEmptySpace || !Input.GetMouseButton(0))
            {
                return;
            }

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
        /// 处理鼠标抬起，收起拖拽状态
        /// </summary>
        public void HandlePointerUp()
        {
            if (_isPointerDownOnEmptySpace && Input.GetMouseButtonUp(0))
            {
                ResetDragState();
            }
        }

        /// <summary>
        /// 处理滚轮缩放，并尽量保持鼠标下方世界点不漂移
        /// </summary>
        /// <param name="isPointerOverUi"></param>
        public void HandleMouseWheelZoom(bool isPointerOverUi)
        {
            if (_worldCamera == null || isPointerOverUi)
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

        /// <summary>
        /// 重置相机拖拽状态
        /// </summary>
        public void ResetDragState()
        {
            _isPointerDownOnEmptySpace = false;
            _isDraggingCamera = false;
        }

        private Vector3 GetMouseWorldPosition()
        {
            Vector3 screenPosition = Input.mousePosition;
            screenPosition.z = Mathf.Abs(_worldCamera.transform.position.z);
            Vector3 worldPosition = _worldCamera.ScreenToWorldPoint(screenPosition);
            worldPosition.z = 0f;
            return worldPosition;
        }

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
    }
}
