using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.Scene
{
    public abstract class SceneMainBase : MonoBehaviour
    {
        private bool _initialized;
        protected SceneParameterBase Parameter { get; private set; }

        public void SetParameter(SceneParameterBase param)
        {
            Parameter = param;
        }

        private void Start()
        {
            GC.Collect();
            OnStart();
        }

        protected virtual void OnStart()
        {
        }

        protected virtual void OnSetup()
        {
        }

        protected virtual void OnInitialize()
        {
        }

        protected virtual UniTask OnPreEnterAsync()
        {
            return UniTask.CompletedTask;
        }

        protected virtual void OnEnter()
        {
        }

        protected virtual UniTask OnTransitionAsync()
        {
            return UniTask.CompletedTask;
        }

        protected virtual void OnLeave()
        {
        }

        public void Setup()
        {
            OnSetup();
        }

        public bool Initialize()
        {
            if (_initialized)
            {
                return true;
            }

            _initialized = true;
            OnInitialize();
            return false;
        }

        public virtual UniTask UpdateContentAsync(SceneParameterBase parameter)
        {
            Parameter = parameter;
            return UniTask.CompletedTask;
        }

        public async UniTask PreEnterAsync()
        {
            await OnPreEnterAsync();
        }

        public void Enter()
        {
            OnEnter();
        }

        public async UniTask TransitionAsync()
        {
            await OnTransitionAsync();
        }

        public void Leave()
        {
            OnLeave();
        }
    }
}