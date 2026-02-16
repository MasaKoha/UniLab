using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace UniLab.Tools.Editor.ProjectScanCommon
{
    public static class ProjectScanFilterUtility
    {
        public static HashSet<string> BuildExtensionFilter(string extensionsCsv)
        {
            var set = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(extensionsCsv))
            {
                return set;
            }

            var items = extensionsCsv.Split(',');
            foreach (var item in items)
            {
                var ext = item.Trim().TrimStart('.');
                if (string.IsNullOrEmpty(ext))
                {
                    continue;
                }

                set.Add(ext.ToLowerInvariant());
            }

            return set;
        }

        public static bool PassExtensionFilter(string path, HashSet<string> extensionFilter)
        {
            if (extensionFilter == null || extensionFilter.Count == 0)
            {
                return true;
            }

            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            if (!extensionFilter.Contains(ext))
            {
                return false;
            }

            return true;
        }

        public static List<string> BuildFolderRoots(IEnumerable<DefaultAsset> folders)
        {
            var roots = new List<string>();
            if (folders == null)
            {
                return roots;
            }

            foreach (var folder in folders)
            {
                if (folder == null)
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(folder);
                if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                if (!path.EndsWith("/"))
                {
                    path += "/";
                }

                roots.Add(path);
            }

            return roots;
        }

        public static bool PassFolderFilter(string path, List<string> roots)
        {
            if (roots == null || roots.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < roots.Count; i++)
            {
                if (path.StartsWith(roots[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}