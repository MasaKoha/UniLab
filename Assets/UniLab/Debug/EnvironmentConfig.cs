using UnityEngine;

namespace UniLab.Diagnostics
{
    /// <summary>
    /// Deployment environment identifier.
    /// </summary>
    public enum Environment
    {
        Development,
        Staging,
        Production,
    }

    /// <summary>
    /// ScriptableObject that holds per-environment configuration such as the API base URL.
    /// Create one asset per environment and swap them via the build pipeline or addressables.
    /// </summary>
    [CreateAssetMenu(menuName = "UniLab/EnvironmentConfig")]
    public class EnvironmentConfig : ScriptableObject
    {
        /// <summary>Base URL for the backend API (e.g. https://api.dev.example.com).</summary>
        [SerializeField]
        private string _apiBaseUrl;

        /// <summary>Active deployment environment for this asset.</summary>
        [SerializeField]
        private Environment _environment;

        /// <summary>
        /// Base URL for the backend API. Corresponds to the selected environment.
        /// </summary>
        public string ApiBaseUrl => _apiBaseUrl;

        /// <summary>
        /// Deployment environment this asset targets.
        /// </summary>
        public Environment TargetEnvironment => _environment;
    }
}
