using UniLab.Common;
using UnityEngine;

namespace UniLab.UI
{
    public abstract class UIInputBlockingManagerBase<TClass> : SingletonMonoBehaviour<TClass> where TClass : MonoBehaviour
    {
        [SerializeField] private Canvas _canvas = null;
        protected Canvas Canvas => _canvas;
        public abstract void Show();
        public abstract void Hide();
    }
}
