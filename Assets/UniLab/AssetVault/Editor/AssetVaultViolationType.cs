namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault 管理グループの規約違反の種別です。
    /// </summary>
    public enum AssetVaultViolationType
    {
        /// <summary>未分類（既定値）。</summary>
        None = 0,

        /// <summary>同一アドレスが複数エントリに付いている（実行時ロードを壊す）。</summary>
        DuplicateAddress,

        /// <summary>どのエントリも使用していない孤立ラベル。</summary>
        OrphanLabel,

        /// <summary>他エントリの依存でもあるアセットがエントリ登録されている（"_" skip フォルダ／共有グループ化の候補）。</summary>
        DependencyRegisteredAsEntry,
    }
}
