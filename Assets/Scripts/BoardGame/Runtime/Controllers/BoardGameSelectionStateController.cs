using System;

namespace BoardGame.Runtime.Controllers
{
    /// <summary>
    /// 节点选择状态控制器
    /// 负责维护当前被鼠标选中的节点 ID
    /// </summary>
    public sealed class BoardGameSelectionStateController
    {
        private string _selectedNodeId = string.Empty;

        public event Action SelectionChanged;

        public string SelectedNodeId => _selectedNodeId;

        /// <summary>
        /// 更新当前被选中的节点
        /// </summary>
        public void SelectNode(string nodeId)
        {
            string nextNodeId = nodeId ?? string.Empty;

            if (_selectedNodeId == nextNodeId)
            {
                return;
            }

            _selectedNodeId = nextNodeId;
            SelectionChanged?.Invoke();
        }
    }
}
