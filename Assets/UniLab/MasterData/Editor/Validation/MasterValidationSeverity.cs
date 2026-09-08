#nullable enable

namespace UniLab.MasterData.Editor.Validation
{
    /// <summary>ビルドを止める必要があるかを表す。</summary>
    public enum MasterValidationSeverity
    {
        /// <summary>問題なし。</summary>
        None = 0,
        /// <summary>確認が必要だがビルド可能。</summary>
        Warning,
        /// <summary>ビルドを中断する不整合。</summary>
        Error,
    }
}
