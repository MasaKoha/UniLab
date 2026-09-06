using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// 自身の RectTransform の横幅を「親幅と MaxWidth の小さい方」に制限し、中央寄せ・縦ストレッチする。
    /// iPad/正方形など横広端末で操作UI(ヘッダー/リスト/タブバー)が間延びするのを防ぎ、左右に余白を作る。
    /// アンカー/ピボット/サイズ/位置は DrivenRectTransformTracker で driven 化するため、ExecuteAlways で編集中に
    /// 動いてもシーン/プレハブには保存されない(LayoutGroup と同じ機構)。編集プレビューと保存非汚染を両立する。
    /// </summary>
    [RequireComponent(typeof(RectTransform)), ExecuteAlways]
    [AddComponentMenu("UniLab/UI/UI Max Width Column")]
    public class UIMaxWidthColumn : MonoBehaviour
    {
        // 参照解像度のユニット基準での最大幅。CanvasScaler の scaleFactor は全体に等しく掛かるため、
        // ローカルユニットのまま親幅と比較してよい。
        [SerializeField] private float _maxWidth = 1179f;

        private RectTransform _rectTransform;
        private DrivenRectTransformTracker _tracker;
        private float _lastParentWidth = -1f;
        private float _lastMaxWidth = -1f;

        private void OnEnable()
        {
            _rectTransform = (RectTransform)transform;
            Apply();
        }

        private void OnDisable()
        {
            // driven 解除(Inspector のグレーアウトを戻し、支配を返す)。
            _tracker.Clear();
        }

        // 例外（no-Update 方針の対象外）: ExecuteAlways で編集時も親幅の変化を監視する純粋な共通UI部品。
        // 駆動する上位 Presenter が存在せず、処理は前回値比較のみで軽量なため Update を許容する。
        private void Update()
        {
            var parent = _rectTransform.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            if (!Mathf.Approximately(parent.rect.width, _lastParentWidth) || !Mathf.Approximately(_maxWidth, _lastMaxWidth))
            {
                Apply();
            }
        }

        private void Apply()
        {
            var parent = _rectTransform.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            _lastParentWidth = parent.rect.width;
            _lastMaxWidth = _maxWidth;

            var width = Mathf.Min(parent.rect.width, _maxWidth);

            // アンカー等を driven 化(=シリアライズ対象外)してから設定する。これが保存汚染を防ぐ肝。
            _tracker.Clear();
            _tracker.Add(this, _rectTransform,
                DrivenTransformProperties.Anchors |
                DrivenTransformProperties.Pivot |
                DrivenTransformProperties.SizeDelta |
                DrivenTransformProperties.AnchoredPosition);

            // 横は中央ピボットの固定幅、縦は親いっぱいにストレッチ。
            _rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _rectTransform.sizeDelta = new Vector2(width, 0f);
            _rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
