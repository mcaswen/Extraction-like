using System;
using System.Collections.Generic;
using Core.BehaviorTree.Nodes;

namespace Core.BehaviorTree.Runtime
{
    /// <summary>
    /// 行为树类定义，包含树的名称和根节点
    /// </summary>
    public sealed class BehaviorTree
    {
        private readonly List<BehaviorNode> _nodes = new List<BehaviorNode>();

        public string Name { get; }
        public BehaviorNode RootNode { get; }

        /// <summary>按深度优先顺序排列的全部节点，序号即节点的 Index</summary>
        public IReadOnlyList<BehaviorNode> Nodes => _nodes;

        public BehaviorTree(string name, BehaviorNode rootNode)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "BehaviorTree" : name;
            RootNode = rootNode ?? throw new ArgumentNullException(nameof(rootNode));
            Register(RootNode);
        }

        private void Register(BehaviorNode node)
        {
            // 节点持有执行状态，同一实例不能挂在两棵树或同一棵树的两个位置
            if (node.Tree != null)
                throw new InvalidOperationException($"Behavior node [{node.NodeName}] already belongs to tree [{node.Tree.Name}].");

            node.Tree = this;
            node.Index = _nodes.Count;
            _nodes.Add(node);
            for (int index = 0; index < node.ChildCount; index++)
                Register(node.GetChild(index));
        }
    }
}