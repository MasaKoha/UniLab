using System.IO;
using UniLab.AssetVault.Debugging;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// デバッグ環境設定アセットを、development ビルドのときだけ Resources に一時複製し、ビルド後に除去します。
    /// 正本は Resources の外にあるため、release ビルドでは複製されず同梱されません（コードは define 制約でストリップ済み）。
    /// </summary>
    public sealed class AssetVaultDebugBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string ResourcesFolderPath = "Assets/UniLab/AssetVault/Debug/Resources";
        private const string DebugFolderPath = "Assets/UniLab/AssetVault/Debug";
        private const string ResourcesFolderName = "Resources";
        private static readonly string ResourceCopyPath = ResourcesFolderPath + "/" + AssetVaultDebugEnvironmentSettings.ResourceName + ".asset";

        /// <summary>他のビルドプロセッサに対する実行順です。既定の 0 で十分です。</summary>
        public int callbackOrder => 0;

        /// <summary>
        /// development ビルドのときのみ、正本アセットを Resources へ複製します。
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            // まず前回ビルドが中断して残った複製を掃除し、状態を release 安全（複製なし）に揃える。
            RemoveResourceCopy();

            var isDevelopmentBuild = (report.summary.options & BuildOptions.Development) != 0;
            if (!isDevelopmentBuild)
            {
                return;
            }

            var sourcePath = AssetVaultDebugEnvironmentSettings.AssetPath;
            if (!File.Exists(sourcePath))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolderPath))
            {
                AssetDatabase.CreateFolder(DebugFolderPath, ResourcesFolderName);
            }

            AssetDatabase.CopyAsset(sourcePath, ResourceCopyPath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// ビルド後に Resources への複製を除去し、リポジトリを汚さないようにします。
        /// </summary>
        public void OnPostprocessBuild(BuildReport report)
        {
            RemoveResourceCopy();
        }

        private static void RemoveResourceCopy()
        {
            if (AssetDatabase.LoadAssetAtPath<AssetVaultDebugEnvironmentSettings>(ResourceCopyPath) == null)
            {
                return;
            }

            AssetDatabase.DeleteAsset(ResourceCopyPath);
            AssetDatabase.SaveAssets();
        }
    }
}
