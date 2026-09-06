using TMPro;
using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// TextMeshPro 関連の共通ユーティリティ。
    /// 動的フォントが生成する TMP SubMeshUI（2ページ目以降のアトラス/フォールバック用の子）の除去などを担う。
    /// 増殖対策の実体はここ（フォント設定）と本メソッド（保存前の掃除）に集約する。
    /// </summary>
    public static class UniLabTmpUtility
    {
        /// <summary>
        /// 階層内の TMP_SubMeshUI を全て破棄する。プレハブ/シーン保存の直前に呼び、
        /// シリアライズされた SubMeshUI が増殖するのを防ぐ。TMP は実行時に必要分を再生成するため安全。
        /// エディタ/ビルド時の利用を想定（DestroyImmediate を使う）。戻り値は除去した数。
        /// </summary>
        public static int StripSubMeshes(GameObject root)
        {
            var subMeshes = root.GetComponentsInChildren<TMP_SubMeshUI>(true);
            var removedCount = 0;
            foreach (var subMesh in subMeshes)
            {
                if (subMesh == null)
                {
                    continue;
                }

                Object.DestroyImmediate(subMesh.gameObject);
                removedCount++;
            }

            return removedCount;
        }
    }
}
