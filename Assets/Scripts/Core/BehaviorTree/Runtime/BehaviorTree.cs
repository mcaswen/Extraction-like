using System;
using Core.BehaviorTree.Nodes;

namespace Core.BehaviorTree.Runtime
{
    /// <summary>
    /// 行为树类定义，包含树的名称和根节点
    /// </summary>
    public sealed class BehaviorTree
    {
        public string Name { get; }
        public BehaviorNode RootNode { get; }

        public BehaviorTree(string name, BehaviorNode rootNode)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "BehaviorTree" : name;
            RootNode = rootNode ?? throw new ArgumentNullException(nameof(rootNode));
        }
    }
}