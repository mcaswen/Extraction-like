using System;
using System.Collections;
using AgentReproduction.Infrastructure;
using AgentReproduction.World;
using Gameplay.Agent.Navigation;
using Gameplay.MapGraph.Binding;
using Gameplay.MapGraph.Config;
using Gameplay.Targets.Authoring;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapCommandNavigationCacheTests : ReproductionTestFixture
    {
        private MapGraphBindingAuthoring _binding;
        private AgentNavigationProfile _profile;
        private ResourceClusterAuthoring _first;
        private string Fingerprint() => MapGraphNavigationFingerprint.Capture(SceneManager.GetActiveScene());
        private void Build()
        {
            TestNavMeshBuilder.Flat(World);
            _first=TargetFactory.Resources(World,Vector3.zero); var second=TargetFactory.Resources(World,Vector3.right*10);
            _binding=MapRouteFactory.Bind(World,new GameplayTargetClusterAuthoringBase[]{_first,second},
                new[]{Vector3.zero,Vector3.right*10},MapRouteFactory.Chain(2));
            _profile=new AgentNavigationProfile(NavMesh.GetSettingsByIndex(0).agentTypeID,-1,1,1);
        }
        private MapGraphNavigationCostService Service(string signature,AgentNavigationProfile profile=null)
            => new MapGraphNavigationCostService(_binding.MapDefinition,_binding,profile??_profile,"","",signature);
        private void Bake(string signature)
        {
            var service=Service(""); service.ProcessPending(1);
            var definition=_binding.MapDefinition;
            definition.ApplyCommandData(definition.MapId,definition.DisplayName,definition.StartNodeId,definition.Zones,definition.Nodes,
                definition.Edges,definition.LayoutConstraints,service.CreateBakeData("editor-scene","editor-navigation",signature));
            _binding.RebuildIndexes();
        }

        [UnityTest]
        public IEnumerator FingerprintTracksRealNavigationAndLinkConfigurationOnly()
        {
            Build(); string original=Fingerprint(); Assert.That(original.Length,Is.EqualTo(64));
            Assert.That(Fingerprint(),Is.EqualTo(original));
            World.Root("Unrelated cosmetic object").transform.position=Vector3.right*3;
            Assert.That(Fingerprint(),Is.EqualTo(original));
            var link=World.Root("Navigation link").AddComponent<NavMeshLink>();
            string linked=Fingerprint(); Assert.That(linked,Is.Not.EqualTo(original));
            link.bidirectional=false; Assert.That(Fingerprint(),Is.Not.EqualTo(linked));
            string directional=Fingerprint(); link.transform.position+=Vector3.right; link.UpdateLink();
            Assert.That(Fingerprint(),Is.Not.EqualTo(directional));
            string moved=Fingerprint(); link.enabled=false; Assert.That(Fingerprint(),Is.Not.EqualTo(moved));
            NavMesh.RemoveAllNavMeshData(); Assert.That(Fingerprint(),Is.Empty);
            TestNavMeshBuilder.Build(World,new Bounds(new Vector3(10,-0.1f,0),new Vector3(30,0.2f,30)));
            Assert.That(Fingerprint(),Is.Not.EqualTo(original)); ContractCompleted=true; yield break;
        }

        [UnityTest]
        public IEnumerator MatchingRuntimeBakeLoadsWithoutEditorFingerprintsOrQueries()
        {
            Build(); string signature=Fingerprint(); Bake(signature);
            var loaded=Service(Fingerprint());
            Assert.That(loaded.PendingEdgeCount,Is.Zero); Assert.That(loaded.CalculationCount,Is.Zero);
            Assert.That(loaded.Snapshot.TryGetCost(_binding.MapDefinition.Edges[0],"n0",out float cost),Is.True);
            Assert.That(cost,Is.EqualTo(10).Within(0.1));
            Assert.That(loaded.ProcessPending(1),Is.Zero); Assert.That(loaded.CalculationCount,Is.Zero);
            ContractCompleted=true; yield break;
        }

        [UnityTest]
        public IEnumerator MissingOrChangedRuntimeSignatureUsesOnlyBudgetedSavedEdges()
        {
            Build(); Bake(""); var legacy=Service(Fingerprint());
            Assert.That(legacy.PendingEdgeCount,Is.EqualTo(1)); Assert.That(legacy.CalculationCount,Is.Zero);
            Assert.That(legacy.ProcessPending(1),Is.EqualTo(1)); Assert.That(legacy.CalculationCount,Is.EqualTo(2));
            Bake(Fingerprint()); var link=World.Root("Changed link").AddComponent<NavMeshLink>();
            var stale=Service(Fingerprint()); Assert.That(stale.PendingEdgeCount,Is.EqualTo(1));
            Assert.That(stale.Snapshot.Count,Is.Zero); Assert.That(stale.CalculationCount,Is.Zero);
            stale.ProcessPending(1); Assert.That(stale.CalculationCount,Is.EqualTo(2));
            ContractCompleted=true; yield break;
        }

        [UnityTest]
        public IEnumerator RuntimeSignatureCannotHideProfileOrAnchorMismatch()
        {
            Build(); string signature=Fingerprint(); Bake(signature);
            var changed=new AgentNavigationProfile(_profile.AgentTypeId,_profile.AreaMask,2,1);
            Assert.That(Service(signature,changed).PendingEdgeCount,Is.EqualTo(1));
            _first.transform.position+=Vector3.right*2;
            var moved=Service(signature); Assert.That(moved.PendingEdgeCount,Is.EqualTo(1)); Assert.That(moved.Snapshot.Count,Is.Zero);
            moved.ProcessPending(1);
            Assert.That(moved.Snapshot.TryGetCost(_binding.MapDefinition.Edges[0],"n0",out float cost),Is.True);
            Assert.That(cost,Is.EqualTo(8).Within(0.1)); ContractCompleted=true; yield break;
        }

        [UnityTest]
        public IEnumerator ActiveLinksRequireRevalidationOfNativeConnectivity()
        {
            Build(); World.Root("Live navigation link").AddComponent<NavMeshLink>();
            string signature=Fingerprint(); Assert.That(signature,Does.StartWith("live-links:"));
            Bake(signature); var loaded=Service(signature);
            Assert.That(loaded.PendingEdgeCount,Is.EqualTo(1)); Assert.That(loaded.Snapshot.Count,Is.Zero);
            loaded.ProcessPending(1); Assert.That(loaded.CalculationCount,Is.EqualTo(2));
            ContractCompleted=true; yield break;
        }

        [UnityTest]
        public IEnumerator RuntimeFingerprintSurvivesAssetSaveUnloadAndReadback()
        {
            Build(); string signature=Fingerprint(); Bake(signature);
            string path="Assets/RouteCacheProbe_"+Guid.NewGuid().ToString("N")+".asset";
            try {
                var definition=_binding.MapDefinition; AssetDatabase.CreateAsset(definition,path); AssetDatabase.SaveAssets();
                string guid=AssetDatabase.AssetPathToGUID(path); Resources.UnloadAsset(definition);
                var loaded=AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>(path);
                Assert.That(loaded.NavigationBake.RuntimeNavigationFingerprint,Is.EqualTo(signature));
                Assert.That(loaded.NavigationBake.SceneFingerprint,Is.EqualTo("editor-scene"));
                Assert.That(AssetDatabase.AssetPathToGUID(path),Is.EqualTo(guid));
                _binding.Configure(loaded,_binding.TargetBindings,_binding.ZoneBindings);
                Assert.That(Service(Fingerprint()).PendingEdgeCount,Is.Zero);
            } finally { AssetDatabase.DeleteAsset(path); }
            ContractCompleted=true; yield break;
        }
    }
}
