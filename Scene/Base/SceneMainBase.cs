using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.Scene
{
    public abstract class SceneMainBase<T> : MonoBehaviour where T : SceneParameterBase, new()
    {
        private bool _isInitialized;
        public T Parameter { get; private set; } = new();

        private void Start()
        {
            UniLabSceneManager.Instance.Initialize();
            Sequence().Forget();
        }

        private async UniTask Sequence()
        {
            if (!_isInitialized)
            {
                await InitializeAsync(Parameter);
            }

            _isInitialized = true;

            await TransitionAsync();
            await PreEnterAsync();
            await EnterAsync();
        }

        protected abstract UniTask OnInitializeAsync();
        protected abstract UniTask OnTransitionAsync();
        protected abstract UniTask OnPreEnterAsync();
        protected abstract UniTask OnEnterAsync();
        protected abstract UniTask OnLeaveAsync();

        public async UniTask InitializeAsync(T parameter)
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            Parameter = parameter;
            await OnInitializeAsync();
        }

        public virtual UniTask UpdateContentAsync(T parameter)
        {
            Parameter = parameter;
            return UniTask.CompletedTask;
        }

        private async UniTask TransitionAsync()
        {
            await OnTransitionAsync();
        }

        private async UniTask PreEnterAsync()
        {
            await OnPreEnterAsync();
        }

        private async UniTask EnterAsync()
        {
            await OnEnterAsync();
        }

        private async UniTask LeaveAsync()
        {
            await OnLeaveAsync();
        }
    }
}