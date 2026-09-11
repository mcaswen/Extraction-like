using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using AnomalySearch.Automation.SceneRaid;
using AnomalySearch.Automation.SceneRaid.Commands;
using Gameplay.Agent.Runtime;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Runtime;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class SceneRaidCommandHarnessTests : ReproductionTestFixture
    {
        public static string[] BadScripts = { "Version", "Duplicate", "UnknownKind", "ForwardReference", "Deadline", "Reason" };
        private static SceneRaidCommandScenario.Step Step(string id, string agent = "1") => new SceneRaidCommandScenario.Step
            { id = id, agent = agent, target = new SceneRaidCommandScenario.Selector { kind = "Resource", distance = "Near" } };
        private string Output(string name) => Path.Combine(TestRunContext.Load().outputPath, name);
        private static Event[] ReadEvents(string output)
        {
            using var stream = new FileStream(Path.Combine(output, "events.jsonl"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(JsonUtility.FromJson<Event>).ToArray();
        }

        [UnityTest]
        public IEnumerator ScenarioRoundTripPreservesNestedSelectorsAndReferences()
        {
            var first = Step("first"); var second = Step("second", "2");
            second.target.sameAsStep = "first"; second.gate.kind = "Moving"; second.gate.referenceStep = "first";
            var scenario = new SceneRaidCommandScenario { id = "Harness", steps = new[] { first, second } };
            scenario.Validate();
            string json = JsonUtility.ToJson(scenario);
            var copy = JsonUtility.FromJson<SceneRaidCommandScenario>(json); copy.Validate();
            Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(json));
            Assert.That(copy.steps[1].target.sameAsStep, Is.EqualTo("first"));
            ContractCompleted = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidCommandScenarioFailsBeforeExecution([ValueSource(nameof(BadScripts))] string defect)
        {
            var scenario = new SceneRaidCommandScenario { id = "Invalid", steps = new[] { Step("first"), Step("second") } };
            if (defect == "Version") scenario.schemaVersion = 8;
            if (defect == "Duplicate") scenario.steps[1].id = "first";
            if (defect == "UnknownKind") scenario.steps[0].target.kind = "Zone";
            if (defect == "ForwardReference") scenario.steps[0].target.sameAsStep = "second";
            if (defect == "Deadline") scenario.steps[0].gameDeadline = float.NaN;
            if (defect == "Reason") scenario.steps[0].reason = "Unreachable";
            Assert.Throws<System.InvalidOperationException>(() => scenario.Validate());
            ContractCompleted = true;
            yield return null;
        }

        [UnityTest]
        public IEnumerator EvidenceSeparatesSameFrameEarlyRejections()
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "1", Vector3.zero);
            AgentFactory.Create(World, "2", new Vector3(0, 0, 8));
            TargetFactory.Resources(World, new Vector3(15, 0, 0));
            yield return null;
            using var writer = new SceneRaidEvidenceWriter(Output("early-rejections"));
            var identity = new SceneRaidIdentityMap();
            using var observer = new SceneRaidObserver(writer, identity);
            var model = new SceneRaidReadModel(identity, observer.LatestResource);
            using var evidence = new SceneRaidCommandEvidence(Output("early-rejections"), "Harness", writer, observer, model, identity);
            var a = evidence.Submit(Step("one"), null, null);
            var b = evidence.Submit(Step("two", "2"), null, null);
            Assert.That(a.accepted || b.accepted, Is.False);
            Assert.That(a.commandId, Is.Null.Or.Empty); Assert.That(b.commandId, Is.Null.Or.Empty);
            Assert.That(a.frame, Is.EqualTo(b.frame));
            Assert.That(a.outcomeSequence, Is.Not.EqualTo(b.outcomeSequence));
            Assert.That(a.synchronousFeedback.Single().attemptId, Is.EqualTo(a.attemptId));
            Assert.That(b.synchronousFeedback.Single().attemptId, Is.EqualTo(b.attemptId));
            writer.Flush();
            var raw = ReadEvents(Output("early-rejections"));
            foreach (var attempt in new[] { a, b })
            {
                var source = raw.Single(x => x.sequence == attempt.outcomeSequence);
                Assert.That(source.kind, Is.EqualTo("directive.Rejected"));
                Assert.That(JsonUtility.FromJson<SceneRaidCommandEvidence.Feedback>(source.detail).reason, Is.EqualTo("InvalidTarget"));
            }
            Assert.Throws<System.InvalidOperationException>(() => evidence.Submit(Step("one"), null, null));
            ContractCompleted = true;
        }

        [System.Serializable] private sealed class Event { public long sequence; public string kind, detail; }

        [UnityTest]
        public IEnumerator ObserverFailureDoesNotEscapeIntoFormalCommand()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero);
            var resource = TargetFactory.Resources(World, new Vector3(20, 0, 0));
            yield return null;
            using var writer = new SceneRaidEvidenceWriter(Output("observer-failure"));
            using var observer = new SceneRaidObserver(writer, new SceneRaidIdentityMap());
            observer.DirectiveObserved += (_, __) => throw new System.InvalidOperationException("ProbeFailureInjection");
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(resource, "1", out var request), Is.True);
            Assert.That(agent.DirectiveLifecycle.Active?.CommandId, Is.EqualTo(request.CommandId));
            Assert.That(observer.ProbeFailure, Does.Contain("ProbeFailureInjection"));
            writer.Flush();
            Assert.That(ReadEvents(Output("observer-failure")).Count(x => x.kind == "directive.Accepted"), Is.EqualTo(1));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator EvidenceTracksSupersededAndAsyncCompletedCommands()
        {
            TestNavMeshBuilder.Flat(World);
            var agent = AgentFactory.Create(World, "1", Vector3.zero, 8, false, false);
            var resource = TargetFactory.Resources(World, new Vector3(-30, 0, 0));
            var enemy = EnemyFactory.Passive(World, new Vector3(14, 0, 0), 50);
            var target = TargetFactory.Enemies(World, enemy);
            yield return null;
            Time.timeScale = 4;
            string output = Output("async-evidence");
            using var writer = new SceneRaidEvidenceWriter(output);
            var identity = new SceneRaidIdentityMap();
            using var observer = new SceneRaidObserver(writer, identity);
            var model = new SceneRaidReadModel(identity, observer.LatestResource);
            using var evidence = new SceneRaidCommandEvidence(output, "Harness", writer, observer, model, identity);
            var old = evidence.Submit(Step("old"), resource, null);
            var current = evidence.Submit(Step("current"), target, null);
            Assert.That(evidence.Latest(old.commandId).stage, Is.EqualTo("Cancelled"));
            Assert.That(evidence.Latest(old.commandId).attemptId, Is.EqualTo(old.attemptId));
            yield return RuntimeWait.Until(() => evidence.Latest(current.commandId)?.stage == "Completed", "asynchronous evidence of real kill", 8);
            Assert.That(enemy == null || !enemy.IsAlive, Is.True);
            Assert.That(evidence.Latest(current.commandId).attemptId, Is.EqualTo(current.attemptId));
            Assert.That(evidence.Latest(current.commandId).eventSequence, Is.GreaterThan(current.outcomeSequence));
            evidence.Dispose();
            int before = writer.Count;
            Assert.That(new AgentTargetCommandDispatcher().TrySubmitClusterCommand(resource, agent.AgentIdValue, out _), Is.True);
            writer.Flush();
            Assert.That(ReadEvents(output)
                .Where(x => x.sequence > before).Any(x => x.kind == "command.feedback"), Is.False, "Disposed evidence must unsubscribe.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator CarriedProbeReadsLiveAndStoredInventoryWithoutMutation()
        {
            TestNavMeshBuilder.Flat(World);
            AgentFactory.Create(World, "1", Vector3.zero);
            AgentFactory.Create(World, "2", new Vector3(0, 0, 8));
            var screen = InventoryFactory.Create(World);
            var item = World.Own(ScriptableObject.CreateInstance<InventoryItemData>());
            item.ItemID = "ReadonlyProbe"; item.Width = 1; item.Height = 1; item.IsStackable = true; item.MaxStack = 10;
            yield return null;
            var registry = AgentRuntimeRegistry.ActiveInstance;
            Assert.That(registry.TrySetFocusedAgent("2"), Is.True);
            Assert.That(InventoryItemFactory.Instance.SpawnItemInGrid(item, screen.BackpackGrid, 0, 0, 3), Is.Not.Null);
            Assert.That(registry.TrySetFocusedAgent("1"), Is.True);
            Assert.That(InventoryItemFactory.Instance.SpawnItemInGrid(item, screen.BackpackGrid, 0, 0, 2), Is.Not.Null);
            Time.timeScale = 4;
            screen.OpenInventorySession(new InventoryScreenSessionContext { ExternalColumns = 2, ExternalRows = 2 });
            var session = screen.ActiveSessionContext;
            var snapshots = RuntimeFixtureAccess.Read<IDictionary>(screen, "_inventorySnapshotsByAgentId");
            var saved = snapshots["2"];
            var live = SceneRaidCarriedInventoryEvidence.Capture(screen, "1");
            var stored = SceneRaidCarriedInventoryEvidence.Capture(screen, "2");
            Assert.That(live.available && stored.available, Is.True);
            Assert.That(live.backpack.Single(x => x.itemId == item.ItemID).amount, Is.EqualTo(2));
            Assert.That(stored.backpack.Single(x => x.itemId == item.ItemID).amount, Is.EqualTo(3));
            stored.backpack[0].amount = 99;
            Assert.That(SceneRaidCarriedInventoryEvidence.Capture(screen, "2").backpack.Single(x => x.itemId == item.ItemID).amount, Is.EqualTo(3));
            Assert.That(SceneRaidCarriedInventoryEvidence.Capture(screen, "missing").available, Is.False);
            Assert.That(snapshots.Count, Is.EqualTo(2));
            Assert.That(snapshots["2"], Is.SameAs(saved));
            Assert.That(screen.ActiveSessionContext, Is.SameAs(session));
            Assert.That(session.IsClosed, Is.False);
            Assert.That(screen.IsInventoryOpen, Is.True);
            Assert.That(screen.ActiveInventoryAgentId, Is.EqualTo("1"));
            Assert.That(registry.FocusedAgentId.Value, Is.EqualTo("1"));
            Assert.That(Time.timeScale, Is.Zero);
            screen.CloseInventory();
            Assert.That(Time.timeScale, Is.EqualTo(4));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator CatalogCaptureDoesNotChangeCommandsNavigationOrTargetState()
        {
            TestNavMeshBuilder.Flat(World);
            var pawn = AgentFactory.Create(World, "Catalog", Vector3.zero);
            var resource = TargetFactory.Resources(World, new Vector3(8, 0, 0), new Vector3(14, 0, 0));
            var exit = TargetFactory.Extraction(World, new Vector3(18, 0, 0));
            var enemy = EnemyFactory.Passive(World, new Vector3(0, 0, 10));
            TargetFactory.Enemies(World, enemy);
            World.Root("Excluded source").AddComponent<EnemySourceClusterAuthoring>();
            yield return null;
            var nav = pawn.NavMeshAgent;
            nav.SetDestination(new Vector3(4, 0, 0));
            yield return null;
            var position = pawn.Position; var destination = nav.destination; var corners = nav.path.corners;
            var focus = AgentRuntimeRegistry.ActiveInstance.FocusedAgentId;
            int registered = GameplayTargetRegistry.ActiveInstance.ClusterCount;
            bool touched = resource.HasBeenTouched, completed = resource.HasBeenCompleted;
            var command = pawn.DirectiveLifecycle.Active;
            var catalog = new SceneRaidClusterCatalog(new SceneRaidIdentityMap());
            var first = catalog.Capture(); var second = catalog.Capture();
            Assert.That(JsonUtility.ToJson(second), Is.EqualTo(JsonUtility.ToJson(first)));
            // 来源群启用时还会创建配套活跃群；只排除来源群本身，不能丢掉合法的空活跃群。
            Assert.That(first.clusters.Count, Is.EqualTo(registered - 1));
            Assert.That(first.clusters.Any(x => x.kind == "EnemySource"), Is.False);
            Assert.That(first.clusters.Single(x => x.kind == "Resource").members.Count, Is.EqualTo(2));
            Assert.That(pawn.Position, Is.EqualTo(position));
            Assert.That(nav.destination, Is.EqualTo(destination));
            Assert.That(nav.path.corners, Is.EqualTo(corners));
            Assert.That(pawn.DirectiveLifecycle.Active, Is.EqualTo(command));
            Assert.That(AgentRuntimeRegistry.ActiveInstance.FocusedAgentId, Is.EqualTo(focus));
            Assert.That(GameplayTargetRegistry.ActiveInstance.ClusterCount, Is.EqualTo(registered));
            Assert.That(resource.HasBeenTouched, Is.EqualTo(touched));
            Assert.That(resource.HasBeenCompleted, Is.EqualTo(completed));
            Assert.That(resource.ResourceMembers.All(x => !x.HasBeenCompleted), Is.True);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator CatalogSeparatesDistanceFromReachability()
        {
            TestNavMeshBuilder.Build(World,
                new Bounds(new Vector3(0, -0.1f, 0), new Vector3(20, 0.2f, 20)),
                new Bounds(new Vector3(100, -0.1f, 0), new Vector3(20, 0.2f, 20)));
            AgentFactory.Create(World, "Catalog", Vector3.zero);
            TargetFactory.Extraction(World, new Vector3(100, 0, 0));
            yield return null;
            var capture = new SceneRaidClusterCatalog(new SceneRaidIdentityMap()).Capture();
            var approach = capture.clusters.Single().members.Single().approaches.Single();
            Assert.That(approach.sampled, Is.True);
            Assert.That(approach.completePath, Is.False);
            Assert.That(approach.distancePlanar, Is.EqualTo(100).Within(0.1));
            Assert.That(SceneRaidClusterCatalog.ClassifyDistance(10, 30, 2), Is.EqualTo("Near"));
            Assert.That(SceneRaidClusterCatalog.ClassifyDistance(60, 30, 2), Is.EqualTo("Far"));
            Assert.That(SceneRaidClusterCatalog.ClassifyDistance(45, 30, 2), Is.EqualTo("Middle"));
            Assert.That(SceneRaidClusterCatalog.ClassifyDistance(1, 30, 2), Is.EqualTo("Interaction"));
            Assert.That(SceneRaidClusterCatalog.ClassifyDistance(10, 0, 2), Is.EqualTo("UndefinedRange"));
            ContractCompleted = true;
        }
    }
}
