using System;
using R3;
using UnityEngine;

namespace UniLab.AssetDelivery.Sample
{
    /// <summary>
    /// Defines the sample UI contract that the presenter observes and updates.
    /// </summary>
    public interface IAssetDeliverySampleView : IDisposable
    {
        /// <summary>
        /// Gets the event emitted by the view when the user requests service initialization.
        /// </summary>
        Observable<Unit> OnInitializeRequested { get; }

        /// <summary>
        /// Gets the event emitted by the view when the user requests catalog check and dependency download.
        /// </summary>
        Observable<Unit> OnCheckAndDownloadRequested { get; }

        /// <summary>
        /// Gets the event emitted by the view when the user requests scoped sprite loading.
        /// </summary>
        Observable<Unit> OnLoadAssetRequested { get; }

        /// <summary>
        /// Gets the event emitted by the view when the user requests cache cleanup.
        /// </summary>
        Observable<Unit> OnClearCacheRequested { get; }

        /// <summary>
        /// Updates the delivery state text shown by the presenter.
        /// </summary>
        void SetStateText(string text);

        /// <summary>
        /// Updates the download progress shown by the presenter.
        /// </summary>
        void SetProgress(float ratio);

        /// <summary>
        /// Updates the loaded sprite preview shown by the presenter.
        /// </summary>
        void SetLoadedSprite(Sprite sprite);

        /// <summary>
        /// Updates the operation message shown by the presenter.
        /// </summary>
        void SetMessage(string text);
    }
}
