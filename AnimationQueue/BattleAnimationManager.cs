using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.AnimationQueue
{
    public sealed class BattleAnimationManager : MonoBehaviour
    {
        private readonly Queue<IScreenAnimation> _screenAnimations = new();
        private bool _isPlaying = false;

        public void Initialize()
        {
        }

        public void Play(GameObject gameObjectInstance)
        {
            Play(gameObjectInstance.GetComponent<IScreenAnimation>());
        }

        public void Play(IScreenAnimation screenAnimation)
        {
            _screenAnimations.Enqueue(screenAnimation);
            if (_isPlaying) { return; }

            PlayQueueAnimation().Forget();
        }

        private async UniTask PlayQueueAnimation()
        {
            _isPlaying = true;
            while (_screenAnimations.Any())
            {
                var screenAnimation = _screenAnimations.Peek();
                await PlayAnimation(screenAnimation);
                _screenAnimations.Dequeue();
            }

            _isPlaying = false;
        }

        private async UniTask PlayAnimation(IScreenAnimation screenAnimation)
        {
            screenAnimation.Play();
            await screenAnimation.Wait();
        }

        public async UniTask Wait()
        {
            await UniTask.WaitUntil(() => !_screenAnimations.Any() || !_isPlaying);
        }
    }
}