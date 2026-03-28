using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace UniLab.Scene.Screen
{
    /// <summary>
    /// Base class for screen managers. Maintains a list of screens, handles show/hide transitions,
    /// exposes OnScreenChanged as an observable, and implements history-based BackAsync.
    /// </summary>
    public abstract class ScreenManagerBase<TScreenBase> : MonoBehaviour, IScreenManager
        where TScreenBase : ScreenBase
    {
        private List<TScreenBase> _screens;
        private ScreenBase _currentScreen;
        private readonly Stack<Enum> _history = new();
        private readonly Subject<IScreenView> _onScreenChangedSubject = new();

        /// <summary>
        /// Emits the newly shown IScreenView each time ShowAsync transitions to a different screen.
        /// </summary>
        public Observable<IScreenView> OnScreenChanged => _onScreenChangedSubject;

        /// <summary>
        /// Registers and initializes the managed screens. All screens start hidden.
        /// </summary>
        public void RegisterScreens(List<TScreenBase> screens)
        {
            _screens = screens;
            foreach (var screen in _screens)
            {
                screen.Initialize();
                screen.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Shows the screen identified by <paramref name="type"/>, hiding the current one if needed.
        /// Pushes the type onto the internal history stack and fires OnScreenChanged.
        /// </summary>
        public async UniTask ShowAsync(Enum type)
        {
            if (_currentScreen != null)
            {
                if (Equals(type, _currentScreen.Type))
                {
                    return;
                }

                await _currentScreen.PreHideAsync();
                _currentScreen.Hide();
                _currentScreen.gameObject.SetActive(false);
            }

            var screen = _screens.FirstOrDefault(s => Equals(s.Type, type));
            if (screen == null)
            {
                Debug.LogError($"Screen not found. TypeIndex: {type}");
                return;
            }

            _history.Push(type);
            _currentScreen = screen;
            _currentScreen.gameObject.SetActive(true);
            await _currentScreen.PreShowAsync();
            _currentScreen.Show();
            _onScreenChangedSubject.OnNext(_currentScreen);
        }

        /// <summary>
        /// Returns to the previous screen in the history stack.
        /// Pops the current entry, then peeks the target, then pops it again before calling ShowAsync —
        /// ShowAsync itself pushes the target back onto the stack, so the pre-pop prevents a duplicate entry.
        /// Does nothing if the history has one or fewer entries.
        /// </summary>
        public async UniTask BackAsync()
        {
            if (_history.Count <= 1)
            {
                return;
            }

            // Remove the current screen from history.
            _history.Pop();
            var previousType = _history.Peek();

            // Remove the previous entry too — ShowAsync will re-push it when it runs.
            _history.Pop();
            await ShowAsync(previousType);
        }

        private void OnDestroy()
        {
            _onScreenChangedSubject.Dispose();
        }
    }
}
