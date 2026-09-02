using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// シーンに置いた View インスタンスをそのまま使い回す ViewProvider。プレハブも Instantiate も使わない。
    /// シーンをコード生成していてプレハブを持たないプロジェクトや、種類が少なく常駐させて構わないポップアップ向け。
    /// 同じ型を同時に2枚出すことはできない（Stack 表示で同型を重ねる用途には向かない）。
    /// </summary>
    public sealed class SceneInstancePopupViewProvider : MonoBehaviour, IPopupViewProvider
    {
        [SerializeField] private List<PopupBase> _popups = new();

        /// <summary>登録済みインスタンスから型一致のものを返す。見つからなければ例外を投げる。</summary>
        public UniTask<TPopup> LoadAsync<TPopup>(CancellationToken cancellationToken) where TPopup : PopupBase
        {
            foreach (var popup in _popups)
            {
                if (popup is TPopup matched)
                {
                    matched.gameObject.SetActive(false);
                    return UniTask.FromResult(matched);
                }
            }

            throw new InvalidOperationException($"型 {typeof(TPopup).Name} のポップアップがシーンに登録されていません。");
        }

        /// <summary>破棄せず非表示に戻す。次回の LoadAsync で再利用される。</summary>
        public void Release(PopupBase popup)
        {
            // 表示中に Play 終了やシーン破棄が起きると、シーンが持つ実体が先に消えたあとで
            // PresentAsync の finally からここへ到達する。Unity の == 判定で破棄済みを弾く
            // （シーンごと消えているため、非表示に戻す対象がそもそも無い）
            if (popup == null)
            {
                return;
            }

            popup.gameObject.SetActive(false);
        }
    }
}
