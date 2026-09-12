using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AgentReproduction.Infrastructure;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Navigation;
using Gameplay.Agent.Routes;
using Gameplay.Agent.Runtime;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class AgentRoutePlanningTests : ReproductionTestFixture
    {
        private static MapGraphNodeDefinition Node(string id) => new MapGraphNodeDefinition(id, MapGraphNodeKind.Resource, Vector2.zero);
        private static MapGraphEdgeDefinition Edge(string a, string b) => new MapGraphEdgeDefinition(a + b, a, b, 999);
        private static MapGraphService Graph(bool disconnected = false) => new MapGraphService(
            new[] { Node("c"), Node("a"), Node("b") }, disconnected ? new[] { Edge("a", "b") } : new[] { Edge("a", "b"), Edge("b", "c") });
        private static MapGraphCostSnapshot Costs() => new MapGraphCostSnapshot("profile", 8,
            new[] { new MapGraphEdgeCost("ab", 7, 11), new MapGraphEdgeCost("bc", 5, 13) });

        private sealed class Resolver : IAgentRouteTargetResolver
        {
            public long Revision { get; set; } = 4;
            public readonly Dictionary<string, AgentRouteTargetFacts> Facts = new Dictionary<string, AgentRouteTargetFacts>();
            public Resolver() { Set("a", 1); Set("b", 2); Set("c", 3); }
            public void Set(string id, float x, AgentRouteTargetStatus status = AgentRouteTargetStatus.Ready)
                => Facts[id] = new AgentRouteTargetFacts(id, MapGraphNodeKind.Resource, new Vector3(x, 0, 0), status);
            public bool TryGetFacts(string id, out AgentRouteTargetFacts facts) => Facts.TryGetValue(id, out facts);
            public bool TryResolveNode(AgentTargetRef target, out string id) { id = target.TargetId; return Facts.ContainsKey(id); }
            public bool TryCreateProcessingDirective(string nodeId, AgentId agentId, string commandId, int priority,
                out AgentDirectiveRequest directive, out AgentDirectiveFailure failure)
            { directive = default; failure = AgentDirectiveFailure.InvalidTarget; return false; }
        }

        private static AgentNavigationSegmentResult Segment(Vector3 origin, Vector3 destination, float length)
            => (AgentNavigationSegmentResult)Activator.CreateInstance(typeof(AgentNavigationSegmentResult),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { origin, destination, length, 2 }, null);
        private static AgentRoutePlanner Planner(Resolver resolver, float[] lengths, string target = "c",
            IReadOnlyList<string> entries = null, MapGraphService graph = null, Action duringQuery = null)
        {
            Func<Vector3, Vector3, AgentNavigationSegmentResult> query = (a, b) =>
            { duringQuery?.Invoke(); return Segment(a, b, lengths[(int)b.x - 1]); };
            return (AgentRoutePlanner)Activator.CreateInstance(typeof(AgentRoutePlanner), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { graph ?? Graph(), 3L, Costs(), resolver, Vector3.zero, target, query, entries }, null);
        }
        private static void Finish(AgentRoutePlanner planner, int budget = 1)
        { for (int i = 0; i < 10 && !planner.IsDone; i++) Assert.That(planner.Advance(budget), Is.LessThanOrEqualTo(budget)); Assert.That(planner.IsDone, Is.True); }

        [UnityTest]
        public IEnumerator ActualEntryDistanceAndDirectedGraphCostsDefineTheRoute()
        {
            var resolver = new Resolver();
            var forward = Planner(resolver, new[] { 9f, 20f, 30f }); Finish(forward);
            Assert.That(forward.Result.NodeIds, Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(forward.Result.EntryLength, Is.EqualTo(9));
            Assert.That(forward.Result.GraphLength, Is.EqualTo(12));
            var reverse = Planner(resolver, new[] { 30f, 20f, 1f }, "a"); Finish(reverse);
            Assert.That(reverse.Result.NodeIds, Is.EqualTo(new[] { "c", "b", "a" }));
            Assert.That(reverse.Result.GraphLength, Is.EqualTo(24));
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator DisconnectedGoalCannotBecomeAnAlternativeDirectEntry()
        {
            var planner = Planner(new Resolver(), new[] { 1f, 2f, 3f }, graph: Graph(true)); Finish(planner);
            Assert.That(planner.Result, Is.Null);
            Assert.That(planner.Failure, Is.EqualTo(AgentRouteFailure.Disconnected));
            Assert.That(planner.QueryCount, Is.EqualTo(3));
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator ReplacementUsesOnlyTheAllowedCurrentEdgeEndpoints()
        {
            var planner = Planner(new Resolver(), new[] { 10f, 5f, 1f }, entries: new[] { "b", "a", "a" }); Finish(planner);
            Assert.That(planner.Result.NodeIds, Is.EqualTo(new[] { "b", "c" }));
            Assert.That(planner.QueryCount, Is.EqualTo(2));
            Assert.That(planner.Result.EntryLength, Is.EqualTo(5));
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator BudgetsTiesAndFrozenResultsAreIndependentOfBatchSize()
        {
            var resolver = new Resolver();
            var one = Planner(resolver, new[] { 1f, 1f, 1f });
            Assert.That(one.Advance(1), Is.EqualTo(1)); Assert.That(one.Result, Is.Null); Assert.That(one.IsDone, Is.False);
            Finish(one);
            var many = Planner(resolver, new[] { 1f, 1f, 1f }, entries: new[] { "c", "b", "a" }); Finish(many, 8);
            Assert.That(one.Result.NodeIds, Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(many.Result.NodeIds, Is.EqualTo(one.Result.NodeIds));
            Assert.That(many.QueryCount, Is.EqualTo(one.QueryCount));
            Assert.That(one.Result.GraphRevision, Is.EqualTo(3)); Assert.That(one.Result.CostRevision, Is.EqualTo(8));
            Assert.That(one.Result.BindingRevision, Is.EqualTo(4)); Assert.That(one.Result.ProfileId, Is.EqualTo("profile"));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)one.Result.NodeIds).Clear());
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator CompletedNodesRemainTransitButUnavailableNodesBlockEdges()
        {
            var resolver = new Resolver(); resolver.Set("b", 2, AgentRouteTargetStatus.Completed);
            var completed = Planner(resolver, new[] { 1f, 2f, 3f }); Finish(completed);
            Assert.That(completed.Result.NodeIds, Is.EqualTo(new[] { "a", "b", "c" }));
            resolver.Set("b", 2, AgentRouteTargetStatus.Unavailable);
            var blocked = Planner(resolver, new[] { 1f, 2f, 3f }); Finish(blocked);
            Assert.That(blocked.Result, Is.Null); Assert.That(blocked.Failure, Is.EqualTo(AgentRouteFailure.Disconnected));
            resolver.Set("c", 3, AgentRouteTargetStatus.Completed);
            var same = Planner(resolver, new[] { 3f, 2f, 1f }); Finish(same);
            Assert.That(same.Result.NodeIds, Is.EqualTo(new[] { "c" }), "同节点也必须保留实际到达步骤");
            Assert.That(same.Result.EntryLength, Is.EqualTo(1));
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator CancellationAndBindingChangesCannotPublishStalePlans()
        {
            var resolver = new Resolver();
            var cancelled = Planner(resolver, new[] { 1f, 2f, 3f }); cancelled.Advance(1); cancelled.Cancel();
            Assert.That(cancelled.Advance(8), Is.Zero); Assert.That(cancelled.Result, Is.Null); Assert.That(cancelled.QueryCount, Is.EqualTo(1));
            var stale = Planner(resolver, new[] { 1f, 2f, 3f }); stale.Advance(1); resolver.Revision++;
            Assert.That(stale.Advance(8), Is.Zero); Assert.That(stale.Result, Is.Null); Assert.That(stale.Failure, Is.EqualTo(AgentRouteFailure.StaleContext));
            var ready = Planner(resolver, new[] { 1f, 2f, 3f }); Finish(ready); Assert.That(ready.Result, Is.Not.Null);
            resolver.Revision++; Assert.That(ready.Result, Is.Null);
            var inQuery = Planner(resolver, new[] { 1f, 2f, 3f }, duringQuery: () => resolver.Revision++);
            Assert.That(inQuery.Advance(8), Is.EqualTo(1)); Assert.That(inQuery.Result, Is.Null);
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator MissingInvalidAndNonFiniteInputsNeverYieldPartialExecution()
        {
            var resolver = new Resolver();
            var missing = Planner(resolver, new[] { 1f, 2f, 3f }, "unknown"); Finish(missing);
            Assert.That(missing.Failure, Is.EqualTo(AgentRouteFailure.MissingTarget)); Assert.That(missing.QueryCount, Is.Zero);
            var bad = Planner(resolver, new[] { 1f, 2f, 3f }, entries: new[] { "missing" });
            Assert.That(bad.IsDone, Is.True); Assert.That(bad.Result, Is.Null); Assert.That(bad.Failure, Is.EqualTo(AgentRouteFailure.InvalidRequest));
            var empty = Planner(resolver, new[] { 1f, 2f, 3f }, entries: Array.Empty<string>()); Finish(empty);
            Assert.That(empty.Failure, Is.EqualTo(AgentRouteFailure.NoReachableEntry));
            var invalid = Planner(resolver, new[] { float.NaN, float.PositiveInfinity, -1f }); Finish(invalid);
            Assert.That(invalid.Result, Is.Null); Assert.That(invalid.Failure, Is.EqualTo(AgentRouteFailure.NoReachableEntry));
            Assert.Throws<ArgumentOutOfRangeException>(() => invalid.Advance(0));
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator RootIdentitySurvivesRoutingAndTargetCanonicalization()
        {
            var original = new AgentRouteRequest("", AgentRouteSource.Player, targetRef:
                AgentTargetRef.FromAbstractPoint(AgentTargetKind.Resource, "business-id", Vector3.one));
            var routed = original.WithTargetAgentId(AgentId.FromString("agent-a")).WithTargetNodeId("c");
            Assert.That(routed.RequestId, Is.EqualTo(original.RequestId)); Assert.That(routed.Source, Is.EqualTo(AgentRouteSource.Player));
            Assert.That(routed.TargetNodeId, Is.EqualTo("c")); Assert.That(routed.TargetRef.TargetId, Is.EqualTo("business-id"));
            Assert.That(original.TargetAgentId.IsEmpty, Is.True); Assert.That(original.TargetNodeId, Is.Empty);
            Assert.That(new AgentRouteResult(routed, 1, AgentRouteStage.Planning).Accepted, Is.False);
            ContractCompleted = true; yield break;
        }
    }
}
