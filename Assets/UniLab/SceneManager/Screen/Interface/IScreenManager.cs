using System;
using Cysharp.Threading.Tasks;
using R3;

namespace UniLab.Scene.Screen
{
    /// <summary>
    /// Manages screen transitions and exposes the current screen state as an observable stream.
    /// </summary>
    public interface IScreenManager
    {
        /// <summary>
        /// Emits the new IScreenView each time the active screen changes.
        /// </summary>
        Observable<IScreenView> OnScreenChanged { get; }

        /// <summary>
        /// Shows the screen identified by <paramref name="type"/>, hiding the current screen if any.
        /// </summary>
        UniTask ShowAsync(Enum type);

        /// <summary>
        /// Returns to the previously shown screen. Does nothing if there is no history.
        /// </summary>
        UniTask BackAsync();
    }
}
