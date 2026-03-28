using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace UniLab.Auth
{
    /// <summary>
    /// Contract for authentication operations. Supports email/password, anonymous, and social sign-in flows.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Current authenticated user. null if not signed in.
        /// </summary>
        AuthUser CurrentUser { get; }

        /// <summary>
        /// Emits when auth state changes (sign-in, sign-out, token refresh).
        /// </summary>
        Observable<AuthUser> OnAuthStateChanged { get; }

        /// <summary>
        /// Creates a new account with email and password.
        /// </summary>
        UniTask<AuthUser> SignUpAsync(string email, string password, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs in with email and password.
        /// </summary>
        UniTask<AuthUser> SignInWithEmailAsync(string email, string password, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs in as an anonymous user.
        /// </summary>
        UniTask<AuthUser> SignInAnonymouslyAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs in using a Google ID token obtained from the Google Sign-In SDK.
        /// </summary>
        UniTask<AuthUser> SignInWithGoogleAsync(string idToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs in using an Apple ID token and nonce obtained from Sign in with Apple.
        /// </summary>
        UniTask<AuthUser> SignInWithAppleAsync(string idToken, string nonce, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restore session from local cache. Returns null if no valid session exists.
        /// </summary>
        UniTask<AuthUser> RestoreSessionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Link an additional identity to the current anonymous user.
        /// </summary>
        UniTask<AuthUser> LinkIdentityAsync(string provider, string idToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Signs out the current user and clears the local session cache.
        /// </summary>
        UniTask SignOutAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Permanently deletes the current user's account.
        /// </summary>
        UniTask DeleteAccountAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a fresh access token, refreshing if necessary.
        /// </summary>
        UniTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    }
}
