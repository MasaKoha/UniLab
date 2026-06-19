using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 全ポップアップ View の基底。背景タップ・ライフサイクルフックを共通化する。
    /// </summary>
    public abstract class PopupBase : MonoBehaviour, IPopupView
    {
        [SerializeField] private Button _backgroundButton = null;

        /// <summary>このポップアップに渡された表示パラメータ。Initialize で設定される。</summary>
        public IPopupParameter Parameter { get; private set; }

        /// <summary>
        /// パラメータを受け取り、背景タップ購読と派生クラス初期化を行う。マネージャから生成直後に呼ばれる。
        /// </summary>
        public void Initialize(IPopupParameter parameter)
        {
            Parameter = parameter;
            SetEvent();
            OnInitialize();
        }

        private void SetEvent()
        {
            // AddTo(this) で破棄時に購読解除する。背景タップ許可時のみ閉じる
            _backgroundButton.OnClickAsObservable()
                .Where(_ => Parameter.EnableBackgroundClose)
                .Subscribe(_ => OnClose())
                .AddTo(this);
        }

        /// <summary>
        /// スタック最上位のときだけ背景ボタンを有効化し、重ね表示時の誤タップを防ぐ。マネージャが呼ぶ。
        /// </summary>
        public void SetBackgroundButtonActiveIfTop(Stack<PopupBase> popupStack)
        {
            // スタックが空でなく、かつ自身が最上位のときだけ背景を有効化する
            if (popupStack.Count <= 0 || popupStack.Peek() != this)
            {
                _backgroundButton.gameObject.SetActive(false);
                return;
            }

            _backgroundButton.gameObject.SetActive(true);
        }

        /// <summary>背景ボタンの表示状態を直接指定する。</summary>
        public void SetActiveBackground(bool isActive)
        {
            _backgroundButton.gameObject.SetActive(isActive);
        }

        /// <summary>派生クラス固有の初期化。Parameter 設定後に呼ばれる。</summary>
        protected abstract void OnInitialize();

        /// <summary>開くアニメーションを再生する。マネージャの表示処理から呼ばれる。</summary>
        public abstract UniTask OpenAsync();

        /// <summary>ユーザー操作の完了を待機する。マネージャのクローズ処理から呼ばれる。</summary>
        public abstract UniTask WaitAsync();

        /// <summary>バックキー / 背景タップ時の閉じ処理。マネージャが呼ぶ。</summary>
        public abstract void OnClose();

        /// <summary>閉じるアニメーションを再生する。マネージャのクローズ処理から呼ばれる。</summary>
        public abstract UniTask CloseAsync();
    }
}
