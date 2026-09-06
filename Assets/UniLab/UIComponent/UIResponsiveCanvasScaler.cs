using UnityEngine;
using UnityEngine.UI;

namespace UniLab.UI
{
    /// <summary>
    /// 画面アスペクト比に応じて CanvasScaler の matchWidthOrHeight を 0(幅合わせ)↔1(高さ合わせ) で自動切替する。
    /// 縦長端末では幅フィット、横広端末(iPad/正方形)では高さフィットにすることで、横広端末で UI が
    /// 過剰スケールするのを防ぎ、余った横幅をレターボックス(左右余白)化する。
    /// matchWidthOrHeight は DrivenRectTransformTracker の対象外(=シリアライズされる)ため、編集時に書き換えると
    /// シーンへ保存汚染が出る。よって本コンポーネントは実行時のみ動作し、編集時はシーン保存値(既定 match=0)を尊重する。
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    [AddComponentMenu("UniLab/UI/UI Responsive Canvas Scaler")]
    public class UIResponsiveCanvasScaler : MonoBehaviour
    {
        // 切替境界でのチャタリング(0↔1往復)を防ぐためのアスペクト不感帯。基準アスペクト±この幅は現状維持。
        [SerializeField] private float _hysteresis = 0.02f;

        private CanvasScaler _canvasScaler;
        private int _lastWidth;
        private int _lastHeight;
        // 適用済み match。未適用は -1。
        private int _appliedMatch = -1;

        private void OnEnable()
        {
            _canvasScaler = GetComponent<CanvasScaler>();
            Apply(true);
        }

        // 例外（no-Update 方針の対象外）: 解像度変化を監視する純粋な共通UI部品で、駆動する上位 Presenter が存在しない。
        // 処理は前回値との比較のみで軽量なため Update を許容する。
        private void Update()
        {
            if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            {
                Apply(false);
            }
        }

        // 現在の画面アスペクトと参照アスペクトを比較し、match を決定して適用する。
        private void Apply(bool force)
        {
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;

            var referenceResolution = _canvasScaler.referenceResolution;
            if (referenceResolution.x <= 0f || referenceResolution.y <= 0f)
            {
                return;
            }

            var referenceAspect = referenceResolution.x / referenceResolution.y;
            var currentAspect = (float)Screen.width / Screen.height;

            var targetMatch = DecideMatch(currentAspect, referenceAspect);
            if (force || targetMatch != _appliedMatch)
            {
                // 0 = 幅合わせ / 1 = 高さ合わせ
                _canvasScaler.matchWidthOrHeight = targetMatch;
                _appliedMatch = targetMatch;
            }
        }

        // 縦長(基準より細い)なら幅合わせ=0、横広(基準より広い)なら高さ合わせ=1。不感帯は現状維持(初回は0)。
        private int DecideMatch(float currentAspect, float referenceAspect)
        {
            if (currentAspect < referenceAspect - _hysteresis)
            {
                return 0;
            }

            if (currentAspect > referenceAspect + _hysteresis)
            {
                return 1;
            }

            return _appliedMatch >= 0 ? _appliedMatch : 0;
        }
    }
}
