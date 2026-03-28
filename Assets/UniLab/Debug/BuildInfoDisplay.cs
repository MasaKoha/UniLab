using TMPro;
using UnityEngine;

namespace UniLab.Debug
{
    /// <summary>
    /// Displays the application version and build GUID on a TMP_Text component.
    /// Attach to any UI GameObject that should show build info to testers.
    /// </summary>
    public class BuildInfoDisplay : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _versionText;

        private void Start()
        {
            _versionText.text = BuildBuildInfoText();
        }

        private static string BuildBuildInfoText()
        {
            // buildGUID is empty in the Unity Editor; use a readable fallback to avoid
            // showing a confusing empty parenthetical in debug builds.
            if (string.IsNullOrEmpty(Application.buildGUID))
            {
                return $"v{Application.version} (editor)";
            }

            return $"v{Application.version} ({Application.buildGUID[..8]})";
        }
    }
}
