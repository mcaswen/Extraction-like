using System;
using System.Collections.Generic;

namespace AnomalySearch.Editor.MapGraph
{
    public sealed class MapGraphValidationIssue
    {
        public string Code { get; }
        public string SubjectId { get; }
        public string RelatedId { get; }
        public string Detail { get; }
        public bool IsError { get; }
        public MapGraphValidationIssue(string code, string subject, string related = "", string detail = "", bool error = true)
        { Code = code; SubjectId = subject; RelatedId = related; Detail = detail; IsError = error; }
        public override string ToString() => Code + ":" + SubjectId + (string.IsNullOrEmpty(RelatedId) ? "" : ":" + RelatedId) + " " + Detail;
    }

    /// <summary>错误禁止应用；警告保留真实断连/人工排除事实，不伪装为几何失败。</summary>
    public sealed class MapGraphValidationResult
    {
        public IReadOnlyList<MapGraphValidationIssue> Issues { get; }
        public int ErrorCount { get; }
        public bool IsValid => ErrorCount == 0;
        public MapGraphValidationResult(IEnumerable<MapGraphValidationIssue> issues)
        {
            var copy = new List<MapGraphValidationIssue>(issues ?? throw new ArgumentNullException(nameof(issues)));
            copy.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.Code, b.Code);
                if (c == 0) c = string.CompareOrdinal(a.SubjectId, b.SubjectId);
                return c == 0 ? string.CompareOrdinal(a.RelatedId, b.RelatedId) : c;
            });
            Issues = copy.AsReadOnly();
            foreach (var issue in copy) if (issue.IsError) ErrorCount++;
        }
        public static MapGraphValidationResult Combine(params MapGraphValidationResult[] reports)
        {
            var issues = new List<MapGraphValidationIssue>();
            foreach (var report in reports) if (report != null) issues.AddRange(report.Issues);
            return new MapGraphValidationResult(issues);
        }
    }
}
