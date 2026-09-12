using System;
using System.Collections;
using System.Collections.Generic;
using AgentReproduction.Infrastructure;
using Gameplay.MapGraph.Config;
using Gameplay.MapGraph.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphPathfindingTests : ReproductionTestFixture
    {
        private static MapGraphNodeDefinition Node(string id, float x = 0f)
            => new MapGraphNodeDefinition(id, MapGraphNodeKind.Resource, new Vector2(x, 0f));
        private static MapGraphEdgeDefinition Edge(string a, string b, float length = 1f, string id = null)
            => new MapGraphEdgeDefinition(id ?? a + b, a, b, length);
        private static MapGraphCostSnapshot Costs(params MapGraphEdgeCost[] values)
            => new MapGraphCostSnapshot("test", 1, values);

        [UnityTest]
        public IEnumerator NavigationCostsOverrideDrawingAndLegacyLengths()
        {
            var graph = new MapGraphService(new[] { Node("a"), Node("b", 1000), Node("c", 1) },
                new[] { Edge("a", "b", 100), Edge("b", "c", 100), Edge("a", "c", 1) });
            var search = new MapGraphPathfindingService(graph);
            Assert.That(search.ResolveFromNode("a", "c").RemainingNodeIds, Is.EqualTo(new[] { "c" }));
            var cost = Costs(new MapGraphEdgeCost("ab", 2, 2), new MapGraphEdgeCost("bc", 3, 3),
                new MapGraphEdgeCost("ac", 40, 40));
            var result = search.ResolveFromNode("a", "c", cost);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.RemainingNodeIds, Is.EqualTo(new[] { "b", "c" }));
            Assert.That(result.TotalEstimatedLengthUnits, Is.EqualTo(5));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator ReverseTraversalUsesItsOwnCostAndDirection()
        {
            var graph = new MapGraphService(new[] { Node("a"), Node("b") }, new[] { Edge("a", "b") });
            var search = new MapGraphPathfindingService(graph, Costs(new MapGraphEdgeCost("ab", 2, 7)));
            Assert.That(search.ResolveFromNode("a", "b").TotalEstimatedLengthUnits, Is.EqualTo(2));
            Assert.That(search.ResolveFromNode("b", "a").TotalEstimatedLengthUnits, Is.EqualTo(7));
            Assert.That(search.ResolveFromNode("b", "a").RemainingNodeIds, Is.EqualTo(new[] { "a" }));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator UnknownEndpointsAndDisconnectedGraphsNeverYieldPartialRoutes()
        {
            var search = new MapGraphPathfindingService(new MapGraphService(new[] { Node("a"), Node("b") },
                Array.Empty<MapGraphEdgeDefinition>()));
            foreach (var pair in new[] { ("a", "b"), ("missing", "missing"), ("a", "missing"), ("missing", "a"), ("", "a") })
            {
                var result = search.ResolveFromNode(pair.Item1, pair.Item2);
                Assert.That(result.IsValid, Is.False);
                Assert.That(result.RemainingNodeIds, Is.Empty);
            }
            var same = search.ResolveFromNode("a", "a");
            Assert.That(same.IsValid, Is.True);
            Assert.That(same.RemainingNodeIds, Is.Empty);
            Assert.That(same.TotalEstimatedLengthUnits, Is.Zero);
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator InvalidOrMissingCostsAreUnavailableEvenFromCustomProviders()
        {
            var edge = Edge("a", "b");
            var graph = new MapGraphService(new[] { Node("a"), Node("b") }, new[] { edge });
            var search = new MapGraphPathfindingService(graph);
            foreach (float value in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Assert.That(search.ResolveFromNode("a", "b", new ConstantCost(value)).IsValid, Is.False);
                Assert.That(Costs(new MapGraphEdgeCost("ab", value, value)).TryGetCost(edge, "a", out _), Is.False);
            }
            Assert.That(search.ResolveFromNode("a", "b", Costs()).IsValid, Is.False);
            Assert.That(search.ResolveFromNode("a", "b", null).IsValid, Is.False);
            Assert.That(Costs(new MapGraphEdgeCost("ab", 1, 1)).TryGetCost(edge, "foreign", out _), Is.False);
            ContractCompleted = true;
            yield break;
        }

        private sealed class ConstantCost : IMapGraphCostProvider
        {
            private readonly float _cost;
            public ConstantCost(float cost) { _cost = cost; }
            public bool TryGetCost(MapGraphEdgeDefinition edge, string from, out float cost) { cost = _cost; return true; }
        }

        [UnityTest]
        public IEnumerator EqualCostsAreStableAcrossNodeAndEdgeOrder()
        {
            var nodes = new[] { Node("s"), Node("b"), Node("a"), Node("t") };
            var edges = new[] { Edge("s", "b"), Edge("b", "t"), Edge("s", "a"), Edge("a", "t") };
            for (int permutation = 0; permutation < 4; permutation++)
            {
                if ((permutation & 1) != 0) Array.Reverse(nodes);
                if ((permutation & 2) != 0) Array.Reverse(edges);
                var result = new MapGraphPathfindingService(new MapGraphService(nodes, edges)).ResolveFromNode("s", "t");
                Assert.That(result.RemainingNodeIds, Is.EqualTo(new[] { "a", "t" }));
            }
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator ZeroCostCyclesTerminateWithSimpleShortestPaths()
        {
            var graph = new MapGraphService(new[] { Node("a"), Node("b"), Node("c"), Node("d") },
                new[] { Edge("a", "b"), Edge("b", "c"), Edge("a", "c"), Edge("c", "d") });
            var search = new MapGraphPathfindingService(graph, Costs(new MapGraphEdgeCost("ab", 0, 0),
                new MapGraphEdgeCost("bc", 0, 0), new MapGraphEdgeCost("ac", 0, 0), new MapGraphEdgeCost("cd", 2, 2)));
            var result = search.ResolveFromNode("a", "d");
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.TotalEstimatedLengthUnits, Is.EqualTo(2));
            Assert.That(new HashSet<string>(result.RemainingNodeIds).Count, Is.EqualTo(result.RemainingNodeIds.Count));
            Assert.That(result.RemainingNodeIds[result.RemainingNodeIds.Count - 1], Is.EqualTo("d"));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator MalformedTopologyIsDiagnosedRatherThanOverwritten()
        {
            var validNodes = new[] { Node("a"), Node("b"), Node("c") };
            var graphs = new[]
            {
                new MapGraphService(new[] { Node("a"), Node("a") }, Array.Empty<MapGraphEdgeDefinition>()),
                new MapGraphService(validNodes, new[] { Edge("a", "a") }),
                new MapGraphService(validNodes, new[] { Edge("a", "missing") }),
                new MapGraphService(validNodes, new[] { Edge("a", "b"), Edge("b", "c", id: "ab") }),
                new MapGraphService(validNodes, new[] { Edge("a", "b"), Edge("b", "a") }),
                new MapGraphService(new[] { Node(" ") }, Array.Empty<MapGraphEdgeDefinition>()),
                new MapGraphService(validNodes, Array.Empty<MapGraphEdgeDefinition>(), "missing"),
                new MapGraphService((SO_MapGraphDefinition)null)
            };
            foreach (var graph in graphs)
            {
                Assert.That(graph.IsValid, Is.False);
                Assert.That(graph.ValidationErrors, Is.Not.Empty);
                Assert.That(new MapGraphPathfindingService(graph).ResolveFromNode("a", "a").IsValid, Is.False);
            }
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator InputCollectionsAndLaterQueriesCannotMutateSnapshots()
        {
            var nodes = new List<MapGraphNodeDefinition> { Node("a"), Node("b"), Node("c") };
            var edges = new List<MapGraphEdgeDefinition> { Edge("a", "b"), Edge("b", "c") };
            var values = new List<MapGraphEdgeCost> { new MapGraphEdgeCost("ab", 1, 1), new MapGraphEdgeCost("bc", 2, 2) };
            var graph = new MapGraphService(nodes, edges);
            var cost = new MapGraphCostSnapshot("profile", 17, values);
            nodes.Clear(); edges.Clear(); values.Clear();
            Assert.That(cost.ProfileId, Is.EqualTo("profile"));
            Assert.That(cost.Revision, Is.EqualTo(17));
            var search = new MapGraphPathfindingService(graph, cost);
            var first = search.ResolveFromNode("a", "c");
            search.ResolveFromNode("c", "a");
            Assert.That(first.RemainingNodeIds, Is.EqualTo(new[] { "b", "c" }));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)first.RemainingNodeIds).Add("bad"));
            Assert.Throws<NotSupportedException>(() => ((IList<MapGraphEdgeDefinition>)graph.GetConnectedEdges("a")).Clear());
            var sourcePath = new List<string> { "b" };
            var copied = MapGraphResolvedPathPlan.FromNodePath("b", sourcePath, 1);
            sourcePath.Clear();
            Assert.That(copied.RemainingNodeIds, Is.EqualTo(new[] { "b" }));
            Assert.Throws<ArgumentException>(() => Costs(new MapGraphEdgeCost("ab", 1, 1), new MapGraphEdgeCost("ab", 2, 2)));
            ContractCompleted = true;
            yield break;
        }

        [UnityTest]
        public IEnumerator SmallGraphCostsMatchIndependentSimplePathEnumeration()
        {
            var random = new System.Random(731);
            for (int sample = 0; sample < 20; sample++)
            {
                const int count = 5;
                var nodes = new MapGraphNodeDefinition[count];
                var edges = new List<MapGraphEdgeDefinition>();
                var costs = new List<MapGraphEdgeCost>();
                var matrix = new float[count, count];
                for (int i = 0; i < count; i++)
                {
                    nodes[i] = Node(i.ToString(), random.Next(1000));
                    for (int j = 0; j < count; j++) matrix[i, j] = float.PositiveInfinity;
                }
                for (int i = 0; i < count; i++) for (int j = i + 1; j < count; j++)
                {
                    if (random.Next(3) == 0) continue;
                    var edge = Edge(i.ToString(), j.ToString(), random.Next(1, 100));
                    edges.Add(edge);
                    matrix[i, j] = random.Next(5); matrix[j, i] = random.Next(5);
                    costs.Add(new MapGraphEdgeCost(edge.EdgeId, matrix[i, j], matrix[j, i]));
                }
                var search = new MapGraphPathfindingService(new MapGraphService(nodes, edges), new MapGraphCostSnapshot("oracle", sample, costs));
                for (int start = 0; start < count; start++) for (int target = 0; target < count; target++)
                {
                    float expected = Enumerate(matrix, start, target, new bool[count]);
                    var result = search.ResolveFromNode(start.ToString(), target.ToString());
                    Assert.That(result.IsValid, Is.EqualTo(!float.IsInfinity(expected)), $"sample={sample}, {start}->{target}");
                    if (!result.IsValid) continue;
                    float actual = 0; int previous = start;
                    foreach (string node in result.RemainingNodeIds)
                    {
                        int next = int.Parse(node); actual += matrix[previous, next]; previous = next;
                    }
                    Assert.That(previous, Is.EqualTo(target));
                    Assert.That(result.TotalEstimatedLengthUnits, Is.EqualTo(expected));
                    Assert.That(actual, Is.EqualTo(expected));
                }
            }
            ContractCompleted = true;
            yield break;
        }

        private static float Enumerate(float[,] matrix, int current, int target, bool[] seen)
        {
            if (current == target) return 0;
            seen[current] = true;
            float best = float.PositiveInfinity;
            for (int next = 0; next < seen.Length; next++)
                if (!seen[next] && !float.IsInfinity(matrix[current, next]))
                    best = Mathf.Min(best, matrix[current, next] + Enumerate(matrix, next, target, seen));
            seen[current] = false;
            return best;
        }

        [UnityTest]
        public IEnumerator ExistingMvpAssetRemainsQueryableWithoutNavigationCosts()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SO_MapGraphDefinition>("Assets/SO/MapGraph/SO_MapGraphDefinition_MVP_Graph.asset");
            Assert.That(asset, Is.Not.Null);
            var graph = new MapGraphService(asset);
            Assert.That(graph.IsValid, Is.True, string.Join(",", graph.ValidationErrors));
            Assert.That(graph.OrderedNodeIds.Count, Is.EqualTo(25));
            var search = new MapGraphPathfindingService(graph);
            foreach (string node in graph.OrderedNodeIds)
                Assert.That(search.ResolveFromNode(graph.GetStartNodeId(), node).IsValid, Is.True, node);
            ContractCompleted = true;
            yield break;
        }
    }
}
