using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.AnimationQueue
{
    public abstract class AnimationBase<T> : MonoBehaviour, IScreenAnimation where T : AnimationParameterBase
    {
        public T Parameter { get; private set; }

        public void Initialize(T parameter)
        {
            Parameter = parameter;
        }

        protected abstract void OnInitialize(T parameter);
        public abstract void Play();
        public abstract UniTask Wait();
    }
}