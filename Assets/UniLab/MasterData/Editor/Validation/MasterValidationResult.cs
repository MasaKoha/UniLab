#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace UniLab.MasterData.Editor.Validation
{
    /// <summary>全診断を集約し、構造エラーからの派生診断を抑制する。</summary>
    public sealed class MasterValidationResult
    {
        private readonly List<MasterValidationIssue> _issues = new List<MasterValidationIssue>();
        /// <summary>追加順の診断。外部からの変更は許可しない。</summary>
        public IReadOnlyList<MasterValidationIssue> Issues => _issues.AsReadOnly();
        /// <summary>一件でも中断すべき診断があるか。</summary>
        public bool HasError => _issues.Any(issue => issue.Severity == MasterValidationSeverity.Error);

        /// <summary>検証器の診断を集約する。</summary>
        public void Add(MasterValidationIssue issue)
        {
            _issues.Add(issue);
        }

        /// <summary>構造が不正な親とその配下を、後続の値検証から除外する。</summary>
        public bool IsBlocked(MasterValidationContext context, string path)
        {
            return _issues.Any(issue => issue.MasterName == context.MasterName
                && issue.Severity == MasterValidationSeverity.Error
                && issue.RuleCode.StartsWith("Schema.", StringComparison.Ordinal)
                && (issue.JsonPath == "$" || path == issue.JsonPath
                    || path.StartsWith(issue.JsonPath + ".", StringComparison.Ordinal)
                    || path.StartsWith(issue.JsonPath + "[", StringComparison.Ordinal)));
        }

        /// <summary>集合を使う相関検証の前提となる構造が正しいか。</summary>
        public bool HasSchemaError(MasterValidationContext context, string path = "$")
        {
            return _issues.Any(issue => issue.MasterName == context.MasterName
                && issue.Severity == MasterValidationSeverity.Error
                && issue.RuleCode.StartsWith("Schema.", StringComparison.Ordinal)
                && (path == "$" || issue.JsonPath == path || issue.JsonPath.StartsWith(path + ".", StringComparison.Ordinal)
                    || issue.JsonPath.StartsWith(path + "[", StringComparison.Ordinal)));
        }
    }
}
