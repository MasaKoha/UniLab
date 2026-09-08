#nullable enable
using System;
using Newtonsoft.Json.Linq;

namespace UniLab.MasterData.Editor.Validation
{
    /// <summary>一つのマスタの入力を、元の文字列を失わずに検証器へ渡す。</summary>
    public sealed class MasterValidationContext
    {
        /// <summary>マスタ識別名。</summary>
        public string MasterName { get; }
        /// <summary>ソースの所在。</summary>
        public string FilePath { get; }
        /// <summary>重複キーを検出するための生の入力。</summary>
        public string RawJson { get; }
        /// <summary>構造・値の検証対象。</summary>
        public JObject Root { get; }
        /// <summary>シリアライズ対象型。</summary>
        public Type MasterType { get; }

        /// <summary>パース済み入力と、その元になった文字列を保持する。</summary>
        public MasterValidationContext(string masterName, string filePath, string rawJson, JObject root, Type masterType)
        {
            MasterName = masterName;
            FilePath = filePath;
            RawJson = rawJson;
            Root = root;
            MasterType = masterType;
        }
    }
}
