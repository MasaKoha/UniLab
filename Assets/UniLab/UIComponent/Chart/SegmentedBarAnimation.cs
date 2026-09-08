using System;
using R3;
using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// SegmentedBarView の値補間、溜め演出、弾け演出の時間進行だけを担当する。
    /// 描画状態そのものは View に保持させ、毎フレームの反映だけを行う。
    /// </summary>
    internal sealed class SegmentedBarAnimation : IDisposable
    {
        private const float MinimumAnimationDurationSeconds = 0.0001f;
        private const float FullProgress = 1f;
        private const float OutBackOvershoot = 1.70158f;
        private const float DefaultGlowIntensity = 0f;
        private const float ShakeOscillationCycles = 6f;
        private const float Tau = Mathf.PI * 2f;

        private readonly SegmentedBarView _view;

        private IDisposable _animationSubscription;
        private float _animationStartedAtRealtimeSeconds;
        private float _animationDurationSeconds;
        private float _animationStartValue;
        private float _animationTargetValue;
        private RadarChartEasing _animationEasing;
        private bool _isValueAnimating;
        private float _chargeStartedAtRealtimeSeconds;
        private float _chargeDurationSeconds;
        private float _chargeShakeAmplitudePixels;
        private Vector2 _chargeOriginAnchoredPosition;
        private bool _isChargePlaying;
        private float _burstStartedAtRealtimeSeconds;
        private float _burstDurationSeconds;
        private float _burstStartValue;
        private float _burstStartGlowIntensity;
        private Vector2 _burstOriginAnchoredPosition;
        private bool _isBurstPlaying;

        internal SegmentedBarAnimation(SegmentedBarView view)
        {
            _view = view;
        }

        internal void AnimateTo(float normalizedValue, float durationSeconds, RadarChartEasing easing)
        {
            StopAllAnimations(restorePosition: true, resetGlow: false);
            _animationStartValue = _view.NormalizedValue;
            _animationTargetValue = Mathf.Clamp01(normalizedValue);
            StartValueAnimation(durationSeconds, easing);
        }

        internal void PlayCharge(float durationSeconds, float shakeAmplitudePixels)
        {
            StopAllAnimations(restorePosition: true, resetGlow: false);

            if (durationSeconds <= 0f)
            {
                _view.GlowIntensity = 1f;
                _view.RequestRedraw();
                return;
            }

            _chargeStartedAtRealtimeSeconds = Time.realtimeSinceStartup;
            _chargeDurationSeconds = Mathf.Max(MinimumAnimationDurationSeconds, durationSeconds);
            _chargeShakeAmplitudePixels = Mathf.Max(0f, shakeAmplitudePixels);
            _chargeOriginAnchoredPosition = _view.AnchoredPosition;
            _isChargePlaying = true;
            EnsureAnimationLoop();
            UpdateAnimatingState();
        }

        internal void PlayBurst(float durationSeconds)
        {
            StopAllAnimations(restorePosition: true, resetGlow: false);
            _view.GlowIntensity = 1f;
            _view.RequestRedraw();

            if (durationSeconds <= 0f)
            {
                _view.NormalizedValue = 0f;
                _view.GlowIntensity = 0f;
                _view.RequestRedraw();
                return;
            }

            _burstStartedAtRealtimeSeconds = Time.realtimeSinceStartup;
            _burstDurationSeconds = Mathf.Max(MinimumAnimationDurationSeconds, durationSeconds);
            _burstStartValue = _view.NormalizedValue;
            _burstStartGlowIntensity = _view.GlowIntensity;
            _burstOriginAnchoredPosition = _view.AnchoredPosition;
            _isBurstPlaying = true;
            EnsureAnimationLoop();
            UpdateAnimatingState();
        }

        internal void StopAllAnimations(bool restorePosition, bool resetGlow)
        {
            var restingAnchoredPosition = ResolveRestingAnchoredPosition();
            _animationSubscription?.Dispose();
            _animationSubscription = null;
            _isValueAnimating = false;
            _isChargePlaying = false;
            _isBurstPlaying = false;

            if (restorePosition)
            {
                _view.AnchoredPosition = restingAnchoredPosition;
            }

            if (resetGlow)
            {
                _view.GlowIntensity = DefaultGlowIntensity;
            }

            UpdateAnimatingState();
        }

        public void Dispose()
        {
            StopAllAnimations(restorePosition: false, resetGlow: false);
        }

        private void StartValueAnimation(float durationSeconds, RadarChartEasing easing)
        {
            if (durationSeconds <= 0f || easing == RadarChartEasing.None)
            {
                _view.NormalizedValue = _animationTargetValue;
                _view.RequestRedraw();
                return;
            }

            _animationStartedAtRealtimeSeconds = Time.realtimeSinceStartup;
            _animationDurationSeconds = Mathf.Max(MinimumAnimationDurationSeconds, durationSeconds);
            _animationEasing = easing;
            _isValueAnimating = true;
            EnsureAnimationLoop();
            UpdateAnimatingState();
        }

        private void EnsureAnimationLoop()
        {
            if (_animationSubscription != null)
            {
                return;
            }

            _animationSubscription = Observable.EveryUpdate(_view.destroyCancellationToken)
                .Subscribe(_ => AdvanceAnimation());
        }

        private void AdvanceAnimation()
        {
            // RadarChartAnimation と同じ理由。進行フラグはマネージド側なので View が Destroy されても
            // 読めてしまい、RequestRedraw のネイティブアクセスで毎フレーム例外を出し続ける。
            if (_view == null)
            {
                _animationSubscription?.Dispose();
                _animationSubscription = null;
                return;
            }

            if (_isValueAnimating)
            {
                AdvanceValueAnimation();
            }

            if (_isChargePlaying)
            {
                AdvanceChargeAnimation();
            }

            if (_isBurstPlaying)
            {
                AdvanceBurstAnimation();
            }

            UpdateAnimatingState();
            if (_view.IsAnimating)
            {
                return;
            }

            _animationSubscription?.Dispose();
            _animationSubscription = null;
        }

        private void AdvanceValueAnimation()
        {
            var elapsedSeconds = Time.realtimeSinceStartup - _animationStartedAtRealtimeSeconds;
            var normalizedTime = Mathf.Clamp01(elapsedSeconds / _animationDurationSeconds);
            var easedProgress = EvaluateEasing(normalizedTime, _animationEasing);
            _view.NormalizedValue = Mathf.Clamp01(Mathf.LerpUnclamped(_animationStartValue, _animationTargetValue, easedProgress));
            _view.RequestRedraw();

            if (normalizedTime < FullProgress)
            {
                return;
            }

            _view.NormalizedValue = _animationTargetValue;
            _isValueAnimating = false;
            _view.RequestRedraw();
        }

        private void AdvanceChargeAnimation()
        {
            var elapsedSeconds = Time.realtimeSinceStartup - _chargeStartedAtRealtimeSeconds;
            var normalizedTime = Mathf.Clamp01(elapsedSeconds / _chargeDurationSeconds);
            _view.GlowIntensity = normalizedTime;
            _view.RequestRedraw();
            ApplyChargeShake(normalizedTime);

            if (normalizedTime < FullProgress)
            {
                return;
            }

            _view.AnchoredPosition = _chargeOriginAnchoredPosition;
            _isChargePlaying = false;
        }

        private void AdvanceBurstAnimation()
        {
            var elapsedSeconds = Time.realtimeSinceStartup - _burstStartedAtRealtimeSeconds;
            var normalizedTime = Mathf.Clamp01(elapsedSeconds / _burstDurationSeconds);
            var easedProgress = EvaluateEasing(normalizedTime, RadarChartEasing.OutCubic);
            _view.NormalizedValue = Mathf.LerpUnclamped(_burstStartValue, 0f, easedProgress);
            _view.GlowIntensity = Mathf.LerpUnclamped(_burstStartGlowIntensity, 0f, easedProgress);
            _view.AnchoredPosition = _burstOriginAnchoredPosition;
            _view.RequestRedraw();

            if (normalizedTime < FullProgress)
            {
                return;
            }

            _view.NormalizedValue = 0f;
            _view.GlowIntensity = 0f;
            _view.AnchoredPosition = _burstOriginAnchoredPosition;
            _isBurstPlaying = false;
            _view.RequestRedraw();
        }

        private void UpdateAnimatingState()
        {
            _view.SetAnimating(_isValueAnimating || _isChargePlaying || _isBurstPlaying);
        }

        private void ApplyChargeShake(float normalizedTime)
        {
            if (_chargeShakeAmplitudePixels <= 0f)
            {
                _view.AnchoredPosition = _chargeOriginAnchoredPosition;
                return;
            }

            var amplitude = Mathf.LerpUnclamped(0f, _chargeShakeAmplitudePixels, EvaluateOutBack(normalizedTime));
            var shakeOffsetX = Mathf.Sin(normalizedTime * ShakeOscillationCycles * Tau) * amplitude;
            _view.AnchoredPosition = new Vector2(_chargeOriginAnchoredPosition.x + shakeOffsetX, _chargeOriginAnchoredPosition.y);
        }

        private Vector2 ResolveRestingAnchoredPosition()
        {
            if (_isChargePlaying)
            {
                return _chargeOriginAnchoredPosition;
            }

            if (_isBurstPlaying)
            {
                return _burstOriginAnchoredPosition;
            }

            return _view.AnchoredPosition;
        }

        private static float EvaluateEasing(float normalizedTime, RadarChartEasing easing)
        {
            switch (easing)
            {
                case RadarChartEasing.None:
                case RadarChartEasing.Linear:
                    return normalizedTime;
                case RadarChartEasing.OutCubic:
                    return 1f - Mathf.Pow(1f - normalizedTime, 3f);
                case RadarChartEasing.OutBack:
                    {
                        var inverse = normalizedTime - 1f;
                        return 1f + ((OutBackOvershoot + 1f) * inverse * inverse * inverse) + (OutBackOvershoot * inverse * inverse);
                    }
                default:
                    return normalizedTime;
            }
        }

        private static float EvaluateOutBack(float normalizedTime)
        {
            var inverse = normalizedTime - 1f;
            return 1f + ((OutBackOvershoot + 1f) * inverse * inverse * inverse) + (OutBackOvershoot * inverse * inverse);
        }
    }
}
