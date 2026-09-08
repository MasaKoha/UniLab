using System;
using R3;
using UnityEngine;

namespace UniLab.UI
{
    /// <summary>
    /// RadarChartView の値アニメーションの進行だけを担当する。
    /// 値バッファの更新タイミングを管理し、描画と軸情報は View 側に残す。
    /// </summary>
    internal sealed class RadarChartAnimation : IDisposable
    {
        private const float MinimumAnimationDurationSeconds = 0.0001f;
        private const float OutBackOvershoot = 1.70158f;

        private readonly RadarChartView _view;

        private float[] _animationStartValues = Array.Empty<float>();
        private float[] _animationTargetValues = Array.Empty<float>();
        private IDisposable _animationSubscription;
        private float _animationStartedAtRealtimeSeconds;
        private float _animationDurationSeconds;
        private RadarChartEasing _animationEasing;

        internal RadarChartAnimation(RadarChartView view)
        {
            _view = view;
        }

        internal void Initialize(int axisCount)
        {
            _animationStartValues = new float[axisCount];
            _animationTargetValues = new float[axisCount];
        }

        internal void AnimateTo(ReadOnlySpan<float> targetValues, float durationSeconds, RadarChartEasing easing)
        {
            _view.CopyClampedValues(targetValues, _animationTargetValues);
            StartAnimation(durationSeconds, easing, zeroStart: false);
        }

        internal void PlayGrowFromCenter(float durationSeconds)
        {
            for (var axisIndex = 0; axisIndex < _view.AxisCount; axisIndex++)
            {
                _animationTargetValues[axisIndex] = _view.NormalizedValues[axisIndex];
            }

            StartAnimation(durationSeconds, RadarChartEasing.OutBack, zeroStart: true);
        }

        public void Dispose()
        {
            StopAnimation();
        }

        private void StartAnimation(float durationSeconds, RadarChartEasing easing, bool zeroStart)
        {
            StopAnimation();

            if (durationSeconds <= 0f || easing == RadarChartEasing.None)
            {
                ApplyAnimationValues(1f, zeroStart);
                return;
            }

            for (var axisIndex = 0; axisIndex < _view.AxisCount; axisIndex++)
            {
                _animationStartValues[axisIndex] = zeroStart ? 0f : _view.NormalizedValues[axisIndex];
            }

            if (zeroStart)
            {
                for (var axisIndex = 0; axisIndex < _view.AxisCount; axisIndex++)
                {
                    _view.NormalizedValues[axisIndex] = 0f;
                }

                _view.RequestRedraw();
            }

            _animationStartedAtRealtimeSeconds = Time.realtimeSinceStartup;
            _animationDurationSeconds = Mathf.Max(MinimumAnimationDurationSeconds, durationSeconds);
            _animationEasing = easing;
            _view.SetAnimating(true);
            _animationSubscription = Observable.EveryUpdate(_view.destroyCancellationToken)
                .Subscribe(_ => AdvanceAnimation());
        }

        private void AdvanceAnimation()
        {
            // IsAnimating は自動プロパティ（マネージド側）なので、View が Destroy されても読めてしまう。
            // ここで破棄を検知しないと RequestRedraw のネイティブアクセスで毎フレーム
            // MissingReferenceException を出し続ける。Unity の等値演算子は破棄済みを null とみなす。
            if (_view == null)
            {
                StopAnimation();
                return;
            }

            if (!_view.IsAnimating)
            {
                return;
            }

            var elapsedSeconds = Time.realtimeSinceStartup - _animationStartedAtRealtimeSeconds;
            var normalizedTime = Mathf.Clamp01(elapsedSeconds / _animationDurationSeconds);
            ApplyAnimationValues(EvaluateEasing(normalizedTime, _animationEasing), zeroStart: false);

            if (normalizedTime < 1f)
            {
                return;
            }

            for (var axisIndex = 0; axisIndex < _view.AxisCount; axisIndex++)
            {
                _view.NormalizedValues[axisIndex] = _animationTargetValues[axisIndex];
            }

            StopAnimation();
            _view.RequestRedraw();
        }

        private void ApplyAnimationValues(float easedProgress, bool zeroStart)
        {
            for (var axisIndex = 0; axisIndex < _view.AxisCount; axisIndex++)
            {
                var startValue = zeroStart ? 0f : _animationStartValues[axisIndex];
                _view.NormalizedValues[axisIndex] = Mathf.Clamp(Mathf.LerpUnclamped(startValue, _animationTargetValues[axisIndex], easedProgress), 0f, _view.MaximumNormalizedValue);
            }

            _view.RequestRedraw();
        }

        private void StopAnimation()
        {
            _animationSubscription?.Dispose();
            _animationSubscription = null;
            if (_view != null)
            {
                _view.SetAnimating(false);
            }
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
    }
}
