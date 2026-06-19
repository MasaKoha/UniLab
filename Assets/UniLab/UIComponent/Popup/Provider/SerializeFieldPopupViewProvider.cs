using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// インスペクタ登録したプレハブから型一致でポップアップを生成する ViewProvider。
    /// 小規模・組み込みポップアップ（Confirm 等）向け。Addressables 非依存。
    /// </summary>
    public sealed class SerializeFieldPopupViewProvider : MonoBehaviour, IPopupViewProvider
    {
        [SerializeField] private Transform _popupRoot = null;
        [SerializeField] private List<PopupBase> _popupPrefabs = new();

        /// <summary>登録プレハブから型一致のものを生成して返す。見つからなければ例外を投げる。</summary>
        public UniTask<TPopup> LoadAsync<TPopup>(CancellationToken cancellationToken) where TPopup : PopupBase
        {
            foreach (var prefab in _popupPrefabs)
            {
                if (prefab is TPopup matchedPrefab)
                {
                    var instance = Instantiate(matchedPrefab, _popupRoot);
                    instance.gameObject.SetActive(false);
                    return UniTask.FromResult(instance);
                }
            }

            throw new InvalidOperationException(
                $"型 {typeof(TPopup).Name} のポップアッププレハブが登録されていません。");
        }

        /// <summary>生成した View を破棄する。</summary>
        public void Release(PopupBase popup)
        {
            Destroy(popup.gameObject);
        }
    }
}
