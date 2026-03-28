namespace UniLab.Auth
{
    /// <summary>
    /// Thrown when an authentication operation fails. Wraps provider-specific exceptions with a normalized error code.
    /// </summary>
    public class AuthException : System.Exception
    {
        /// <summary>Normalized error category for programmatic handling.</summary>
        public AuthErrorCode ErrorCode { get; }

        /// <summary>
        /// Initializes a new instance with the given error code and human-readable message.
        /// </summary>
        public AuthException(AuthErrorCode errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Categorizes authentication failures for structured error handling.
    /// </summary>
    public enum AuthErrorCode
    {
        /// <summary>An unclassified or unexpected error occurred.</summary>
        Unknown,

        /// <summary>The provided email or password is incorrect.</summary>
        InvalidCredentials,

        /// <summary>The email address is already registered.</summary>
        EmailAlreadyInUse,

        /// <summary>A network error prevented the request from completing.</summary>
        NetworkError,

        /// <summary>The session has expired and could not be refreshed.</summary>
        SessionExpired,

        /// <summary>No account exists for the given identifier.</summary>
        AccountNotFound,
    }
}
