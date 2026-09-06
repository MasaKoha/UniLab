namespace UniLab.Native.Gallery
{
    /// <summary>
    /// ギャラリー保存の結果。成否と保存先（content:// URI 等）を呼び出し側へ返す。
    /// </summary>
    public readonly struct GallerySaveResult
    {
        /// <summary>保存に成功したか。</summary>
        public bool Success { get; }

        /// <summary>保存先の URI またはパス。失敗時は空文字。</summary>
        public string Destination { get; }

        /// <summary>保存元の一時 PNG パス。</summary>
        public string SourcePath { get; }

        public GallerySaveResult(bool success, string destination, string sourcePath)
        {
            Success = success;
            Destination = destination;
            SourcePath = sourcePath;
        }

        /// <summary>保存成功の結果を生成する。</summary>
        public static GallerySaveResult Succeeded(string destination, string sourcePath)
        {
            return new GallerySaveResult(true, destination, sourcePath);
        }

        /// <summary>保存失敗の結果を生成する。保存先は空文字になる。</summary>
        public static GallerySaveResult Failed(string sourcePath)
        {
            return new GallerySaveResult(false, string.Empty, sourcePath);
        }
    }
}
