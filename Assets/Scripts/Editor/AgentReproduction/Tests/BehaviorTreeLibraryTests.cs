using System;
using System.Collections.Generic;
using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Debugging;
using Core.BehaviorTree.Nodes;
using Core.BehaviorTree.Nodes.Composites;
using Core.BehaviorTree.Nodes.Decorators;
using Core.BehaviorTree.Nodes.Leaves;
using Core.BehaviorTree.Runtime;
using Core.StateMachine.Runtime;
using NUnit.Framework;
using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;

namespace AgentReproduction.Tests
{
    /// <summary>行为树与状态机库语义：响应式组合的中断时机、作用域回调、结构接口、结构化事件和状态机观察。</summary>
    public sealed class BehaviorTreeLibraryTests
    {
        private sealed class Probe : ActionNode
        {
            private readonly List<string> _log;
            public BehaviorNodeStatus Next = BehaviorNodeStatus.Success;

            public Probe(string name, List<string> log) : base(name) { _log = log; }

            protected override void OnEnter(BehaviorTreeContext context) => _log.Add(NodeName + ":enter");
            protected override BehaviorNodeResult Tick(BehaviorTreeContext context)
            {
                _log.Add(NodeName + ":tick");
                return Next == BehaviorNodeStatus.Success ? Succeed()
                    : Next == BehaviorNodeStatus.Running ? Running()
                    : Fail(BehaviorFailureCode.ConditionFailed, "probe");
            }
            protected override void OnExit(BehaviorTreeContext context, BehaviorNodeResult result) => _log.Add(NodeName + ":exit");
            protected override void OnAbort(BehaviorTreeContext context) => _log.Add(NodeName + ":abort");
        }

        private sealed class Switch : ConditionNode
        {
            public bool Value = true;
            public Switch(string name) : base(name) { }
            protected override bool Evaluate(BehaviorTreeContext context) => Value;
        }

        private sealed class Scope : IBehaviorScope
        {
            private readonly List<string> _log;
            public Scope(List<string> log) { _log = log; }
            public void OnScopeEnter(BehaviorTreeContext context) => _log.Add("scope:enter");
            public void OnScopeExit(BehaviorTreeContext context, BehaviorNodeResult result) => _log.Add("scope:exit");
            public void OnScopeAbort(BehaviorTreeContext context) => _log.Add("scope:abort");
        }

        private sealed class State : StateMachineState
        {
            public State(string name) : base(name) { }
        }

        private sealed class Observer : IStateMachineObserver
        {
            public readonly List<string> Log = new List<string>();
            public void OnTransitionEvaluationStarted(StateMachineState leafState, double timeSeconds) => Log.Add("start:" + leafState.StateName);
            public void OnTransitionEvaluated(StateMachineState ownerState, StateTransition transition, bool satisfied)
                => Log.Add($"eval:{ownerState.StateName}:{transition.Name}:{satisfied}");
            public void OnTransitionPerformed(StateMachineState fromLeaf, StateMachineState toLeaf, StateTransition transition, StateChangeReason reason, double timeSeconds)
                => Log.Add($"performed:{fromLeaf.StateName}>{toLeaf.StateName}:{transition?.Name}:{transition?.Priority}:{reason}");
        }

        private static BehaviorTreeContext Context() => new BehaviorTreeContext(new BehaviorBlackboard());

        [Test]
        public void ReactiveSequenceRechecksEarlierChildrenAndAbortsTheRunningLaterChild()
        {
            var log = new List<string>();
            var guard = new Switch("guard");
            var first = new Probe("first", log);
            var second = new Probe("second", log) { Next = BehaviorNodeStatus.Running };
            var runner = new BehaviorTreeRunner(new BehaviorTreeType("t", new ReactiveSequenceNode("seq", guard, first, second)), Context());

            Assert.That(runner.Tick(0.1f, 1).IsRunning, Is.True);
            Assert.That(log, Is.EqualTo(new[] { "first:enter", "first:tick", "first:exit", "second:enter", "second:tick" }));

            log.Clear();
            Assert.That(runner.Tick(0.1f, 2).IsRunning, Is.True);
            Assert.That(log, Is.EqualTo(new[] { "first:enter", "first:tick", "first:exit", "second:tick" }), "前面的子节点每帧重新执行，运行中的子节点不重新进入");

            log.Clear();
            first.Next = BehaviorNodeStatus.Running;
            Assert.That(runner.Tick(0.1f, 3).IsRunning, Is.True);
            Assert.That(log, Is.EqualTo(new[] { "first:enter", "first:tick", "second:abort" }), "更前的子节点转为运行中时，在它返回之后中断后面的子节点");

            log.Clear();
            guard.Value = false;
            Assert.That(runner.Tick(0.1f, 4).IsFailure, Is.True);
            Assert.That(log, Is.EqualTo(new[] { "first:abort" }), "条件失败时中断运行中的子节点");
        }

        [Test]
        public void ReactiveSelectorPreemptsTheRunningLowerPriorityChild()
        {
            var log = new List<string>();
            var high = new Probe("high", log) { Next = BehaviorNodeStatus.Failure };
            var low = new Probe("low", log) { Next = BehaviorNodeStatus.Running };
            var runner = new BehaviorTreeRunner(new BehaviorTreeType("t", new ReactiveSelectorNode("sel", high, low)), Context());

            Assert.That(runner.Tick(0.1f, 1).IsRunning, Is.True);
            log.Clear();
            Assert.That(runner.Tick(0.1f, 2).IsRunning, Is.True);
            Assert.That(log, Is.EqualTo(new[] { "high:enter", "high:tick", "high:exit", "low:tick" }), "高优先级子节点每帧重新求值");

            log.Clear();
            high.Next = BehaviorNodeStatus.Success;
            Assert.That(runner.Tick(0.1f, 3).IsSuccess, Is.True);
            Assert.That(log, Is.EqualTo(new[] { "high:enter", "high:tick", "high:exit", "low:abort" }), "胜出的子节点返回之后中断低优先级子节点");

            log.Clear();
            high.Next = BehaviorNodeStatus.Failure;
            low.Next = BehaviorNodeStatus.Failure;
            Assert.That(runner.Tick(0.1f, 4).IsFailure, Is.True);
            Assert.That(log, Is.EqualTo(new[] { "high:enter", "high:tick", "high:exit", "low:enter", "low:tick", "low:exit" }));
        }

        [Test]
        public void MemoryCompositesKeepTheirOriginalResumeSemantics()
        {
            var log = new List<string>();
            var guard = new Switch("guard");
            var work = new Probe("work", log) { Next = BehaviorNodeStatus.Running };
            var runner = new BehaviorTreeRunner(new BehaviorTreeType("t", new SequenceNode("seq", guard, work)), Context());
            runner.Tick(0.1f, 1);
            guard.Value = false;
            log.Clear();
            Assert.That(runner.Tick(0.1f, 2).IsRunning, Is.True, "记忆式序列从运行中的子节点续跑，不重查前面的条件");
            Assert.That(log, Is.EqualTo(new[] { "work:tick" }));
        }

        [Test]
        public void ScopeDecoratorWrapsTheSubtreeLifecycle()
        {
            var log = new List<string>();
            var child = new Probe("child", log) { Next = BehaviorNodeStatus.Running };
            var scoped = new ScopeDecorator("scope", new Scope(log), child);
            var context = Context();
            var runner = new BehaviorTreeRunner(new BehaviorTreeType("t", scoped), context);
            runner.Tick(0.1f, 1);
            Assert.That(log, Is.EqualTo(new[] { "scope:enter", "child:enter", "child:tick" }));

            log.Clear();
            runner.Abort();
            Assert.That(log, Is.EqualTo(new[] { "child:abort", "scope:abort" }), "中断时先中断子节点再通知作用域");

            log.Clear();
            child.Next = BehaviorNodeStatus.Success;
            runner.Tick(0.1f, 2);
            Assert.That(log, Is.EqualTo(new[] { "scope:enter", "child:enter", "child:tick", "child:exit", "scope:exit" }));
        }

        [Test]
        public void TreeIndexesNodesDepthFirstAndRejectsSharedInstances()
        {
            var log = new List<string>();
            var a = new Probe("a", log);
            var b = new Switch("b");
            var c = new Probe("c", log);
            var inner = new ReactiveSelectorNode("inner", b, c);
            var root = new SequenceNode("root", a, new ScopeDecorator("scope", new Scope(log), inner));
            var tree = new BehaviorTreeType("t", root);

            Assert.That(tree.Nodes.Count, Is.EqualTo(6));
            Assert.That(new[] { root.Index, a.Index, inner.Index, b.Index, c.Index }, Is.EqualTo(new[] { 0, 1, 3, 4, 5 }));
            Assert.That(tree.Nodes[2].Kind, Is.EqualTo(BehaviorNodeKind.Decorator));
            Assert.That(root.Kind, Is.EqualTo(BehaviorNodeKind.Composite));
            Assert.That(b.Kind, Is.EqualTo(BehaviorNodeKind.Condition));
            Assert.That(a.Kind, Is.EqualTo(BehaviorNodeKind.Action));
            Assert.That(root.ChildCount, Is.EqualTo(2));
            Assert.That(root.GetChild(1).GetChild(0), Is.SameAs(inner));
            Assert.That(a.Tree, Is.SameAs(tree));
            Assert.Throws<InvalidOperationException>(() => new BehaviorTreeType("again", new SequenceNode("other", a)));
        }

        [Test]
        public void NodeStatusAndEventsAreRecordedWithoutStringTraces()
        {
            var guard = new Switch("guard");
            var log = new List<string>();
            var leaf = new ReactiveSelectorNode("leaf", new Switch("never") { Value = false }, new Switch("always"));
            var root = new ReactiveSequenceNode("root", guard, leaf);
            var trace = new BehaviorTreeDebugTrace();
            var context = new BehaviorTreeContext(new BehaviorBlackboard(), trace);
            var ring = new BehaviorNodeEventRing(64);
            context.NodeEventSink = ring;
            var runner = new BehaviorTreeRunner(new BehaviorTreeType("t", root), context);

            runner.Tick(0.1f, 1);
            Assert.That(root.LastStatus, Is.EqualTo(BehaviorNodeStatus.Success));
            Assert.That(root.LastTickVersion, Is.EqualTo(context.TickVersion));
            Assert.That(leaf.GetChild(0).LastStatus, Is.EqualTo(BehaviorNodeStatus.Failure));
            Assert.That(leaf.GetChild(0).LastFailureCode, Is.EqualTo(BehaviorFailureCode.ConditionFailed));
            Assert.That(ring.Count, Is.GreaterThan(0));
            var first = ring.Get(0);
            Assert.That(first.Node, Is.SameAs(root));
            Assert.That(first.Kind, Is.EqualTo(BehaviorNodeEventKind.Enter));

            for (int i = 0; i < 100; i++) runner.Tick(0.1f, 2 + i);
            Assert.That(trace.Records, Is.Empty, "节点进入、执行、退出不再逐帧拼接文字记录");
            Assert.That(ring.Count, Is.EqualTo(ring.Capacity));
            Assert.That(ring.TotalRecorded, Is.GreaterThan(ring.Capacity));
            var newest = ring.Get(ring.Count - 1);
            Assert.That(newest.TickVersion, Is.EqualTo(context.TickVersion), "环形缓冲保留最新记录");
            Assert.That(log, Is.Empty);
        }

        [Test]
        public void StateMachineObserverSeesOnlyTheEvaluationsTheMachineMakes()
        {
            var root = new State("root");
            var idle = new State("idle");
            var busy = new State("busy");
            root.AddChild(idle, true);
            root.AddChild(busy);
            bool go = false;
            idle.AddTransition(new StateTransition("low", busy, _ => go, 10));
            idle.AddTransition(new StateTransition("high", busy, _ => go, 90));
            var machine = new HierarchicalStateMachine(root, new StateMachineContext(Context()));
            var observer = new Observer();
            machine.Observer = observer;

            machine.Update(0.1f, 1);
            Assert.That(observer.Log, Is.EqualTo(new[] { "start:idle", "eval:idle:low:False", "eval:idle:high:False" }));

            observer.Log.Clear();
            go = true;
            machine.Update(0.1f, 2);
            Assert.That(observer.Log, Is.EqualTo(new[] { "start:idle", "eval:idle:low:True", "eval:idle:high:True", "performed:idle>busy:high:90:Transition" }));
            Assert.That(machine.CurrentLeafState, Is.SameAs(busy));
            Assert.That(machine.RootState, Is.SameAs(root));
        }
    }
}
