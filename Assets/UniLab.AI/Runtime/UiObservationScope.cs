#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;

namespace UniLab.AI
{
    /// <summary>保存用の全観測を保持したまま、表示対象だけを絞り込みます。</summary>
    internal static class UiObservationScope
    {
        /// <summary>観測範囲の誤指定を入口で拒否します。</summary>
        internal static void Validate(string scope)
        {
            if (scope != "visible" && scope != "all")
            {
                throw new ArgumentException("scope は visible または all を指定してください。", nameof(scope));
            }
        }

        /// <summary>元の観測を変更せず、指定範囲の要素だけを返します。</summary>
        internal static UiSnapshotDocument Filter(UiSnapshotDocument document, string scope)
        {
            if (document == null)
            {
                return null;
            }

            if (scope != null)
            {
                Validate(scope);
            }

            var elements = new List<UiSnapshotElement>();
            var focusedPath = string.Empty;
            foreach (var element in document.elements ?? Array.Empty<UiSnapshotElement>())
            {
                if (element == null || (scope != "all" && element.offscreen) || (scope == "visible" && element.clipped))
                {
                    continue;
                }

                if (scope == "visible" && element.kind == "Text" && !string.IsNullOrEmpty(element.blockedBy))
                {
                    continue;
                }

                elements.Add(element);
                if (element.path == document.focusedPath)
                {
                    focusedPath = document.focusedPath;
                }
            }

            return new UiSnapshotDocument
            {
                capturedAt = document.capturedAt,
                frame = document.frame,
                activeScene = document.activeScene,
                screenWidth = document.screenWidth,
                screenHeight = document.screenHeight,
                focusedPath = focusedPath,
                elements = elements.ToArray(),
                game = document.game,
            };
        }
    }
}
#endif
