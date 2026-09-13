using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEngine;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphConnectionSelectionTests
    {
        [SetUp] public void SetUp() { TestRunContext.Load(); CaseArtifactWriter.Trace("setup", "Ordered canvas endpoint state."); }
        [TearDown] public void TearDown() => CaseArtifactWriter.Complete("COMPLETED");
        [Test] public void PlainThenShiftAndTwoShiftClicksPreserveEndpointOrder()
        {
            var graph = MapGraphGridPlacementTests.Fixture(); var state = new MapGraphConnectionSelection();
            Assert.That(state.Pick(graph, "b", false, 1), Is.False); Assert.That(state.Pick(graph, "a", true, 1), Is.True);
            Assert.That(state.FromNodeId, Is.EqualTo("b")); Assert.That(state.ToNodeId, Is.EqualTo("a")); Assert.That(state.RebindEdgeId, Is.Empty);
            Assert.That(state.TryGetAxis(graph, out var axis), Is.True); Assert.That(axis, Is.EqualTo(MapGraphAxis.Horizontal));
            state.Cancel(); Assert.That(state.Pick(graph, "a", true, 1), Is.False); Assert.That(state.Pick(graph, "c", true, 1), Is.True);
            Assert.That(state.FromNodeId, Is.EqualTo("a")); Assert.That(state.ToNodeId, Is.EqualTo("c"));
        }
        [Test] public void FailedPairKeepsStartAndThirdShiftReplacesEndWithoutSelfLoops()
        {
            var graph = MapGraphGridPlacement.MoveNode(MapGraphGridPlacementTests.Fixture(), "b", new Vector2(0, 160), 80);
            var state = new MapGraphConnectionSelection(); state.Pick(graph, "a", true, 1); state.Pick(graph, "b", true, 1);
            Assert.That(state.TryGetAxis(graph, out _), Is.False); Assert.That(state.HasPair, Is.True); StringAssert.Contains("同行", state.Failure);
            Assert.That(state.Pick(graph, "a", true, 1), Is.False); Assert.That(state.FromNodeId, Is.EqualTo("a")); Assert.That(state.ToNodeId, Is.EqualTo("b"));
            Assert.That(state.Pick(graph, "c", true, 1), Is.True); Assert.That(state.FromNodeId, Is.EqualTo("a")); Assert.That(state.ToNodeId, Is.EqualTo("c"));
        }
        [Test] public void RebindingRequiresExplicitModeAndCancelClearsIt()
        {
            var graph = MapGraphGridPlacementTests.Fixture(); var state = new MapGraphConnectionSelection();
            state.BeginRebind(graph, "ab", 10); Assert.That(state.RebindEdgeId, Is.EqualTo("ab")); Assert.That(state.HasPair, Is.False);
            state.Pick(graph, "b", false, 10); state.Pick(graph, "a", true, 10); Assert.That(state.RebindEdgeId, Is.EqualTo("ab"));
            state.Cancel(); Assert.That(state.RebindEdgeId, Is.Empty); Assert.That(state.HasPair, Is.False);
            state.Pick(graph, "b", false, 10); state.Pick(graph, "c", true, 10); Assert.That(state.RebindEdgeId, Is.Empty);
        }
        [Test] public void RevisionAndMissingIdentityInvalidateTransientEndpoints()
        {
            var graph = MapGraphGridPlacementTests.Fixture(); var state = new MapGraphConnectionSelection();
            state.Pick(graph, "a", true, 1); state.Pick(graph, "b", true, 1); state.Synchronize(graph, 2);
            Assert.That(state.FromNodeId, Is.Empty); Assert.That(state.HasPair, Is.False);
            state.BeginRebind(graph, "ab", 2); state.Synchronize(MapGraphEditOperations.DeleteEdge(graph, "ab"), 2);
            Assert.That(state.RebindEdgeId, Is.Empty); state.Pick(graph, "a", true, 2); state.Synchronize(null, 2); Assert.That(state.FromNodeId, Is.Empty);
        }
    }
}
