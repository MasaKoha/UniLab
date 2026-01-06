using R3;
using UnityEngine.InputSystem;

namespace UniLab.Common.Input
{
    public sealed class BackKeyInputManager : SingletonMonoBehaviour<BackKeyInputManager>
    {
        private readonly Subject<Unit> _onPressBackKey = new();
        public Observable<Unit> OnPressBackKey => _onPressBackKey;
        public bool IsBlocked { get; private set; } = false;
#if UNITY_ANDROID

        protected override void OnAwake()
        {
            SetDontDestroyOnLoad();
        }

        private void Update()
        {
            if (!Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            GoBack();
        }

        private void GoBack()
        {
            if (IsBlocked)
            {
                return;
            }

            _onPressBackKey.OnNext(Unit.Default);
        }

#endif
        public void SetBlock(bool block)
        {
            IsBlocked = block;
        }
    }
}