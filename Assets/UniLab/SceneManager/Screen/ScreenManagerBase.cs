using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace UniLab.Scene.Screen
{
    public abstract class ScreenManagerBase<TScreenBase> : MonoBehaviour where TScreenBase : ScreenBase
    {
        private List<TScreenBase> _screens;
        private ScreenBase _currentScreen;

        public void RegisterScreens(List<TScreenBase> screens)
        {
            _screens = screens;
            foreach (var screen in _screens)
            {
                screen.Initialize();
                screen.gameObject.SetActive(false);
            }
        }

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

            _currentScreen = screen;
            _currentScreen.gameObject.SetActive(true);
            await _currentScreen.PreShowAsync();
            _currentScreen.Show();
        }
    }
}