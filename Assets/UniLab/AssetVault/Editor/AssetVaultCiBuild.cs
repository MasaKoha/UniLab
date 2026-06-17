using System;
using UnityEditor;
using UnityEngine;

namespace UniLab.AssetVault.Editor
{
    /// <summary>
    /// CI / batchmode から Addressables ビルドを実行するためのエントリポイントです。
    /// 実体は <see cref="AssetVaultEditorOperations"/> に委譲し、
    /// ビルド結果を終了コードへ変換することで CI のパス/フェイルを判定可能にします。
    /// 新規ビルドの呼び出し例:
    /// unity -batchmode -quit -executeMethod UniLab.AssetVault.Editor.AssetVaultCiBuild.BuildNewForCi
    /// content update の呼び出し例:
    /// unity -batchmode -quit -executeMethod UniLab.AssetVault.Editor.AssetVaultCiBuild.BuildContentUpdateForCi
    /// </summary>
    public static class AssetVaultCiBuild
    {
        /// <summary>
        /// 新規 Addressables player content をビルドし、結果を終了コードへ変換します。
        /// 成功時は exit 0、規約違反やビルド失敗時は exit 1 で CI を確実に落とします。
        /// CI からは次のように呼び出します:
        /// unity -batchmode -quit -executeMethod UniLab.AssetVault.Editor.AssetVaultCiBuild.BuildNewForCi
        /// </summary>
        public static void BuildNewForCi()
        {
            Debug.Log("AssetVault CI: new build started.");
            try
            {
                var succeeded = AssetVaultEditorOperations.BuildNew();
                // ビルド本体は委譲先が担い、ここは結果を終了コードへ変換するだけの薄い層に留める。
                ExitByResult(succeeded, "new build");
            }
            catch (Exception exception)
            {
                // batchmode で例外が握り潰されると CI が緑のまま通過してしまうため、必ず exit 1 で落とす
                Debug.LogError($"AssetVault CI: new build threw an exception. {exception}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// 前回の content state file から Addressables content update をビルドし、結果を終了コードへ変換します。
        /// 成功時は exit 0、規約違反やビルド失敗時は exit 1 で CI を確実に落とします。
        /// CI からは次のように呼び出します:
        /// unity -batchmode -quit -executeMethod UniLab.AssetVault.Editor.AssetVaultCiBuild.BuildContentUpdateForCi
        /// </summary>
        public static void BuildContentUpdateForCi()
        {
            Debug.Log("AssetVault CI: content update build started.");
            try
            {
                var succeeded = AssetVaultEditorOperations.BuildContentUpdate();
                // ビルド本体は委譲先が担い、ここは結果を終了コードへ変換するだけの薄い層に留める。
                ExitByResult(succeeded, "content update build");
            }
            catch (Exception exception)
            {
                // batchmode で例外が握り潰されると CI が緑のまま通過してしまうため、必ず exit 1 で落とす
                Debug.LogError($"AssetVault CI: content update build threw an exception. {exception}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// ビルド成否に応じてログ出力と Editor の終了コードを設定する共通処理です。
        /// </summary>
        private static void ExitByResult(bool succeeded, string buildLabel)
        {
            if (succeeded)
            {
                Debug.Log($"AssetVault CI: {buildLabel} succeeded.");
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError($"AssetVault CI: {buildLabel} failed.");
            EditorApplication.Exit(1);
        }
    }
}
