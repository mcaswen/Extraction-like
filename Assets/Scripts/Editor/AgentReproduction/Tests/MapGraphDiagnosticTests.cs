using System.Linq;
using AgentReproduction.Infrastructure;
using AgentReproduction.Reporting;
using AnomalySearch.Editor.MapGraph;
using Gameplay.MapGraph.Config;
using NUnit.Framework;
using UnityEngine;

namespace AgentReproduction.Tests
{
    public sealed class MapGraphDiagnosticTests
    {
        [SetUp] public void SetUp() { TestRunContext.Load(); CaseArtifactWriter.Trace("setup", "Construct name, icon and alignment conflicts without a scene or navigation query."); }
        [TearDown] public void TearDown() => CaseArtifactWriter.Complete("COMPLETED");
        private static MapGraphLayoutDraft Fixture()
        {
            return MapGraphGridPlacement.WithPositions(new MapGraphLayoutDraft(
                new[] { new MapGraphZoneDefinition("zone_test", "员工食堂", new Rect(-320, -240, 640, 480), new Vector2(100, 28)) },
                new[] { new MapGraphNodeDefinition("cluster_left", MapGraphNodeKind.EnemySource, new Vector2(-160, 0), "西侧敌人群", zoneId: "zone_test"),
                    new MapGraphNodeDefinition("cluster_right", MapGraphNodeKind.Extraction, new Vector2(160, 0), "东侧撤离群", zoneId: "zone_test"),
                    new MapGraphNodeDefinition("cluster_middle", MapGraphNodeKind.Resource, Vector2.zero, "中央资源群", zoneId: "zone_test") },
                new[] { new MapGraphEdgeDefinition("edge_cross", "cluster_left", "cluster_right", 10, MapGraphAxis.Horizontal),
                    new MapGraphEdgeDefinition("edge_wrong_axis", "cluster_right", "cluster_middle", 10, MapGraphAxis.Vertical) }), null);
        }
        [Test] public void ScreenshotConflictTypesHaveReadableSubjectsAndActions()
        {
            var layout = Fixture(); string before = layout.ContentFingerprint;
            var issues = MapGraphValidation.Validate(layout).Issues;
            foreach (string code in new[] { "NodeOverName", "EdgeThroughName", "EdgeThroughNode", "NonOrthogonalEdge" })
            {
                var issue = issues.First(i => i.Code == code);
                string text = MapGraphDiagnosticFormatter.Format(issue, layout);
                Assert.That(text, Does.Contain("员工食堂"));
                Assert.That(text, Does.Not.Contain("cluster_").And.Not.Contain("edge_"));
                CaseArtifactWriter.Trace("readable-conflict", issue + "\n" + text);
            }
            Assert.That(layout.ContentFingerprint, Is.EqualTo(before));
        }
        [Test] public void FocusSelectsEndpointsAndNameObstaclesWithoutEditingLayout()
        {
            var layout = Fixture(); var canvas = new MapGraphEditorCanvas(); canvas.Fit(layout, new Rect(0, 0, 900, 600));
            string before = layout.ContentFingerprint;
            var issue = MapGraphValidation.Validate(layout).Issues.First(i => i.Code == "EdgeThroughName");
            Assert.That(canvas.FocusDiagnostic(layout, issue), Is.True);
            Assert.That(canvas.SelectionKind, Is.EqualTo(MapGraphSelectionKind.Edge)); Assert.That(canvas.SelectionId, Is.EqualTo("edge_cross"));
            Assert.That(canvas.FocusedIssue, Is.SameAs(issue));
            Assert.That(layout.ContentFingerprint, Is.EqualTo(before));
            canvas.ClearDiagnostic(); Assert.That(canvas.FocusedIssue, Is.Null);
            Assert.That(canvas.FocusDiagnostic(layout, new MapGraphValidationIssue("Unknown", "removed")), Is.False);
        }
        [Test] public void UnknownDiagnosticsKeepDetailsAndDuplicateNamesStayDistinct()
        {
            var basis = Fixture(); var layout = new MapGraphLayoutDraft(basis.Zones,
                basis.Nodes.Select(n => n.WithSourceLabel("同名群", n.Description)), basis.Edges, basis.Constraints);
            Assert.That(MapGraphDiagnosticFormatter.ObjectName(layout, "cluster_left"), Is.Not.EqualTo(MapGraphDiagnosticFormatter.ObjectName(layout, "cluster_right")));
            var issue = new MapGraphValidationIssue("FutureRule", "removed", detail: "完整底层原因");
            Assert.That(MapGraphDiagnosticFormatter.Format(issue, layout), Does.Contain("FutureRule").And.Contain("removed").And.Contain("完整底层原因"));
            Assert.That(MapGraphDiagnosticFormatter.Format(issue, null), Does.Contain("removed"));
        }
    }
}
