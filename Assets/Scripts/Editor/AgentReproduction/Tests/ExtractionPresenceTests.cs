using System.Collections;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AgentReproduction.World;
using AnomalySearch.Automation.SceneRaid;
using Gameplay.Agent.Core;
using Gameplay.Targets.Authoring;
using Gameplay.Targets.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class ExtractionPresenceTests : ReproductionTestFixture
    {
        public static int[] Speeds = { 1, 4 };
        public static bool[] Destruction = { false, true };
        private AgentPawnRoot _a, _b;
        private ExtractionPointController _point;
        private RaidFlowController _raid;
        private SceneRaidReadModel _model;

        private void CreateStaticBodies(int speed, bool secondInside = true)
        {
            TestNavMeshBuilder.Flat(World);
            _a = AgentFactory.Create(World, "A", new Vector3(-3, 0, 0), 0, false, false);
            _b = AgentFactory.Create(World, "B", new Vector3(3, 0, secondInside ? 0 : 20), 0, false, false);
            // Keep Pawn registration alive so the real inventory owner can initialize both identities.
            // Discovery is disabled and these stationary actors have no directive.
            _a.NavMeshAgent.enabled = _b.NavMeshAgent.enabled = false;
            var cluster = TargetFactory.Extraction(World, Vector3.zero);
            _point = cluster.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>();
            _point.GetComponent<BoxCollider>().size = new Vector3(20, 6, 20);
            _point.ExtractionDurationSeconds = 30;
            _raid = World.Root("Presence raid").AddComponent<RaidFlowController>();
            InventoryFactory.Create(World);
            _model = new SceneRaidReadModel(new SceneRaidIdentityMap(), null);
            Time.timeScale = speed;
            Physics.SyncTransforms();
        }

        private IDictionary Progress => RuntimeFixtureAccess.Read<IDictionary>(_raid, "_activeExtractionProgressByAgentId");
        private float Seconds(string id)
        {
            Assert.That(Progress.Contains(id), Is.True, "Missing extraction presence: " + id);
            return RuntimeFixtureAccess.Read<float>(Progress[id], "ProgressSeconds");
        }
        private IEnumerator Advance(float seconds)
        {
            double end = Time.timeAsDouble + seconds;
            yield return RuntimeWait.Until(() => Time.timeAsDouble >= end, "physics presence advances", 5);
        }
        private void TraceProgress()
        {
            CaseArtifactWriter.Trace("extraction-presence", JsonUtility.ToJson(_model.Capture()));
        }

        [UnityTest]
        public IEnumerator TwoBodiesKeepIndependentProgress([ValueSource(nameof(Speeds))] int speed)
        {
            CreateStaticBodies(speed);
            _point.ExtractionDurationSeconds = 1;
            yield return Advance(0.3f);
            TraceProgress();
            float a = Seconds("A"), b = Seconds("B");
            Assert.That(a, Is.GreaterThan(0.1f));
            Assert.That(b, Is.GreaterThan(0.1f));
            var snapshot = _model.Capture();
            Assert.That(snapshot.extractionProgress.Single(x => x.agent == "A").progressSeconds, Is.EqualTo(a));
            Assert.That(snapshot.extractionProgress.Single(x => x.agent == "B").progressSeconds, Is.EqualTo(b));
            Assert.That(Seconds("A"), Is.EqualTo(a), "Reading the probe must not change progress.");
            yield return RuntimeWait.Until(() => _raid.IsInputLocked, "both physical presences settle", 5);
            Assert.That(_model.Capture().missionCompleted, Is.True);
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, _model.Capture().settledAgents);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator SharedPointCommandsExtractBoth([ValueSource(nameof(Speeds))] int speed)
        {
            TestNavMeshBuilder.Flat(World);
            var a = AgentFactory.Create(World, "A", new Vector3(-8, 0, -14), 8, false, false);
            var b = AgentFactory.Create(World, "B", new Vector3(8, 0, -14), 8, false, false);
            var parent = World.Root("Real extraction prefab", false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Cluster/ExtractionCluster.prefab");
            var instance = Object.Instantiate(prefab, parent.transform);
            instance.transform.localPosition = Vector3.zero;
            parent.SetActive(true);
            var cluster = instance.GetComponent<ExtractionClusterAuthoring>();
            _point = cluster.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>();
            _raid = World.Root("Shared commanded raid").AddComponent<RaidFlowController>();
            InventoryFactory.Create(World);
            _model = new SceneRaidReadModel(new SceneRaidIdentityMap(), null);
            Time.timeScale = speed;
            yield return null;
            var dispatcher = new AgentTargetCommandDispatcher();
            Assert.That(dispatcher.TrySubmitClusterCommand(cluster, a.AgentIdValue, out _), Is.True);
            Assert.That(dispatcher.TrySubmitClusterCommand(cluster, b.AgentIdValue, out _), Is.True);
            try { yield return RuntimeWait.Until(() => _raid.IsInputLocked, "two formal commands share actual prefab", 12); }
            finally { TraceProgress(); }
            Assert.That(_model.Capture().missionCompleted, Is.True);
            CollectionAssert.AreEquivalent(new[] { "A", "B" }, _model.Capture().settledAgents);
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator OtherActorLeavingDoesNotResetRemaining([ValueSource(nameof(Speeds))] int speed)
        {
            CreateStaticBodies(speed);
            yield return Advance(0.3f);
            float before = Seconds("B");
            _a.transform.position = new Vector3(-3, 3, 30); // Fixture movement exercises actual trigger exit.
            Physics.SyncTransforms();
            yield return Advance(0.2f);
            TraceProgress();
            Assert.That(Progress.Contains("A"), Is.False);
            Assert.That(Seconds("B"), Is.GreaterThan(before + 0.1f));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator MultipleCollidersLeaveIndependently([ValueSource(nameof(Speeds))] int speed)
        {
            CreateStaticBodies(speed, false);
            var child = new GameObject("Second player collider");
            child.transform.SetParent(_a.transform, false);
            child.tag = "Player";
            var extra = child.AddComponent<SphereCollider>();
            yield return Advance(0.3f);
            float before = Seconds("A");
            _a.GetComponent<Collider>().enabled = false;
            yield return Advance(0.2f);
            Assert.That(Seconds("A"), Is.GreaterThan(before + 0.1f));
            extra.enabled = false;
            yield return Advance(0.1f);
            Assert.That(Progress.Contains("A"), Is.False);
            extra.enabled = true;
            yield return Advance(0.15f);
            Assert.That(Seconds("A"), Is.LessThan(before), "A new entry starts a new timer.");
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator DisabledPointClearsPresenceAndCanReenter([ValueSource(nameof(Speeds))] int speed)
        {
            CreateStaticBodies(speed);
            yield return Advance(0.3f);
            _point.enabled = false;
            yield return Advance(0.2f);
            Assert.That(Progress.Count, Is.Zero, "Disabled behaviours can still receive physics callbacks.");
            _point.enabled = true;
            yield return Advance(0.3f);
            Assert.That(Seconds("A"), Is.GreaterThan(0.1f));
            Assert.That(Seconds("B"), Is.GreaterThan(0.1f));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator DisabledOrDestroyedBodyOnlyClearsItsActor([ValueSource(nameof(Destruction))] bool destroy)
        {
            CreateStaticBodies(1);
            yield return Advance(0.3f);
            float before = Seconds("B");
            if (destroy) Object.Destroy(_a.gameObject); else _a.gameObject.SetActive(false);
            yield return Advance(0.2f);
            Assert.That(Progress.Contains("A"), Is.False);
            Assert.That(Seconds("B"), Is.GreaterThan(before + 0.1f));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator HorizontalDetectionScaleStillExcludesTheEdge()
        {
            CreateStaticBodies(1);
            _point.DetectionHorizontalScale = 0.5f;
            _a.transform.position = new Vector3(-8, 3, 0);
            Physics.SyncTransforms();
            yield return Advance(0.3f);
            Assert.That(Progress.Contains("A"), Is.False);
            Assert.That(Seconds("B"), Is.GreaterThan(0.1f));
            ContractCompleted = true;
        }

        [UnityTest]
        public IEnumerator LegacyPlayerStillHasOneTimer()
        {
            var cluster = TargetFactory.Extraction(World, Vector3.zero);
            _point = cluster.ExtractionMembers[0].EntityObject.GetComponent<ExtractionPointController>();
            _point.ExtractionDurationSeconds = 30;
            _raid = World.Root("Legacy presence raid").AddComponent<RaidFlowController>();
            var body = World.Root("Legacy body");
            body.tag = "Player";
            body.AddComponent<CapsuleCollider>();
            body.AddComponent<Rigidbody>().isKinematic = true;
            yield return Advance(0.3f);
            Assert.That(Seconds("Player"), Is.GreaterThan(0.1f));
            body.transform.position = Vector3.right * 20;
            Physics.SyncTransforms();
            yield return Advance(0.1f);
            Assert.That(Progress.Count, Is.Zero);
            ContractCompleted = true;
        }
    }
}
