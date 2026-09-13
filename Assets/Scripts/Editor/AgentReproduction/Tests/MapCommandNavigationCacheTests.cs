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
        public IEnumerator RegisteredGeometryOrderDoesNotInvalidateRuntimeBake()
        {
            Build();
            NavMeshData Tile(float x)
            {
                var sources=new System.Collections.Generic.List<NavMeshBuildSource>{new NavMeshBuildSource{
                    shape=NavMeshBuildSourceShape.Box,size=new Vector3(20,.2f,20),
                    transform=Matrix4x4.TRS(new Vector3(x,-.1f,0),Quaternion.identity,Vector3.one),area=0}};
                return World.Own(NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0),sources,
                    new Bounds(new Vector3(x,0,0),new Vector3(24,10,24)),Vector3.zero,Quaternion.identity));
            }
            var first=Tile(100);var second=Tile(140);
            var a=NavMesh.AddNavMeshData(first);var b=NavMesh.AddNavMeshData(second);
            string original=Fingerprint();Bake(original);
            a.Remove();b.Remove();
            b=NavMesh.AddNavMeshData(second);a=NavMesh.AddNavMeshData(first);
            string reordered=Fingerprint();
            AgentReproduction.Reporting.CaseArtifactWriter.Trace("navigation-order","before="+original+"; after="+reordered);
            Assert.That(reordered,Is.EqualTo(original),"同一导航几何重新注册的顺序不应使发布缓存失效");
            Assert.That(Service(reordered).PendingEdgeCount,Is.Zero);
            ContractCompleted=true;yield break;
        }

        [UnityTest]
        public IEnumerator GeometrySignaturePreservesExactGeometryAreasAndWinding()
        {
            var original=new NavMeshTriangulation{vertices=new[]{Vector3.zero,Vector3.right*4,Vector3.forward*4,new Vector3(4,0,4)},
                indices=new[]{0,2,1,1,2,3},areas=new[]{0,2}};
            string signature=MapGraphNavigationGeometrySignature.Capture(original);
            var reordered=new NavMeshTriangulation{vertices=new[]{new Vector3(4,0,4),Vector3.forward*4,Vector3.right*4,Vector3.zero},
                indices=new[]{1,0,2,1,2,3},areas=new[]{2,0}};
            Assert.That(MapGraphNavigationGeometrySignature.Capture(reordered),Is.EqualTo(signature));
            var changed=original;changed.areas=new[]{0,3};
            Assert.That(MapGraphNavigationGeometrySignature.Capture(changed),Is.Not.EqualTo(signature));
            changed=original;changed.indices=new[]{0,1,2,1,2,3};
            Assert.That(MapGraphNavigationGeometrySignature.Capture(changed),Is.Not.EqualTo(signature),"反转绕序仍失效");
            changed=original;changed.vertices=(Vector3[])original.vertices.Clone();changed.vertices[1]+=Vector3.up*.0001f;
            Assert.That(MapGraphNavigationGeometrySignature.Capture(changed),Is.Not.EqualTo(signature),"不能用 Bounds 或量化隐藏几何变化");
            changed=original;changed.indices=new[]{0,2,3,0,3,1};
            Assert.That(MapGraphNavigationGeometrySignature.Capture(changed),Is.Not.EqualTo(signature),"相同顶点的不同三角连接仍失效");
            changed=original;changed.indices=new[]{0,2,1,1,2,3,0,2,1};changed.areas=new[]{0,2,0};
            Assert.That(MapGraphNavigationGeometrySignature.Capture(changed),Is.Not.EqualTo(signature),"重复几何不能被集合去重");
            changed=original;changed.vertices=(Vector3[])original.vertices.Clone();changed.vertices[1]=new Vector3(float.NaN,0,0);
            Assert.That(MapGraphNavigationGeometrySignature.Capture(changed),Is.Empty);
            changed=original;changed.indices=new[]{0,2,99,1,2,3};
            Assert.That(MapGraphNavigationGeometrySignature.Capture(changed),Is.Empty);
            var coincident=new NavMeshTriangulation{vertices=new[]{Vector3.zero,Vector3.right,Vector3.forward,Vector3.zero},
                indices=new[]{0,2,1,0,1,2},areas=new[]{0,0}};
            var split=coincident;split.indices=new[]{0,2,1,3,1,2};
            Assert.That(MapGraphNavigationGeometrySignature.Capture(split),Is.Not.EqualTo(MapGraphNavigationGeometrySignature.Capture(coincident)),
                "相同坐标的独立顶点不可合并，否则会隐藏原有顶点连接关系");
            Assert.That(original.vertices[1],Is.EqualTo(Vector3.right*4));
            ContractCompleted=true;yield break;
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
