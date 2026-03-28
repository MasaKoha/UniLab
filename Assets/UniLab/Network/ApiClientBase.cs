using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace UniLab.Network
{
    /// <summary>
    /// Abstract base class for BFF API clients. Handles JSON serialization, JWT auth headers,
    /// timeout, and exponential-backoff retry for transient errors.
    /// Designed for pure C# DI usage — no MonoBehaviour dependency.
    /// </summary>
    public abstract class ApiClientBase
    {
        // --- Constants ---

        private const int MaxRetryCount = 3;
        private const int RetryBaseDelayMilliseconds = 1000;

        // --- Fields ---

        /// <summary>
        /// Timeout seconds per request attempt. Override in subclass constructor if needed.
        /// </summary>
        protected int _timeoutSeconds = 10;

        // --- Abstract members ---

        /// <summary>Base URL of the BFF server (e.g. "https://api.example.com").</summary>
        protected abstract string BaseUrl { get; }

        /// <summary>
        /// Returns the current JWT access token. Return empty string to skip the Authorization header.
        /// </summary>
        protected abstract string GetAccessToken();

        // --- Protected API methods ---

        /// <summary>
        /// Sends a GET request to <paramref name="path"/> and deserializes the response as <typeparamref name="TResponse"/>.
        /// </summary>
        protected async UniTask<TResponse> GetAsync<TResponse>(
            string path,
            CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(path);
            using var request = UnityWebRequest.Get(url);
            SetCommonHeaders(request);
            return await SendWithRetryAsync<TResponse>(request, retryCount: 0, cancellationToken);
        }

        /// <summary>
        /// Sends a POST request with a JSON-serialized <paramref name="body"/> to <paramref name="path"/>
        /// and deserializes the response as <typeparamref name="TResponse"/>.
        /// </summary>
        protected async UniTask<TResponse> PostAsync<TResponse, TRequest>(
            string path,
            TRequest body,
            CancellationToken cancellationToken = default)
        {
            var url = BuildUrl(path);
            var json = JsonUtility.ToJson(body);
            var uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json))
            {
                contentType = "application/json"
            };
            using var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = uploadHandler,
                downloadHandler = new DownloadHandlerBuffer()
            };
            SetCommonHeaders(request);
            return await SendWithRetryAsync<TResponse>(request, retryCount: 0, cancellationToken);
        }

        // --- Virtual hook ---

        /// <summary>
        /// Called when the server returns 401. Override to refresh the access token before the caller retries.
        /// </summary>
        protected virtual void OnUnauthorized()
        {
        }

        // --- Private helpers ---

        private string BuildUrl(string path)
        {
            // Avoid double slashes when path already starts with '/'.
            return BaseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        }

        private void SetCommonHeaders(UnityWebRequest request)
        {
            request.SetRequestHeader("Accept", "application/json");

            var token = GetAccessToken();
            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
            }
        }

        private async UniTask<TResponse> SendWithRetryAsync<TResponse>(
            UnityWebRequest request,
            int retryCount,
            CancellationToken cancellationToken)
        {
            request.timeout = _timeoutSeconds;

            await request.SendWebRequest().WithCancellation(cancellationToken);

            var statusCode = (int)request.responseCode;

            if (request.result == UnityWebRequest.Result.Success)
            {
                var responseText = request.downloadHandler.text;
                return JsonUtility.FromJson<TResponse>(responseText);
            }

            var responseBody = request.downloadHandler?.text ?? string.Empty;

            if (statusCode == 401)
            {
                OnUnauthorized();
                throw new UnauthorizedException(responseBody);
            }

            if (statusCode == 400 || statusCode == 404)
            {
                throw new ApiException(statusCode, responseBody, $"Request failed with status {statusCode}.");
            }

            // Retry only on 429 and 5xx (transient errors).
            var isRetryable = statusCode == 429 || statusCode >= 500;
            if (isRetryable && retryCount < MaxRetryCount)
            {
                // Exponential backoff: 1s, 2s, 4s.
                var delayMilliseconds = RetryBaseDelayMilliseconds * (int)Math.Pow(2, retryCount);
                await UniTask.Delay(delayMilliseconds, cancellationToken: cancellationToken);

                // Recreate the request because UnityWebRequest cannot be resent after completion.
                using var retryRequest = CloneRequest(request);
                return await SendWithRetryAsync<TResponse>(retryRequest, retryCount + 1, cancellationToken);
            }

            if (statusCode == 429)
            {
                throw new TooManyRequestsException(responseBody);
            }

            if (statusCode == 503)
            {
                throw new ServiceUnavailableException(responseBody);
            }

            throw new ApiException(statusCode, responseBody, $"Request failed with status {statusCode}.");
        }

        /// <summary>
        /// Clones a completed <see cref="UnityWebRequest"/> so it can be resent.
        /// UnityWebRequest is single-use; cloning is required for retry.
        /// </summary>
        private UnityWebRequest CloneRequest(UnityWebRequest source)
        {
            var clone = new UnityWebRequest(source.url, source.method)
            {
                timeout = source.timeout,
                downloadHandler = new DownloadHandlerBuffer()
            };

            if (source.uploadHandler != null)
            {
                // Re-wrap the same raw bytes — upload data does not change across retries.
                clone.uploadHandler = new UploadHandlerRaw(source.uploadHandler.data)
                {
                    contentType = source.uploadHandler.contentType
                };
            }

            // Copy headers by re-applying common headers through the same path.
            clone.SetRequestHeader("Accept", "application/json");
            var token = GetAccessToken();
            if (!string.IsNullOrEmpty(token))
            {
                clone.SetRequestHeader("Authorization", "Bearer " + token);
            }

            return clone;
        }
    }
}
