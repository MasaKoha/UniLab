using System.Collections.Generic;

namespace UniLab.AssetDelivery
{
    /// <summary>
    /// Carries catalog update results so the boot sequence can decide whether download preparation should continue.
    /// </summary>
    public readonly record struct CatalogUpdateInfo
    {
        /// <summary>
        /// Gets whether the catalog check found and applied catalog updates.
        /// </summary>
        public bool HasUpdate { get; }

        /// <summary>
        /// Gets the catalog identifiers updated during the check so callers can log or inspect the change set.
        /// </summary>
        public IReadOnlyList<string> UpdatedCatalogIds { get; }

        /// <summary>
        /// Creates catalog update information returned from the delivery service to the boot sequence.
        /// </summary>
        public CatalogUpdateInfo(bool hasUpdate, IReadOnlyList<string> updatedCatalogIds)
        {
            HasUpdate = hasUpdate;
            UpdatedCatalogIds = updatedCatalogIds;
        }
    }
}
