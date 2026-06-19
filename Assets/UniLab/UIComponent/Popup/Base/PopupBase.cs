using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 全ポップアップ View の基底。背景タップとライフサイクルフックを共通化する。
    /// </summary>
    public abstract class PopupBase : MonoBehaviour, IPopupView
    {
        [SerializeField] private Button _backgroundButton = null;

        /// <summary>このポップアップに渡された表示パラメータ。Initialize で設定される。</summary>
        public IPopupParameter Parameter { get; private set; }

        /// <summary>
        /// パラメータを受け取り、背景タップ購読と派生クラス初期化を行う。表示前に PopupService が呼ぶ。
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

        /// <summary>派生クラス固有の初期化。Parameter 設定後に呼ばれる。</summary>
        protected abstract void OnInitialize();

        /// <summary>開くアニメーションを再生する。PopupService の表示処理から呼ばれる。</summary>
        public abstract UniTask OpenAsync();

        /// <summary>バックキー / 背景タップ時の閉じ処理。結果を確定する。</summary>
        public abstract void OnClose();

        /// <summary>閉じるアニメーションを再生する。PopupService のクローズ処理から呼ばれる。</summary>
        public abstract UniTask CloseAsync();
    }
}
