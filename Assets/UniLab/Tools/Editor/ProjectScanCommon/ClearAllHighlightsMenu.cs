using System.Collections.Generic;
using UniLab.Tools.Editor.AssetReferenceFinder;
using UniLab.Tools.Editor.MissingChecker;
using UniLab.Tools.Editor.UnreferencedAssetFinder;
using UnityEditor;

namespace UniLab.Tools.Editor.ProjectScanCommon
{
    /// <summary>
    /// Menu item that clears all Project/Hierarchy highlight overlays at once.
    /// </summary>
    public static class ClearAllHighlightsMenu
    {
        /// <summary>
        /// Clears highlights from AssetReferenceFinder, MissingChecker, and UnreferencedAssetFinder.
        /// </summary>
        [MenuItem("UniLab/Clear All Highlights", false, -50)]
        private static void ClearAllHighlights()
        {
            ProjectAssetReferenceHighlighter.Clear();
            ProjectMissingCheckerProjectHighlighter.SetMissingGuids(
                new List<string>(), new HashSet<string>());
            ProjectUnreferencedAssetHighlighter.Clear();
        }
    }
}
