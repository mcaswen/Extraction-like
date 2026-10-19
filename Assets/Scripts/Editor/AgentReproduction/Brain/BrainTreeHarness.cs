using System;
using System.Collections.Generic;
using Core.BehaviorTree.Blackboard;
using Core.BehaviorTree.Runtime;
using Gameplay.Agent.Core;
using Gameplay.Agent.Data;
using UnityEngine;
using BehaviorTreeType = global::Core.BehaviorTree.Runtime.BehaviorTree;

namespace AgentReproduction.Brain
{
    /// <summary>对拍脚本的一步：先执行 Before 改变世界或黑板，再推进 Ticks 次。</summary>
    public sealed class BrainStep
    {
        public readonly string Label;
        public readonly int Ticks;
        public readonly Action<BrainStepContext> Before;

        public BrainStep(string label, int ticks, Action<BrainStepContext> before = null)
        {
            Label = label;
            Ticks = ticks;
            Before = before;
        }
    }

    public sealed class BrainStepContext
    {
        public BehaviorBlackboard Blackboard;
        public RecordingAgent Agent;
        public AgentPawnRoot Pawn;
        public double TimeSeconds;
        public List<string> Log;

        public void SetDirective(AgentDirectiveRequest request)
        {
            Blackboard.SetValue(AgentBlackboardKeys.HasPendingDirective, request.DirectiveType != AgentDirectiveType.None, TimeSeconds);
            Blackboard.SetValue(AgentBlackboardKeys.PendingDirectiveRequest, request, TimeSeconds);
        }

        public void ClearDirective()
        {
            Blackboard.SetValue(AgentBlackboardKeys.HasPendingDirective, false, TimeSeconds);
            Blackboard.RemoveValue(AgentBlackboardKeys.PendingDirectiveRequest, TimeSeconds);
        }

        /// <summary>把真实智能体瞬移到指定地面点并同步物理；Warp 直接写入身体位置，需要自己加上导航基准偏移</summary>
        public void PlaceAgent(Vector3 groundPosition)
        {
            var nav = Pawn.NavMeshAgent;
            nav.Warp(groundPosition + Vector3.up * nav.baseOffset * Pawn.transform.lossyScale.y);
            Physics.SyncTransforms();
        }
    }

    /// <summary>每个变体各自构建的场景：真实智能体、场景对象和逐帧探针</summary>
    public sealed class BrainScenario
    {
        public AgentPawnRoot Pawn;
        public readonly List<Func<string>> Probes = new List<Func<string>>();
        public readonly List<Action> Cleanups = new List<Action>();
    }

    /// <summary>
    /// 在一帧内同步执行整段脚本，用固定步长和显式时间推进行为树，输出规范化的逐帧记录。
    /// 新旧两棵树在各自全新的世界里跑同一段脚本，记录逐行一致即判定等价。
    /// </summary>
    public static class BrainTreeHarness
    {
        public const float DeltaTime = 0.1f;

        private static readonly BlackboardKey[] SeededKeys =
        {
            AgentBlackboardKeys.MoveSpeed, AgentBlackboardKeys.MoveStoppingDistance, AgentBlackboardKeys.InteractionDistance,
            AgentBlackboardKeys.AttackRange, AgentBlackboardKeys.AttackDamage, AgentBlackboardKeys.AttackInterval
        };

        public static List<string> Run(
            AgentPawnRoot pawn,
            Func<BehaviorTreeType> buildTree,
            IEnumerable<BrainStep> steps,
            IReadOnlyList<Func<string>> probes = null,
            Core.BehaviorTree.Debugging.IBehaviorNodeEventSink eventSink = null)
        {
            var log = new List<string>();
            var blackboard = new BehaviorBlackboard();
            double time = 100d;
            foreach (BlackboardKey key in SeededKeys)
                if (pawn.Blackboard.TryGetValue(key, out float value)) blackboard.SetValue(key, value, time);
            blackboard.SetValue(AgentBlackboardKeys.HasVisibleEnemy, false, time);

            var agent = new RecordingAgent(pawn, blackboard, log) { TimeSeconds = time };
            blackboard.ValueChanged += (_, change) => log.Add("bb " + change.Key + "=" + BrainTrace.Format(change.NewValue));
            var context = new BehaviorTreeContext(blackboard, null, agent) { NodeEventSink = eventSink };
            var runner = new BehaviorTreeRunner(buildTree(), context);
            var step = new BrainStepContext { Blackboard = blackboard, Agent = agent, Pawn = pawn, Log = log };

            int tick = 0;
            foreach (BrainStep brainStep in steps)
            {
                log.Add("== " + brainStep.Label);
                step.TimeSeconds = time;
                brainStep.Before?.Invoke(step);
                Physics.SyncTransforms();
                for (int i = 0; i < brainStep.Ticks; i++)
                {
                    time += DeltaTime;
                    agent.TimeSeconds = time;
                    log.Add("tick " + (++tick));
                    BehaviorNodeResult result = runner.Tick(DeltaTime, time);
                    log.Add("  -> " + result.Status);
                    if (probes != null)
                        foreach (Func<string> probe in probes) log.Add("  probe " + probe());
                }
            }

            if (runner.IsRunning) runner.Abort();
            log.Add("== end");
            if (probes != null)
                foreach (Func<string> probe in probes) log.Add("  probe " + probe());
            return log;
        }

        /// <summary>返回首处差异的说明；完全一致时返回 null</summary>
        public static string FirstDifference(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
        {
            int count = Math.Min(expected.Count, actual.Count);
            for (int i = 0; i < count; i++)
                if (expected[i] != actual[i])
                    return $"line {i + 1}: expected [{expected[i]}] actual [{actual[i]}]" + Context(expected, i);
            if (expected.Count != actual.Count)
                return $"length differs: expected {expected.Count} actual {actual.Count}" + Context(expected.Count > actual.Count ? expected : actual, count);
            return null;
        }

        private static string Context(IReadOnlyList<string> lines, int index)
        {
            var text = new System.Text.StringBuilder("\ncontext:");
            for (int i = Math.Max(0, index - 6); i < Math.Min(lines.Count, index + 3); i++) text.Append("\n  ").Append(lines[i]);
            return text.ToString();
        }
    }

    public static class BrainTrace
    {
        public static string Format(Vector3 value) => $"({value.x:0.00},{value.y:0.00},{value.z:0.00})";

        public static string Format(object value)
        {
            switch (value)
            {
                case null: return "null";
                case Vector3 v: return Format(v);
                case float f: return f.ToString("0.###");
                case double d: return d.ToString("0.###");
                case AgentDirectiveRequest r: return $"{r.DirectiveType}:{r.CommandId}:{r.TargetRef.Kind}";
                case UnityEngine.Object o: return o != null ? o.name : "destroyed";
                default: return value.ToString();
            }
        }
    }
}
