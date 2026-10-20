using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using Gameplay.Agent.Commands;
using Gameplay.Agent.Data;
using Gameplay.Agent.Decision;
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
    /// <summary>决策用作者图路程、途经敌群风险和带出价值撤离加分。</summary>
    public sealed class DecisionRoutePacingTests : ReproductionTestFixture
    {
        private static MapGraphNodeDefinition Node(string id) => new MapGraphNodeDefinition(id, MapGraphNodeKind.Resource, Vector2.zero);
        private static MapGraphEdgeDefinition Edge(string a, string b) => new MapGraphEdgeDefinition(a + b, a, b, 999);

        private sealed class Resolver : IAgentRouteTargetResolver
        {
            public long Revision => 1;
            public readonly Dictionary<string, AgentRouteTargetFacts> Facts = new Dictionary<string, AgentRouteTargetFacts>();
            public void Set(string id, Vector3 anchor, AgentRouteTargetStatus status = AgentRouteTargetStatus.Ready)
                => Facts[id] = new AgentRouteTargetFacts(id, MapGraphNodeKind.Resource, anchor, status);
            public bool TryGetFacts(string id, out AgentRouteTargetFacts facts) => Facts.TryGetValue(id, out facts);
            public bool TryResolveNode(AgentTargetRef target, out string id) { id = target.TargetId; return Facts.ContainsKey(id); }
            public bool TryCreateProcessingDirective(string nodeId, AgentId agentId, string commandId, int priority,
                out AgentDirectiveRequest directive, out AgentDirectiveFailure failure)
            { directive = default; failure = AgentDirectiveFailure.InvalidTarget; return false; }
            public bool TryCopyAliveEnemies(string nodeId, List<global::EnemyHealthController> results) { results?.Clear(); return false; }
        }

        // a(0,0,0) — b(0,0,100) — c(10,0,0)：c 直线离 a 很近，但图上只能经 b 绕行。
        private static AgentRouteEnvironment Environment(Resolver resolver)
        {
            var graph = new MapGraphService(new[] { Node("a"), Node("b"), Node("c") }, new[] { Edge("a", "b"), Edge("b", "c") });
            var costs = new MapGraphCostSnapshot("profile", 1, new[] { new MapGraphEdgeCost("ab", 100, 100), new MapGraphEdgeCost("bc", 120, 120) });
            return new AgentRouteEnvironment(graph, 1, 1, costs, resolver, new AgentNavigationProfile(0, -1, 1, 1));
        }

        private static Resolver ThreeNodes()
        {
            var resolver = new Resolver();
            resolver.Set("a", Vector3.zero); resolver.Set("b", new Vector3(0, 0, 100)); resolver.Set("c", new Vector3(10, 0, 0));
            return resolver;
        }

        private static AgentTargetRef Target(string id) => AgentTargetRef.FromConcreteObject(AgentTargetKind.Resource, null, id);

        [UnityTest]
        public IEnumerator RouteEstimateFollowsAuthoredGraphInsteadOfStraightLine()
        {
            var estimator = new AgentRouteCostEstimator();
            var environment = Environment(ThreeNodes());
            var nodes = new List<string>();
            Assert.That(estimator.TryEstimate(environment, new Vector3(0, 0, 3), null, Target("c"), nodes, out float length), Is.True);
            Assert.That(nodes, Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(length, Is.EqualTo(223).Within(0.01f), "起点直线 3 + 图上 100 + 120");
            // 上一路线停在 b 且仍在附近时从 b 出发，不按直线回退到 a。
            Assert.That(estimator.TryEstimate(environment, new Vector3(0, 0, 90), "b", Target("c"), nodes, out length), Is.True);
            Assert.That(nodes, Is.EqualTo(new[] { "b", "c" }));
            Assert.That(length, Is.EqualTo(130).Within(0.01f));
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator UnavailableTransitNodeMakesTargetUnreachable()
        {
            var resolver = ThreeNodes();
            resolver.Set("b", new Vector3(0, 0, 100), AgentRouteTargetStatus.Unavailable);
            var estimator = new AgentRouteCostEstimator();
            Assert.That(estimator.TryEstimate(Environment(resolver), Vector3.zero, null, Target("c"), null, out float length), Is.False);
            Assert.That(float.IsPositiveInfinity(length), Is.True);
            Assert.That(estimator.TryEstimate(Environment(resolver), Vector3.zero, null, Target("missing"), null, out _), Is.False);
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator RouteRiskReplacesStraightLineProximityAndCanRejectResource()
        {
            var config = World.Own(ScriptableObject.CreateInstance<AgentDecisionConfig>());
            var service = new AgentTargetDecisionService(config);
            var target = World.Root("Resource");
            var context = new AgentDecisionContext(Vector3.zero, 30, 300, 300, 10, string.Empty, new List<global::EnemyHealthController>());
            var safe = new AgentDecisionCandidate(AgentDecisionTargetKind.Resource, AgentDirectiveType.Search, AgentTargetKind.Resource,
                "r", target, Vector3.forward * 50, 50 * 50, null, false, true, 0.5f);
            Assert.That(service.TryChooseTarget(context, new[] { safe }, out var result), Is.True);
            Assert.That(result.Risk, Is.EqualTo(0.5f).Within(0.0001f));
            var dangerous = new AgentDecisionCandidate(AgentDecisionTargetKind.Resource, AgentDirectiveType.Search, AgentTargetKind.Resource,
                "r", target, Vector3.forward * 50, 50 * 50, null, false, true, config.ResourceRiskThreshold + 0.1f);
            Assert.That(service.TryChooseTarget(context, new[] { dangerous }, out _), Is.False, "途经敌群风险超过资源阈值时不选择");
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator CarriedValueRaisesExtractionAboveNearbyResource()
        {
            var config = World.Own(ScriptableObject.CreateInstance<AgentDecisionConfig>());
            RuntimeFixtureAccess.Configure(config, "_carriedValueTarget", 4000f);
            RuntimeFixtureAccess.Configure(config, "_carriedValueExtractionBonus", 80f);
            var service = new AgentTargetDecisionService(config);
            var resource = new AgentDecisionCandidate(AgentDecisionTargetKind.Resource, AgentDirectiveType.Search, AgentTargetKind.Resource,
                "r", World.Root("Resource"), Vector3.forward * 100, 100 * 100, null, false, true, 0f);
            var exit = new AgentDecisionCandidate(AgentDecisionTargetKind.Extraction, AgentDirectiveType.Extract, AgentTargetKind.Extraction,
                "x", World.Root("Exit"), Vector3.back * 300, 300 * 300, null, false, true, 0f);
            var candidates = new[] { resource, exit };
            var risks = new List<global::EnemyHealthController>();
            var empty = new AgentDecisionContext(Vector3.zero, 30, 300, 300, 10, string.Empty, risks, 0);
            Assert.That(service.TryChooseTarget(empty, candidates, out var early), Is.True);
            Assert.That(early.Candidate.DecisionTargetKind, Is.EqualTo(AgentDecisionTargetKind.Resource));
            var half = new AgentDecisionContext(Vector3.zero, 30, 300, 300, 10, string.Empty, risks, 2000);
            Assert.That(service.TryChooseTarget(half, candidates, out var middle), Is.True);
            Assert.That(middle.Candidate.DecisionTargetKind, Is.EqualTo(AgentDecisionTargetKind.Resource), "36+40-24 低于 85-8");
            var full = new AgentDecisionContext(Vector3.zero, 30, 300, 300, 10, string.Empty, risks, 9000);
            Assert.That(service.TryChooseTarget(full, candidates, out var late), Is.True);
            Assert.That(late.Candidate.DecisionTargetKind, Is.EqualTo(AgentDecisionTargetKind.Extraction), "加分封顶 80：36+80-24 高于 85-8");
            ContractCompleted = true; yield break;
        }

        [UnityTest]
        public IEnumerator ExtractionPenaltyScaleLetsLowHealthAgentRetreatToFarExit()
        {
            var config = World.Own(ScriptableObject.CreateInstance<AgentDecisionConfig>());
            var service = new AgentTargetDecisionService(config);
            var resource = new AgentDecisionCandidate(AgentDecisionTargetKind.Resource, AgentDirectiveType.Search, AgentTargetKind.Resource,
                "r", World.Root("Resource"), Vector3.forward * 80, 80 * 80, null, false, true, 0.5f);
            var exit = new AgentDecisionCandidate(AgentDecisionTargetKind.Extraction, AgentDirectiveType.Extract, AgentTargetKind.Extraction,
                "x", World.Root("Exit"), Vector3.back * 700, 700 * 700, null, false, true, 3f);
            var wounded = new AgentDecisionContext(Vector3.zero, 30, 90, 300, 10, string.Empty, new List<global::EnemyHealthController>());
            // 倍率 1：36+72-24*3-0.08*700=-20，低于资源 85-12-6.4，残血也不回撤。
            Assert.That(service.TryChooseTarget(wounded, new[] { resource, exit }, out var full), Is.True);
            Assert.That(full.Candidate.DecisionTargetKind, Is.EqualTo(AgentDecisionTargetKind.Resource));
            RuntimeFixtureAccess.Configure(config, "_extractionPenaltyScale", 0.25f);
            Assert.That(service.TryChooseTarget(wounded, new[] { resource, exit }, out var scaled), Is.True);
            Assert.That(scaled.Candidate.DecisionTargetKind, Is.EqualTo(AgentDecisionTargetKind.Extraction), "倍率 0.25：108-18-14=76 高于 66.6");
            ContractCompleted = true; yield break;
        }
    }
}
