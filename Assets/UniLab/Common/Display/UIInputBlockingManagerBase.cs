using UnityEngine;

namespace UniLab.Common.Display
{
    public abstract class UIInputBlockingManagerBase<TClass> : SingletonMonoBehaviour<TClass> where TClass : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas = null;
        protected Canvas Canvas => _canvas;
        public abstract void Show();
        public abstract void Hide();
    }
}