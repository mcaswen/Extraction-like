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
        [SerializeField] private float _cameraDragStartPixelThreshold = 8f;
        [SerializeField] private float _mouseWheelZoomStep = 1.2f;
        [SerializeField] private float _minOrthographicSize = 3f;
        [SerializeField] private float _maxOrthographicSize = 18f;
        [SerializeField] private float _minPerspectiveFieldOfView = 25f;
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

        /// <summary>
        /// 协调地图输入主循环
        /// 先处理悬停与交互锁定，再根据当前状态决定走升级输入、节点点击还是相机拖拽
        /// </summary>
        private void Update()
        {
            if (_prototypeController == null || _worldCamera == null)
            {
                return;
            }

            bool isPointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool isInteractionLocked = _prototypeController.IsInteractionLocked;

            // 悬停高亮始终先刷新，这样即使后面因为交互锁定提前 return，地图表现也还是最新的
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

        /// <summary>
        /// 在升级等待态下处理 1/2/3 快捷选择
        /// </summary>
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

        /// <summary>
        /// 处理鼠标按下
        /// 若没有命中可交互目标，则把这次按下视为一次可能的相机拖拽起点
        /// </summary>
        /// <param name="isPointerOverUi"></param>
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

        /// <summary>
        /// 懒创建输入子控制器，保持 MonoBehaviour 只负责场景绑定和参数序列化
        /// </summary>
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
