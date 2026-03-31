using BoardGame.Runtime.Controllers;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BoardGame.Presentation
{
    /// <summary>
    /// 地图输入总协调器
    /// 保留场景绑定入口，对内把节点输入和相机输入拆开协作
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
        private BoardGameNodeInputController _nodeInputController;
        private BoardGameCameraController _cameraController;

        private void Awake()
        {
            EnsureControllers();
        }

        /// <summary>
        /// 绑定运行时总控与场景相机
        /// </summary>
        public void Bind(BoardGamePrototypeController prototypeController, Camera worldCamera)
        {
            EnsureControllers();
            _prototypeController = prototypeController;
            _worldCamera = worldCamera;
            _nodeInputController.Bind(prototypeController, worldCamera);
            _cameraController.Bind(worldCamera);
        }

        private void Update()
        {
            if (_prototypeController == null || _worldCamera == null)
            {
                return;
            }

            bool isPointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool isInteractionLocked = _prototypeController.IsInteractionLocked;
            _nodeInputController.UpdateHoveredNode(isPointerOverUi || isInteractionLocked);

            if (isInteractionLocked)
            {
                _cameraController.ResetDragState();
                HandlePendingLevelUpInput();
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                HandlePointerDown(isPointerOverUi);
            }

            _cameraController.HandleMouseWheelZoom(isPointerOverUi);
            _cameraController.HandlePointerHold();
            _cameraController.HandlePointerUp();
        }

        private void HandlePendingLevelUpInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
            {
                _prototypeController.TryApplyLevelUpChoice(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
            {
                _prototypeController.TryApplyLevelUpChoice(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
            {
                _prototypeController.TryApplyLevelUpChoice(2);
            }
        }

        private void HandlePointerDown(bool isPointerOverUi)
        {
            if (isPointerOverUi)
            {
                _cameraController.ResetDragState();
                return;
            }

            if (_nodeInputController.TryHandlePointerDown())
            {
                _cameraController.ResetDragState();
                return;
            }

            _cameraController.BeginPotentialDrag();
        }

        private void EnsureControllers()
        {
            if (_nodeInputController == null)
            {
                _nodeInputController = new BoardGameNodeInputController();
            }

            if (_cameraController == null)
            {
                _cameraController = new BoardGameCameraController(
                    _cameraDragStartPixelThreshold,
                    _mouseWheelZoomStep,
                    _minOrthographicSize,
                    _maxOrthographicSize,
                    _minPerspectiveFieldOfView,
                    _maxPerspectiveFieldOfView);
            }
        }
    }
}
