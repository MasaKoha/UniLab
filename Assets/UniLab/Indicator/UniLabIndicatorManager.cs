using System.Collections.Generic;
using UnityEngine;

namespace UniLab.Indicator
{
    public class UniLabIndicatorManager : IndicatorManagerBase
    {
        [SerializeField] private Transform _indicatorRoot = null;

        protected override void SetParent(IReadOnlyList<IndicatorCellBase> indicatorInstances)
        {
            foreach (var indicator in indicatorInstances)
            {
                indicator.transform.SetParent(_indicatorRoot);
            }
        }
    }
}
