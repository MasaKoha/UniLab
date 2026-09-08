#nullable enable

namespace UniLab.MasterData.Editor.Validation
{
    /// <summary>入力の修正箇所と理由を保持する不変の診断。</summary>
    public sealed class MasterValidationIssue
    {
        /// <summary>重要度。</summary>
        public MasterValidationSeverity Severity { get; }
        /// <summary>安定したルール識別子。</summary>
        public string RuleCode { get; }
        /// <summary>対象マスタ。</summary>
        public string MasterName { get; }
        /// <summary>入力ファイル。</summary>
        public string FilePath { get; }
        /// <summary>問題の入力位置。</summary>
        public string JsonPath { get; }
        /// <summary>不整合の理由。</summary>
        public string Message { get; }
        /// <summary>期待値。</summary>
        public string Expected { get; }
        /// <summary>実際の値。</summary>
        public string Actual { get; }

        /// <summary>修正に必要な診断情報を保持する。</summary>
        public MasterValidationIssue(MasterValidationSeverity severity, string ruleCode, string masterName, string filePath, string jsonPath, string message, string expected, string actual)
        {
            Severity = severity;
            RuleCode = ruleCode;
            MasterName = masterName;
            FilePath = filePath;
            JsonPath = jsonPath;
            Message = message;
            Expected = expected;
            Actual = actual;
        }

        /// <summary>ログを一行で検索できる形式にする。</summary>
        public override string ToString()
        {
            return $"[{Severity}/{RuleCode}] {MasterName} {FilePath}::{JsonPath}: {Message} (期待: {Expected}, 実際: {Actual})"
                .Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
