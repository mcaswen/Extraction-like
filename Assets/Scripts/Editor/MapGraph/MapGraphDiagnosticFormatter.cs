using System;
using System.Linq;

namespace AnomalySearch.Editor.MapGraph
{
    /// <summary>只把校验事实解析为作者可读的名称和建议，机器诊断与校验规则保持不变。</summary>
    public static class MapGraphDiagnosticFormatter
    {
        public static string Format(MapGraphValidationIssue issue, MapGraphLayoutDraft layout)
        {
            if (issue == null) return "";
            if (issue.Code.StartsWith("Scene", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(issue.Detail)) return issue.Detail;
            string subject = ObjectName(layout, issue.SubjectId), related = ObjectName(layout, issue.RelatedId);
            string text;
            switch (issue.Code)
            {
                case "NodeOverName": text = $"群「{subject}」压住区域「{related}」的名称避让框。请将群移出框外。"; break;
                case "EdgeThroughName": text = $"连线「{subject}」穿过区域「{related}」的名称避让框。请调整端点所在行/列，或删除该线后重新连接。"; break;
                case "EdgeThroughNode": text = $"连线「{subject}」穿过群「{related}」。可经由这个群分成两条横竖连接，或调整摆放。"; break;
                case "NonOrthogonalEdge": text = $"连线「{subject}」的端点不符合该线的横/竖方向。请对齐端点；旧自动线可通过“生成连接”重建，人工线需调整或重选端点。"; break;
                case "NodeOverlap": text = $"群「{subject}」与「{related}」的图标重叠。请拉开距离。"; break;
                case "NodeOutsideZone": text = $"群「{subject}」超出所属区域「{related}」。请移回区域内，或调整区域边界。"; break;
                case "ZoneOverlap": text = $"区域「{subject}」与「{related}」重叠。请移动或缩小区域。"; break;
                case "NameOutsideZone": text = $"区域「{subject}」容不下名称避让框。请扩大区域。"; break;
                case "PortOverlap": text = $"连线「{subject}」与「{related}」使用了同一群的同向端口。请删除重复方向的连接，或改用相邻群。"; break;
                case "CollinearEdges": text = $"连线「{subject}」与「{related}」重叠。请改为相邻群之间的连接。"; break;
                case "EdgeAlignmentNotShared": text = $"连线「{subject}」的行列约束不一致。请重新生成连接。"; break;
                case "InvalidEdgeSpan": text = $"连线「{subject}」没有足够的显示空间。请拉开端点，或减小两端留白。"; break;
                case "LostReachableConnectivity": text = $"实际可达的群「{subject}」和「{related}」尚未通过地图线路连通。请对齐中间群、补线，或尝试微调建议。"; break;
                case "PinnedConnectionUnavailable": text = $"保留的人工/样式连线「{subject}」不满足导航或禁连约束。请检查对应端点。"; break;
                case "PlacementConnectionsIncomplete": text = "保持当前位置未能生成有效线路。先处理摆放和人工线冲突，再对齐断开的群；也可尝试微调建议。"; break;
                case "PlacementAdjustmentNotFound": text = "有限微调未找到有效方案。下列冲突对应当前摆放，未接受的候选没有应用。"; break;
                case "GeneratedLayoutValidationFailed": text = "线路校验未通过，当前摆放保留，尚未发布。"; break;
                case "NoPublishableLayout": text = "没有可应用的有效线路，当前摆放保留。"; break;
                default: text = $"{issue.Code}：{subject}" + (string.IsNullOrEmpty(related) ? "" : $"；{related}"); break;
            }
            return text + (string.IsNullOrWhiteSpace(issue.Detail) ? "" : "\n" + issue.Detail);
        }

        public static string ObjectName(MapGraphLayoutDraft layout, string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (id == "graph") return "整张地图";
            if (layout == null) return id;
            if (layout.Graph.TryGetNode(id, out var node))
            {
                string name = string.IsNullOrWhiteSpace(node.DisplayName) ? node.NodeId : node.DisplayName;
                if (layout.Nodes.Count(n => n.ZoneId == node.ZoneId && n.DisplayName == node.DisplayName) > 1)
                    name += "（" + id.Substring(Math.Max(0, id.Length - 6)) + "）";
                return layout.Graph.TryGetZone(node.ZoneId, out var owner) ? owner.DisplayName + " / " + name : name;
            }
            if (layout.Graph.TryGetZone(id, out var zone)) return zone.DisplayName;
            if (layout.Graph.TryGetEdge(id, out var edge))
                return ObjectName(layout, edge.FromNodeId) + " ↔ " + ObjectName(layout, edge.ToNodeId);
            return id;
        }
    }
}
