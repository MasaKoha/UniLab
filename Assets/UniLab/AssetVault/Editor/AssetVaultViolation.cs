namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// AssetVault 規約違反の1件です（Dashboard 表示用の読み取り専用データ）。
    /// </summary>
    public readonly struct AssetVaultViolation
    {
        /// <summary>
        /// 違反の種別と説明文を指定して1件を生成します。<see cref="AssetVaultConventionChecker"/> が検出時に呼びます。
        /// </summary>
        public AssetVaultViolation(AssetVaultViolationType violationType, string message)
        {
            ViolationType = violationType;
            Message = message;
        }

        /// <summary>違反の種別です。</summary>
        public AssetVaultViolationType ViolationType { get; }

        /// <summary>違反内容の説明（対象アドレス・アセットパス等を含む）です。</summary>
        public string Message { get; }

        /// <summary>
        /// ビルドを止めるべき致命的違反かどうかです（true なら Error 相当）。
        /// 重複アドレスは実行時ロードを壊すため Error 扱いとし、Dashboard 表示・ビルド前ゲートの双方がこの判定を共有します。
        /// </summary>
        public bool IsError => ViolationType == AssetVaultViolationType.DuplicateAddress;
    }
}
