using System;
using R3;
using TMPro;
using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// 整数表示を差し替えつつ、跳ね演出とカウントアップを再生する TMP 小部品。
    /// 値更新は毎フレームでも、更新ループ内では追加確保を出さない。
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class NumberBounceText : MonoBehaviour
    {
        private const float DefaultBounceDurationSeconds = 0.25f;
        private const float DefaultPeakScale = 1.6f;
        private const float DefaultScale = 1f;
        private const float MinimumDurationSeconds = 0.0001f;
        private const float OutBackOvershoot = 1.70158f;
        private const float HalfProgress = 0.5f;
        private const float FullProgress = 1f;

        [SerializeField] private string _format = "{0}";

        private TextMeshProUGUI _textMeshPro;
        private RectTransform _rectTransform;
        private IDisposable _animationSubscription;
        private int _currentValue;
        private int _countUpStartValue;
        private int _countUpTargetValue;
        private float _countUpStartedAtRealtimeSeconds;
        private float _countUpDurationSeconds;
        private bool _isCountingUp;
        private float _bounceStartedAtRealtimeSeconds;
        private float _bounceDurationSeconds;
        private float _bounceStartScale;
        private float _bouncePeakScale;
        private bool _isBouncing;

        /// <summary>
        /// 表示書式を返す。`{0}` の位置に整数値を埋め込む。
        /// </summary>
        public string Format
        {
            get
            {
                return _format;
            }
            set
            {
                _format = string.IsNullOrEmpty(value) ? "{0}" : value;
                RefreshText();
            }
        }

        /// <summary>
        /// カウントアップまたは跳ね演出の再生中かを返す。
        /// </summary>
        public bool IsPlaying { get; private set; }

        // 参照は Awake に頼らず遅延取得する。モーダル配下など非アクティブな GameObject に付いていると、
        // 呼び出し側が Initialize で Format や SetValue を設定した時点では Awake が走っておらず null になる（2026-09-03 実測）
        private TextMeshProUGUI TextMeshPro => _textMeshPro != null ? _textMeshPro : (_textMeshPro = GetComponent<TextMeshProUGUI>());

        private RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        private void OnDestroy()
        {
            StopAnimation(resetScale: false);
        }

        /// <summary>
        /// 値を即時反映し、演出を停止する。
        /// </summary>
        public void SetValue(int value)
        {
            StopAnimation(resetScale: true);
            _currentValue = value;
            RefreshText();
        }

        /// <summary>
        /// 値を差し替えて跳ね演出を再生する。
        /// 再生中に呼ばれても現在スケールから再開し、演出を積み重ねない。
        /// </summary>
        public void Bounce(int value, float durationSeconds = DefaultBounceDurationSeconds, float peakScale = DefaultPeakScale)
        {
            _currentValue = value;
            RefreshText();

            var startScale = RectTransform.localScale.x;
            StopAnimation(resetScale: false);
            StartBounce(startScale, durationSeconds, peakScale);
        }

        /// <summary>
        /// 開始値から終了値まで整数表示を進め、最後に跳ね演出で着地させる。
        /// </summary>
        public void CountUp(int fromValue, int toValue, float durationSeconds)
        {
            StopAnimation(resetScale: true);
            _countUpStartValue = fromValue;
            _countUpTargetValue = toValue;
            _currentValue = fromValue;
            RefreshText();

            if (durationSeconds <= 0f || fromValue == toValue)
            {
                Bounce(toValue, DefaultBounceDurationSeconds, DefaultPeakScale);
                return;
            }

            _countUpStartedAtRealtimeSeconds = Time.realtimeSinceStartup;
            _countUpDurationSeconds = Mathf.Max(MinimumDurationSeconds, durationSeconds);
            _isCountingUp = true;
            EnsureAnimationLoop();
            UpdatePlayingState();
        }

        private void EnsureAnimationLoop()
        {
            if (_animationSubscription != null)
            {
                return;
            }

            _animationSubscription = Observable.EveryUpdate(destroyCancellationToken)
                .Subscribe(_ => AdvanceAnimation());
        }

        private void AdvanceAnimation()
        {
            if (_isCountingUp)
            {
                AdvanceCountUp();
            }

            if (_isBouncing)
            {
                AdvanceBounce();
            }

            if (_isCountingUp || _isBouncing)
            {
                return;
            }

            _animationSubscription?.Dispose();
            _animationSubscription = null;
            UpdatePlayingState();
        }

        private void AdvanceCountUp()
        {
            var elapsedSeconds = Time.realtimeSinceStartup - _countUpStartedAtRealtimeSeconds;
            var normalizedTime = Mathf.Clamp01(elapsedSeconds / _countUpDurationSeconds);
            var nextValue = Mathf.RoundToInt(Mathf.Lerp(_countUpStartValue, _countUpTargetValue, normalizedTime));
            if (nextValue != _currentValue)
            {
                _currentValue = nextValue;
                RefreshText();
            }

            if (normalizedTime < FullProgress)
            {
                return;
            }

            _currentValue = _countUpTargetValue;
            RefreshText();
            _isCountingUp = false;
            StartBounce(DefaultScale, DefaultBounceDurationSeconds, DefaultPeakScale);
        }

        private void StartBounce(float startScale, float durationSeconds, float peakScale)
        {
            _bounceStartedAtRealtimeSeconds = Time.realtimeSinceStartup;
            _bounceDurationSeconds = Mathf.Max(MinimumDurationSeconds, durationSeconds);
            _bounceStartScale = Mathf.Max(0f, startScale);
            _bouncePeakScale = Mathf.Max(DefaultScale, peakScale);
            _isBouncing = true;
            EnsureAnimationLoop();
            ApplyScale(_bounceStartScale);
            UpdatePlayingState();
        }

        private void AdvanceBounce()
        {
            var elapsedSeconds = Time.realtimeSinceStartup - _bounceStartedAtRealtimeSeconds;
            var normalizedTime = Mathf.Clamp01(elapsedSeconds / _bounceDurationSeconds);
            ApplyScale(EvaluateBounceScale(normalizedTime));

            if (normalizedTime < FullProgress)
            {
                return;
            }

            ApplyScale(DefaultScale);
            _isBouncing = false;
            UpdatePlayingState();
        }

        private float EvaluateBounceScale(float normalizedTime)
        {
            if (normalizedTime <= HalfProgress)
            {
                var ascentProgress = normalizedTime / HalfProgress;
                return Mathf.LerpUnclamped(_bounceStartScale, _bouncePeakScale, EvaluateOutBack(ascentProgress));
            }

            var descentProgress = (normalizedTime - HalfProgress) / HalfProgress;
            var reverseProgress = 1f - descentProgress;
            var descentEase = 1f - EvaluateOutBack(reverseProgress);
            return Mathf.LerpUnclamped(_bouncePeakScale, DefaultScale, descentEase);
        }

        private void StopAnimation(bool resetScale)
        {
            _animationSubscription?.Dispose();
            _animationSubscription = null;
            _isCountingUp = false;
            _isBouncing = false;

            if (resetScale)
            {
                ApplyScale(DefaultScale);
            }

            UpdatePlayingState();
        }

        private void UpdatePlayingState()
        {
            IsPlaying = _isCountingUp || _isBouncing;
        }

        private void RefreshText()
        {
            TextMeshPro.SetText(_format, _currentValue);
        }

        private void ApplyScale(float scale)
        {
            RectTransform.localScale = new Vector3(scale, scale, DefaultScale);
        }

        private static float EvaluateOutBack(float normalizedTime)
        {
            var inverse = normalizedTime - 1f;
            return 1f + ((OutBackOvershoot + 1f) * inverse * inverse * inverse) + (OutBackOvershoot * inverse * inverse);
        }
    }
}
