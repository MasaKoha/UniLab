using System;
using System.Text;

namespace UniLab.Network
{
    /// <summary>
    /// Base exception for all API communication errors, carrying HTTP status and raw response body.
    /// </summary>
    public class ApiException : Exception
    {
        /// <summary>HTTP status code returned by the server.</summary>
        public int StatusCode { get; }

        /// <summary>Raw response body bytes returned by the server.</summary>
        public byte[] ResponseBody { get; }

        /// <summary>
        /// UTF-8 decoded representation of <see cref="ResponseBody"/>.
        /// Returns an empty string when <see cref="ResponseBody"/> is <c>null</c>.
        /// </summary>
        public string ResponseBodyAsString => ResponseBody == null ? string.Empty : Encoding.UTF8.GetString(ResponseBody);

        /// <summary>
        /// Initializes a new instance of <see cref="ApiException"/>.
        /// </summary>
        public ApiException(int statusCode, byte[] responseBody, string message)
            : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }

    /// <summary>
    /// Thrown when the server returns HTTP 401 Unauthorized.
    /// </summary>
    public class UnauthorizedException : ApiException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="UnauthorizedException"/>.
        /// </summary>
        public UnauthorizedException(byte[] responseBody)
            : base(401, responseBody, "Unauthorized: access token is invalid or expired.")
        {
        }
    }

    /// <summary>
    /// Thrown when the server returns HTTP 429 Too Many Requests.
    /// </summary>
    public class TooManyRequestsException : ApiException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="TooManyRequestsException"/>.
        /// </summary>
        public TooManyRequestsException(byte[] responseBody)
            : base(429, responseBody, "Too many requests: rate limit exceeded.")
        {
        }
    }

    /// <summary>
    /// Thrown when the server returns HTTP 503 Service Unavailable.
    /// </summary>
    public class ServiceUnavailableException : ApiException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ServiceUnavailableException"/>.
        /// </summary>
        public ServiceUnavailableException(byte[] responseBody)
            : base(503, responseBody, "Service unavailable: the server is temporarily down.")
        {
        }
    }
}
