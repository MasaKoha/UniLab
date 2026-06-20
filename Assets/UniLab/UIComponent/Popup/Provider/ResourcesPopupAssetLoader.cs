using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// Resources からポップアップ View をロードする IPopupAssetLoader 実装。
    /// パスは型名規約 Resources/Popup/{型名}。Addressables 非依存で、小規模・組み込み用途向け。
    /// </summary>
    public sealed class ResourcesPopupAssetLoader : IPopupAssetLoader
    {
        private const string ResourcePathPrefix = "Popup/";

        /// <summary>Resources/Popup/{型名} をロードして生成し、非表示で返す。見つからなければ例外を投げる。</summary>
        public UniTask<TPopup> InstantiateAsync<TPopup>(Transform parent, CancellationToken cancellationToken)
            where TPopup : PopupBase
        {
            var prefab = Resources.Load<TPopup>($"{ResourcePathPrefix}{typeof(TPopup).Name}");
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Resources/{ResourcePathPrefix}{typeof(TPopup).Name} が見つかりません。");
            }

            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            instance.gameObject.SetActive(false);
            return UniTask.FromResult(instance);
        }

        /// <summary>生成物を破棄する。Play モード停止等で既に破棄済みなら何もしない。</summary>
        public void Release(PopupBase popup)
        {
            // Unity の == は破棄済みを null 扱いする。破棄済みアクセスで MissingReferenceException になるため弾く
            if (popup == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(popup.gameObject);
        }
    }
}
