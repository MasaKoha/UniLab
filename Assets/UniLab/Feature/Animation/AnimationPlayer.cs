using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace UniLab.Feature.Animation
{
    [RequireComponent(typeof(Animator))]
    public sealed class AnimationPlayer : MonoBehaviour
    {
        [SerializeField] private string _animationName = "";

        [SerializeField] private bool _playOnAwake = false;

        [SerializeField] private float _playbackSpeed = 1.0f;

        [SerializeField] private int _loopCount = 1; // 0: 無限ループ, 1以上: 指定回数ループ

        private Subject<Unit> _onPlay;
        public Observable<Unit> OnPlay => _onPlay ??= new Subject<Unit>();
        private Subject<Unit> _onComplete;

        public Observable<Unit> OnComplete => _onComplete ??= new Subject<Unit>();
        private Animator _targetAnimator = null;
        public bool IsPlaying { get; private set; } = false;

        private CancellationTokenSource _tokenSource;

        private void Awake()
        {
            _targetAnimator = GetComponent<Animator>();
            if (_playOnAwake)
            {
                PlayAsync().Forget();
            }
        }

        public async UniTask PlayAsync(CancellationToken token)
        {
            await PlayAsync(null, null, token);
        }

        public async UniTask PlayAsync(string targetAnimationName, CancellationToken token)
        {
            await PlayAsync(targetAnimationName, null, token);
        }

        public async UniTask PlayAsync(string targetAnimationName = null, int? loopCount = null, CancellationToken token = default)
        {
            var animationName = targetAnimationName ?? _animationName;
            var loops = loopCount ?? _loopCount;
            if (_targetAnimator == null || string.IsNullOrEmpty(animationName))
            {
                Debug.LogWarning($"[{nameof(AnimationPlayer)}] Animation name is null or empty. Cannot play animation.");
                return;
            }

            IsPlaying = true;
            _targetAnimator.speed = _playbackSpeed;
            _onPlay?.OnNext(Unit.Default);

            var played = 0;
            if (!token.CanBeCanceled)
            {
                // トークンの指定が無ければ自前で作成
                _tokenSource = new CancellationTokenSource();
                token = _tokenSource.Token;
            }

            try
            {
                while ((loops == 0 || played < loops) && !token.IsCancellationRequested)
                {
                    _targetAnimator.Play(animationName, 0, 0f);

                    await UniTask.WaitUntil(() =>
                    {
                        var stateInfo = _targetAnimator.GetCurrentAnimatorStateInfo(0);
                        return stateInfo.IsName(animationName) && stateInfo.normalizedTime >= 1f;
                    }, cancellationToken: token);

                    played++;
                }
            }
            catch (OperationCanceledException)
            {
            }

            IsPlaying = false;
            _onComplete?.OnNext(Unit.Default);
        }

        private void OnDestroy()
        {
            _onPlay?.Dispose();
            _onComplete?.Dispose();
            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
        }
    }
}