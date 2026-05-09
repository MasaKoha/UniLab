using System.Collections.Generic;

namespace UniLab.Tools.Editor.ComponentUsageProfiler
{
    /// <summary>
    /// Represents a single location where a MonoBehaviour script is placed (scene/prefab + GameObject path).
    /// </summary>
    public class ScriptUsageLocation
    {
        /// <summary>
        /// Scene path or Prefab asset path where the script is used.
        /// </summary>
        public string AssetPath;

        /// <summary>
        /// Hierarchical path of the GameObject (e.g. "Canvas/Panel/Button").
        /// </summary>
        public string GameObjectPath;
    }

    /// <summary>
    /// Represents the usage status of a single MonoBehaviour script file.
    /// </summary>
    public class ScriptUsageEntry
    {
        /// <summary>
        /// File path of the MonoBehaviour script (e.g. "Assets/_Project/Scripts/Foo.cs").
        /// </summary>
        public string ScriptPath;

        /// <summary>
        /// Fully qualified type name including namespace.
        /// </summary>
        public string TypeFullName;

        /// <summary>
        /// Short type name without namespace.
        /// </summary>
        public string TypeName;

        /// <summary>
        /// Whether this script is used in any scene or prefab.
        /// </summary>
        public bool IsUsed;

        /// <summary>
        /// Locations where this script is placed. Empty if not used.
        /// </summary>
        public List<ScriptUsageLocation> Locations = new();
    }
}
