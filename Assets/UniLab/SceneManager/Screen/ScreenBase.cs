using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.Scene.Screen
{
    public abstract class ScreenBase : MonoBehaviour, IDisposable
    {
        public abstract Enum Type { get; protected set; }
        private bool _isInitialized = false;

        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            OnInitialize();
            _isInitialized = true;
        }

        public async UniTask PreShowAsync()
        {
            await OnPreShowAsync();
        }

        public void Show()
        {
            OnShow();
        }

        public async UniTask PreHideAsync()
        {
            await OnPreHideAsync();
        }

        public void Hide()
        {
            OnHide();
        }

        protected abstract void OnInitialize();
        protected abstract UniTask OnPreShowAsync();
        protected abstract void OnShow();
        protected abstract UniTask OnPreHideAsync();
        protected abstract void OnHide();
        public abstract void Dispose();
    }
}