using System;
using R3;
using VContainer.Unity;

namespace UniLab.Scene.Screen
{
    /// <summary>
    /// Base class for screen presenters managed by VContainer.
    /// Subclasses implement Initialize() to set up R3 subscriptions,
    /// which are automatically cleaned up via Disposables on Dispose().
    /// </summary>
    public abstract class ScreenPresenterBase : IInitializable, IDisposable
    {
        /// <summary>
        /// Disposable container for all R3 subscriptions created in Initialize().
        /// </summary>
        protected readonly CompositeDisposable Disposables = new();

        /// <summary>
        /// Called by VContainer after dependency injection. Set up subscriptions here.
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Disposes all subscriptions registered in Disposables.
        /// </summary>
        public void Dispose() => Disposables.Dispose();
    }
}
