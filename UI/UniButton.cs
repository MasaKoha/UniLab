using System;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.UI;

namespace UniLab.UI
{
    public class UniButton : Button
    {
        public void OnClick(Action onClickAction)
        {
            onClick.AsObservable().Subscribe(_ => onClickAction.Invoke()).AddTo(this);
        }

        public void OnClick(Func<UniTask> onClickAction)
        {
            onClick.AsObservable().Subscribe(_ => onClickAction.Invoke()).AddTo(this);
        }
    }
}