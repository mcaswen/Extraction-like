using System;
using Core.BehaviorTree.Runtime;

namespace Core.BehaviorTree.Nodes.Decorators
{
    /// <summary>
    /// 作用域装饰器：透传子节点结果，并在进入、退出和中断时通知作用域对象。
    /// 中断时先中断子节点，再通知作用域。
    /// </summary>
    public sealed class ScopeDecorator : DecoratorNode
    {
        private readonly IBehaviorScope _scope;

        public ScopeDecorator(string nodeName, IBehaviorScope scope, BehaviorNode childNode)
            : base(nodeName, childNode)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        public IBehaviorScope Scope => _scope;

        protected override void OnEnter(BehaviorTreeContext context)
        {
            _scope.OnScopeEnter(context);
        }

        protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
        {
            BehaviorNodeResult result = ChildNode.Execute(context);
            if (!result.IsRunning)
                ChildNode.Exit(context, result);
            return result;
        }

        protected override void OnExit(BehaviorTreeContext context, BehaviorNodeResult result)
        {
            _scope.OnScopeExit(context, result);
        }

        protected override void OnAbort(BehaviorTreeContext context)
        {
            base.OnAbort(context);
            _scope.OnScopeAbort(context);
        }
    }
}
