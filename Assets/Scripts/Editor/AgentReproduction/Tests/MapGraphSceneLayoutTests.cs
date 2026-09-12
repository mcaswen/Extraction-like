using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphSceneLayoutTests
    {
        [SetUp] public void SetUp()
        {
            TestRunContext.Load();
            Assert.That(Application.isPlaying, Is.False);
            Assert.That(Application.companyName, Is.EqualTo("AnomalySearch.Automation"));
            CaseArtifactWriter.Trace("setup", "Read-only actual scene layout diagnostic; fixed MST is only a seed.");
        }
        [TearDown] public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            NavMesh.RemoveAllNavMeshData(); CaseArtifactWriter.Complete("COMPLETED");
        }

        [UnityTest] public IEnumerator ActualSceneFixedSeedIsMeasuredAndBounded()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Scene_DB/Scenezl_Final 1.unity");
            yield return null; Physics.SyncTransforms();
            var snapshot = MapGraphSceneCollector.Capture(scene);
            var scan = new MapGraphSceneNavigationScan(snapshot);
            while (!scan.IsComplete) { scan.Advance(12); yield return null; }
            var settings = new MapGraphGenerationSettings();
            var reference = MapGraphLayoutGenerator.CreateReference(snapshot, settings);
            var seeds = BuildSeed(reference, scan, snapshot.Profiles.Count);
            var solver = new MapGraphOrthogonalLayoutSolver(reference, seeds, settings);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!solver.IsComplete && timer.Elapsed.TotalSeconds < 90)
            { solver.Advance(32); yield return null; }
            var evidence = new Evidence
            {
                sceneFingerprint = snapshot.SceneFingerprint, navigationFingerprint = snapshot.NavigationFingerprint,
                status = solver.Result != null ? "FIXED_SEED_FEASIBLE" : solver.IsComplete ? "FIXED_SEED_UNRESOLVED" : "SOLVER_TIMEOUT",
                complete = solver.IsComplete, budgetExhausted = solver.BudgetExhausted, states = solver.SearchStates,
                feasibleCandidates = solver.FeasibleLayouts, milliseconds = solver.ElapsedMilliseconds,
                seedEdges = seeds, navigation = scan.Connections.Select(c => c.Edge).ToList(),
                failures = solver.FailureCounts.OrderByDescending(p => p.Value).Select(p => p.Key + "=" + p.Value).ToList()
            };
            if (solver.Result != null)
            {
                var layout = solver.Result;
                evidence.zones = layout.Zones.ToList(); evidence.nodes = layout.Nodes.ToList(); evidence.edges = layout.Edges.ToList(); evidence.constraints = layout.Constraints;
                var score = MapGraphLayoutScore.Evaluate(layout, reference, settings);
                evidence.score = score.Total; evidence.reversals = score.DirectionReversals; evidence.crossings = score.Crossings;
                evidence.geometryIssues = MapGraphValidation.Validate(layout).Issues.Select(i => i.ToString()).ToList();
                foreach (var profile in snapshot.Profiles)
                    evidence.navigationIssues.AddRange(MapGraphNavigationValidation.Validate(layout,
                        scan.Connections.Where(c => c.ProfileId == profile.Data.ProfileId).Select(c => c.Edge).ToArray()).Issues.Select(i => i.ToString()));
            }
            File.WriteAllText(Path.Combine(TestRunContext.Load().outputPath, "map-seed-layout.json"), JsonUtility.ToJson(evidence, true));
            CaseArtifactWriter.Trace("fixed-seed", evidence.status + "; states=" + evidence.states + "; ms=" + evidence.milliseconds);
            Assert.That(solver.IsComplete, Is.True, "Bounded seed diagnostic exceeded the wall-clock deadline.");
            Assert.That(solver.SearchStates, Is.LessThanOrEqualTo(settings.MaximumSearchStates));
            Assert.That(snapshot.Nodes.Count, Is.EqualTo(28)); Assert.That(seeds.Count, Is.EqualTo(26));
            if (solver.Result != null)
            {
                Assert.That(MapGraphValidation.Validate(solver.Result).IsValid, Is.True);
                foreach (var profile in snapshot.Profiles)
                    Assert.That(MapGraphNavigationValidation.Validate(solver.Result,
                        scan.Connections.Where(c => c.ProfileId == profile.Data.ProfileId).Select(c => c.Edge).ToArray()).IsValid, Is.True);
            }
            // 此测试证明真实输入和预算诊断完整；没有候选时不能解释为联合生成通过或数学无解。
        }

        private static List<MapGraphEdgeDefinition> BuildSeed(MapGraphLayoutDraft reference, MapGraphSceneNavigationScan scan, int profiles)
        {
            var parent = reference.Nodes.ToDictionary(n => n.NodeId, n => n.NodeId);
            var candidates = scan.Connections.GroupBy(c => c.Edge.EdgeId)
                .Where(g => g.Count() == profiles && g.All(c => MapGraphNavigationValidation.Usable(c.Edge)))
                .OrderBy(g => g.Max(c => (double)c.Edge.ForwardLength + c.Edge.ReverseLength)).ThenBy(g => g.Key, StringComparer.Ordinal);
            var result = new List<MapGraphEdgeDefinition>();
            foreach (var candidate in candidates)
            {
                var edge = candidate.First().Edge;
                string a = Root(edge.FromNodeId), b = Root(edge.ToNodeId); if (a == b) continue;
                parent[a] = b;
                result.Add(new MapGraphEdgeDefinition(edge.EdgeId, edge.FromNodeId, edge.ToNodeId, 1, origin: MapGraphEdgeOrigin.Generated));
            }
            return result;
            string Root(string node) { while (parent[node] != node) node = parent[node]; return node; }
        }

        [Serializable] private sealed class Evidence
        {
            public string status, sceneFingerprint, navigationFingerprint;
            public bool complete, budgetExhausted;
            public int states, feasibleCandidates, reversals, crossings;
            public double milliseconds, score;
            public List<string> failures, geometryIssues;
            public List<string> navigationIssues = new List<string>();
            public List<MapGraphZoneDefinition> zones;
            public List<MapGraphNodeDefinition> nodes;
            public List<MapGraphEdgeDefinition> edges, seedEdges;
            public MapGraphLayoutConstraints constraints;
            public List<MapGraphNavigationEdgeBake> navigation;
        }
    }
}
