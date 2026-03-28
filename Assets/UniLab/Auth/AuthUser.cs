namespace UniLab.Auth
{
    /// <summary>
    /// Represents an authenticated user session, including tokens and identity metadata.
    /// </summary>
    [System.Serializable]
    public class AuthUser
    {
        /// <summary>Unique user identifier.</summary>
        public string UserId;

        /// <summary>Email address. Empty for anonymous users.</summary>
        public string Email;

        /// <summary>True if the user signed in anonymously without a persistent identity.</summary>
        public bool IsAnonymous;

        /// <summary>JWT access token used for API authorization.</summary>
        public string AccessToken;

        /// <summary>Token used to obtain a new access token without re-authentication.</summary>
        public string RefreshToken;

        /// <summary>Access token expiry as Unix timestamp (seconds).</summary>
        public long ExpiresAt;
    }
}
