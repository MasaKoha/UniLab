using UnityEditor;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// QA 用のデバッグ上書き機能です。Play 突入時に <see cref="AssetVaultRuntime"/> の
    /// BaseUrl / ContentPath を上書きし、「prod アプリで dev1 のアセットを見る」「特定の版フォルダを読む」を実現します。
    /// 値は EditorPrefs に保持し、ランタイムへはエディタ側からのみ反映します（ランタイムにデバッグ専用 API は足しません）。
    /// </summary>
    [InitializeOnLoad]
    public static class AssetVaultDebugOverride
    {
        private const string EnabledPrefKey = "UniLab.AssetVault.DebugOverride.Enabled";
        private const string BaseUrlPrefKey = "UniLab.AssetVault.DebugOverride.BaseUrl";
        private const string ContentPathPrefKey = "UniLab.AssetVault.DebugOverride.ContentPath";

        static AssetVaultDebugOverride()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>デバッグ上書きを有効にするかどうかです。</summary>
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, false);
            set => EditorPrefs.SetBool(EnabledPrefKey, value);
        }

        /// <summary>上書きする BaseUrl です（例 https://dev1.xxx.xxx/app）。</summary>
        public static string BaseUrl
        {
            get => EditorPrefs.GetString(BaseUrlPrefKey, string.Empty);
            set => EditorPrefs.SetString(BaseUrlPrefKey, value);
        }

        /// <summary>上書きする ContentPath です（version.json の path に相当する版セグメント）。</summary>
        public static string ContentPath
        {
            get => EditorPrefs.GetString(ContentPathPrefKey, string.Empty);
            set => EditorPrefs.SetString(ContentPathPrefKey, value);
        }

        // EnteredPlayMode（ドメインリロード後）で反映する。アプリ初期化との前後は保証されないため、
        // アプリ側が config から値を設定する場合はそちらが優先される（順序はアプリ責務）。
        // ドメインリロード無効時は static フィールド（AssetVaultRuntime 側の値）が Play セッション間で
        // 残留するため、無効時は null クリアして前回のデバッグ値リークを防ぐ。
        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            if (!Enabled)
            {
                AssetVaultRuntime.BaseUrl = null;
                AssetVaultRuntime.ContentPath = null;
                return;
            }

            AssetVaultRuntime.BaseUrl = BaseUrl;
            AssetVaultRuntime.ContentPath = ContentPath;
        }
    }
}
