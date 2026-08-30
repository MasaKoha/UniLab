using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UniLab.UI.Focus
{
    /// <summary>
    /// EventSystem の現在選択オブジェクトに追従する枠ハイライト。パッド／キーボードで
    /// 「今どこを操作しているか」を常に視認可能にする（screen-spec.md「入力・フォーカス設計」）。
    /// 選択中の Selectable の子として枠を配置し、Stretch でぴったり重ねることで追従を実現する。
    /// 枠の見た目（枠線・塗り）は複数の Graphic で構成されるため、本コンポーネントは
    /// 枠ルートの GameObject を丸ごと ON/OFF する。本コンポーネント自身は常時アクティブな
    /// 別の GameObject に置くこと（枠と同居させると非表示中に毎フレーム処理が止まる）。
    /// </summary>
    public sealed class FocusHighlighter : MonoBehaviour
    {
        [SerializeField] private RectTransform _highlightRectTransform;
        [SerializeField] private GameObject _highlightVisualRoot;

        private readonly CompositeDisposable _disposables = new();
        private GameObject _lastSelectedGameObject;

        // EventSystem はシーンごとに生成・破棄されるため、シーンをまたいで参照を持ち越さないよう
        // 初期化時にそのシーンのものを受け取る
        private EventSystem _eventSystem;

        /// <summary>
        /// 操作対象の EventSystem を受け取り、初期表示状態の設定と毎フレーム追従の購読を開始する。
        /// このシーンの Presenter が初期化時に一度だけ呼ぶ。
        /// </summary>
        public void Initialize(EventSystem eventSystem)
        {
            _eventSystem = eventSystem;
            _highlightVisualRoot.SetActive(false);
            EnsureIgnoreLayout();

            this.UpdateAsObservable()
                .Subscribe(_ => HandleUpdate())
                .AddTo(_disposables);
        }

        /// <summary>
        /// LayoutGroup 配下の Selectable に SetParent されると、レイアウト計算に巻き込まれて
        /// ハイライトが高さ0に潰れる。LayoutElement(ignoreLayout) を必ず付与して除外する。
        /// </summary>
        private void EnsureIgnoreLayout()
        {
            var layoutElement = _highlightRectTransform.gameObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = _highlightRectTransform.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.ignoreLayout = true;
        }

        private void HandleUpdate()
        {
            var currentSelected = _eventSystem != null ? _eventSystem.currentSelectedGameObject : null;

            // perf: 選択が前フレームと同じなら追従処理をスキップする（毎フレームの SetParent を避ける）
            if (currentSelected == _lastSelectedGameObject)
            {
                return;
            }

            _lastSelectedGameObject = currentSelected;
            ApplySelection(currentSelected);
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }

        private void ApplySelection(GameObject selected)
        {
            var selectable = selected != null ? selected.GetComponent<Selectable>() : null;
            if (selectable == null)
            {
                _highlightVisualRoot.SetActive(false);
                return;
            }

            _highlightVisualRoot.SetActive(true);
            _highlightRectTransform.SetParent(selectable.transform, worldPositionStays: false);
            _highlightRectTransform.anchorMin = Vector2.zero;
            _highlightRectTransform.anchorMax = Vector2.one;
            _highlightRectTransform.offsetMin = Vector2.zero;
            _highlightRectTransform.offsetMax = Vector2.zero;
            _highlightRectTransform.SetAsLastSibling();
        }
    }
}
