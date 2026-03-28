using System;
using Cysharp.Threading.Tasks;

namespace UniLab.UI.Popup
{
    public interface IPopupParameter
    {
        // Whether to respond to back key press
        public bool EnableBackKey { get; }

        // Function to execute on back key press. If null, default close behavior is used.
        public Func<UniTask> CustomBackAsync { get; }
        public bool EnableBackgroundClose { get; }
    }
}
