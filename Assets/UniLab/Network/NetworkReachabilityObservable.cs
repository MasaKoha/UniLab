using System;
using R3;
using UnityEngine;

namespace UniLab.Network
{
    /// <summary>
    /// Polls <see cref="Application.internetReachability"/> on a fixed interval and emits
    /// only when the value changes. Useful for reacting to connectivity transitions
    /// without coupling to MonoBehaviour lifecycle.
    /// </summary>
    public sealed class NetworkReachabilityObservable : IDisposable
    {
        // --- Fields ---

        private readonly Subject<NetworkReachability> _subject = new();
        private readonly IDisposable _subscription;
        private NetworkReachability _lastReachability;

        // --- Properties ---

        /// <summary>
        /// Emits the new <see cref="NetworkReachability"/> value whenever connectivity state changes.
        /// </summary>
        public Observable<NetworkReachability> OnReachabilityChanged => _subject;

        // --- Constructor ---

        /// <summary>
        /// Initializes the observable and starts polling at the specified interval.
        /// </summary>
        /// <param name="pollingInterval">How often to check reachability. Defaults to 3 seconds.</param>
        public NetworkReachabilityObservable(TimeSpan pollingInterval = default)
        {
            var interval = pollingInterval == default ? TimeSpan.FromSeconds(3) : pollingInterval;

            _lastReachability = Application.internetReachability;

            _subscription = Observable
                .Interval(interval)
                .Subscribe(_ => CheckReachability());
        }

        // --- Public methods ---

        /// <summary>
        /// Stops polling and completes the inner subject.
        /// </summary>
        public void Dispose()
        {
            _subscription.Dispose();
            _subject.Dispose();
        }

        // --- Private helpers ---

        private void CheckReachability()
        {
            var current = Application.internetReachability;
            if (current == _lastReachability)
            {
                return;
            }

            _lastReachability = current;
            _subject.OnNext(current);
        }
    }
}
