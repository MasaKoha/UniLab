using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// 全ポップアップ View の基底。背景タップ・初期化・開閉アニメーション委譲を共通化する。
    /// </summary>
    public abstract class PopupBase : MonoBehaviour, IPopupView
    {
        [SerializeField] private Button _backgroundButton = null;
        [SerializeField] private PopupTransitionBase _transition = null;

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

        /// <summary>開くアニメーションを再生する。Transition 未設定なら即時完了する。</summary>
        public async UniTask OpenAsync()
        {
            if (_transition != null)
            {
                // destroyCancellationToken で外部破棄・シーン遷移時にアニメーションを安全に中断する
                await _transition.PlayOpenAsync(destroyCancellationToken);
            }
        }

        /// <summary>閉じるアニメーションを再生する。Transition 未設定なら即時完了する。</summary>
        public async UniTask CloseAsync()
        {
            if (_transition != null)
            {
                await _transition.PlayCloseAsync(destroyCancellationToken);
            }
        }

        /// <summary>派生クラス固有の初期化。Parameter 設定後に呼ばれる。</summary>
        protected abstract void OnInitialize();

        /// <summary>バックキー / 背景タップ時の閉じ処理。結果を確定する。</summary>
        public abstract void OnClose();
    }
}
