using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UniLab.Common;
using UnityEngine;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// ポップアップのスタック表示・状態管理を担うマネージャ基底。Singleton として常駐する。
    /// </summary>
    public abstract class PopupManagerBase<T> : SingletonMonoBehaviour<T> where T : MonoBehaviour
    {
        [SerializeField] private Transform _popupRoot = null;
        private readonly ReactiveProperty<int> _popupCount = new();
        private readonly Stack<PopupBase> _popupStack = new();

        /// <summary>表示中のポップアップが 1 つ以上あるか。入力ブロック判定に使う。</summary>
        public bool HasActivePopup => _popupCount.Value > 0;

        protected override void OnAwake()
        {
            _popupCount.Subscribe(_ =>
                {
                    foreach (var popup in _popupStack)
                    {
                        popup.SetBackgroundButtonActiveIfTop(_popupStack);
                    }
                })
                .AddTo(destroyCancellationToken);
        }

        /// <summary>
        /// プレハブからポップアップを生成し初期化する。生成直後は非表示で、OpenPopupAsync で表示する。
        /// </summary>
        public TPopup InstantiatePopup<TPopup>(TPopup popup, IPopupParameter parameter) where TPopup : PopupBase
        {
            if (popup == null)
            {
                throw new System.ArgumentNullException(nameof(popup), "Popup cannot be null.");
            }

            var popupObject = Instantiate(popup, _popupRoot);
            popupObject.Initialize(parameter);
            popupObject.gameObject.SetActive(false);
            return popupObject;
        }

        /// <summary>
        /// ポップアップをスタックに積んで表示し、開くアニメーションを再生する。
        /// </summary>
        public async UniTask OpenPopupAsync<TPopup>(TPopup popupInstance) where TPopup : PopupBase
        {
            PushToStack(popupInstance);
            popupInstance.gameObject.SetActive(true);
            await popupInstance.OpenAsync();
        }

        /// <summary>
        /// ユーザー操作の完了を待ってからスタックから除去し破棄する。後方互換のため残す。
        /// </summary>
        public async UniTask WaitPopupAsync<TPopup>(TPopup popupInstance, bool destroy = true) where TPopup : PopupBase
        {
            await popupInstance.WaitAsync();
            RemoveFromStack(popupInstance);
            if (destroy)
            {
                Destroy(popupInstance.gameObject);
            }
        }

        /// <summary>
        /// 閉じるアニメーションを再生してスタックから除去・破棄する。結果待ちは呼び出し側が行う前提。
        /// ShowAsync の finally から呼ぶことで、キャンセル・例外時もリークせず確実に後始末できる。
        /// </summary>
        public async UniTask ClosePopupAsync<TPopup>(TPopup popupInstance, bool destroy = true) where TPopup : PopupBase
        {
            await popupInstance.CloseAsync();
            RemoveFromStack(popupInstance);
            if (destroy)
            {
                Destroy(popupInstance.gameObject);
            }
        }

        /// <summary>
        /// 最上位ポップアップをバックキー相当で閉じる。空スタック時は何もしない（バックキー誤発火でのクラッシュ対策）。
        /// </summary>
        public async UniTask CloseTopPopupAsync()
        {
            if (_popupStack.Count == 0)
            {
                return;
            }

            var popupInstance = _popupStack.Peek();
            var parameter = popupInstance.Parameter;
            if (!parameter.EnableBackKey)
            {
                return;
            }

            if (parameter.CustomBackAsync != null)
            {
                await parameter.CustomBackAsync();
                return;
            }

            popupInstance.OnClose();
        }

        private void PushToStack(PopupBase popup)
        {
            _popupStack.Push(popup);
            // _popupCount と Stack.Count の二重管理を避けるため、更新点を 1 箇所に集約して同期する
            _popupCount.Value = _popupStack.Count;
        }

        private void RemoveFromStack(PopupBase popup)
        {
            // v1 は最上位のみ閉じる前提。トップ不一致は二重クローズ等の異常なので、誤破棄を避け警告に留める
            if (_popupStack.Count > 0 && _popupStack.Peek() == popup)
            {
                _popupStack.Pop();
                _popupCount.Value = _popupStack.Count;
                return;
            }

            Debug.LogWarning($"[Popup] 閉じる対象がスタック最上位ではありません: {popup.name}");
        }
    }
}
