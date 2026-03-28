using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace UniLab.Scene
{
    /// <summary>
    /// Base class for scene root objects. Manages the scene lifecycle and owns the VContainer LifetimeScope.
    /// </summary>
    public abstract class SceneMainBase : MonoBehaviour
    {
        [SerializeField] private LifetimeScope _lifetimeScope = null;

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

        private void OnDestroy()
        {
            _lifetimeScope?.Dispose();
        }
    }
}