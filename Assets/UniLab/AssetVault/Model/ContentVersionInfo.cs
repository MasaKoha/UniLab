namespace UniLab.AssetVault
{
    /// <summary>
    /// version.json の中身を表します。ContentVersion は文字列一致で版変更を判定する内部版 ID、Path は URL セグメントとして使う公開パスです。
    /// </summary>
    public readonly struct ContentVersionInfo
    {
        /// <summary>
        /// 内部版 ID を取得します。文字列一致で版変更を判定し、順序比較には使いません。
        /// </summary>
        public string ContentVersion { get; }

        /// <summary>
        /// RemoteLoadPath の ContentPath に使う、公開 URL の不透明セグメントを取得します。
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// version.json から解決したコンテンツ版情報を作成します。
        /// </summary>
        public ContentVersionInfo(string contentVersion, string path)
        {
            ContentVersion = contentVersion;
            Path = path;
        }
    }
}
