using UnityEngine;

namespace UniLab.Indicator
{
    public abstract class IndicatorCellBase : MonoBehaviour
    {
        public int Index { get; private set; }

        public void Initialize(int index)
        {
            Index = index;
            Show(false);
            OnInitialize();
        }

        protected abstract void OnInitialize();
        public abstract void Show(bool isShow);
    }
}
