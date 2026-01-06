using System.Collections.Generic;
using UnityEngine;

namespace UniLab.Feature.Indicator
{
    public abstract class IndicatorManagerBase : MonoBehaviour
    {
        [SerializeField] private IndicatorCellBase _indicatorPrefab = null;
        private readonly List<IndicatorCellBase> _indicators = new();

        public void Initialize(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var instance = Instantiate(_indicatorPrefab);
                instance.Initialize(i);
                _indicators.Add(instance);
            }

            SetParent(_indicators);
        }

        protected abstract void SetParent(IReadOnlyList<IndicatorCellBase> indicatorInstances);

        public void ShowCurrentIndicator(int index)
        {
            foreach (var indicator in _indicators)
            {
                indicator.Show(indicator.Index == index);
            }
        }
    }
}