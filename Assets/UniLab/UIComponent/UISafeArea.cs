using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// RectTransform のアンカーを端末のセーフエリアに合わせる。
    /// アンカーは DrivenRectTransformTracker で driven 化するため、ExecuteAlways で編集時に動いても
    /// シーン/プレハブには保存されない（LayoutGroup 等と同じ機構）。これにより編集プレビューと
    /// 「壊れたアンカーを保存しない」を両立する。
    /// </summary>
    [RequireComponent(typeof(RectTransform)), ExecuteAlways]
    public class UISafeArea : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private DrivenRectTransformTracker _tracker;
        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;

        private void OnEnable()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void OnDisable()
        {
            // driven 解除（Inspector のグレーアウトを戻し、他コンポーネントへ支配を返す）。
            _tracker.Clear();
        }

        // 例外（no-Update 方針の対象外）: ExecuteAlways で編集時もセーフエリア/解像度変化を監視する純粋な共通UI部品。
        // 駆動する上位 Presenter が存在せず、処理は前回値比較のみで軽量なため Update を許容する。
        private void Update()
        {
            if (HasScreenChanged())
            {
                ApplySafeArea();
            }
        }

        private bool HasScreenChanged()
        {
            return _lastSafeArea != Screen.safeArea ||
                   _lastResolution.x != Screen.width ||
                   _lastResolution.y != Screen.height;
        }

        private void ApplySafeArea()
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            // 解像度が未確定（0）のときは NaN を避けるため適用を見送る。
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);

            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            // セーフエリアは画面の部分矩形なのでアンカーは [0,1] に収まるはず。
            // 編集時など Screen 値が不定で範囲外（壊れ値）になった場合は適用を見送る。
            if (anchorMin.x < -0.01f || anchorMin.y < -0.01f || anchorMax.x > 1.01f || anchorMax.y > 1.01f ||
                anchorMin.x > anchorMax.x || anchorMin.y > anchorMax.y)
            {
                return;
            }

            // アンカーを driven 化（＝シリアライズ対象外）してから設定する。これが保存汚染を防ぐ肝。
            _tracker.Clear();
            _tracker.Add(this, _rectTransform, DrivenTransformProperties.Anchors);
            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
        }
    }
}
