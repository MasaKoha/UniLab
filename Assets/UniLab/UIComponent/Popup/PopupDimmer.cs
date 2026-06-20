using R3;
using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI.Popup
{
    /// <summary>
    /// IPopupDimmer 実装。全画面ボタンで後ろの操作を遮り、タップを OnClick として通知する。
    /// PopupRoot 配下に 1 つ置き、表示時に対象ポップアップの直下へ移動して暗幕を共有する。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class PopupDimmer : MonoBehaviour, IPopupDimmer
    {
        private readonly Subject<Unit> _onClick = new();
        private Button _button = null;

        /// <summary>暗幕タップ通知。</summary>
        public Observable<Unit> OnClick => _onClick;

        private void Awake()
        {
            _button = GetComponent<Button>();
            // ボタンクリックを Subject へ中継する。AddTo(this) で破棄時に購読解除する
            _button.OnClickAsObservable()
                .Subscribe(_ => _onClick.OnNext(Unit.Default))
                .AddTo(this);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _onClick.Dispose();
        }

        /// <summary>暗幕を表示し、対象ポップアップの直下へ移動する。</summary>
        public void Show(Transform popupTransform)
        {
            gameObject.SetActive(true);
            // UI は後ろの兄弟ほど前面。暗幕→ポップアップの順で最前面へ送り、暗幕を必ずポップアップの直下に置く
            transform.SetAsLastSibling();
            popupTransform.SetAsLastSibling();
        }

        /// <summary>暗幕を隠す。</summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
