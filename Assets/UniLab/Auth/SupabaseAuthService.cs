#if UNILAB_SUPABASE
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UniLab.Persistence;

namespace UniLab.Auth
{
    /// <summary>
    /// IAuthService implementation backed by Supabase. Pure C# class; no MonoBehaviour dependency.
    /// Session is persisted via LocalSave and restored on startup.
    /// </summary>
    public class SupabaseAuthService : IAuthService, IDisposable
    {
        private readonly Supabase.Client _supabaseClient;
        private readonly Subject<AuthUser> _currentUserSubject;
        private AuthUser _currentUser;

        /// <inheritdoc/>
        public AuthUser CurrentUser => _currentUser;

        /// <inheritdoc/>
        public Observable<AuthUser> OnAuthStateChanged => _currentUserSubject;

        /// <summary>
        /// Initializes the service with a pre-configured Supabase client.
        /// </summary>
        public SupabaseAuthService(Supabase.Client supabaseClient)
        {
            _supabaseClient = supabaseClient;
            _currentUserSubject = new Subject<AuthUser>();
        }

        /// <inheritdoc/>
        public async UniTask<AuthUser> SignUpAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _supabaseClient.Auth.SignUp(email, password).AsUniTask().AttachExternalCancellation(cancellationToken);
                return UpdateSession(response);
            }
            catch (Exception exception)
            {
                throw MapException(exception);
            }
        }

        /// <inheritdoc/>
        public async UniTask<AuthUser> SignInWithEmailAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _supabaseClient.Auth.SignIn(email, password).AsUniTask().AttachExternalCancellation(cancellationToken);
                return UpdateSession(response);
            }
            catch (Exception exception)
            {
                throw MapException(exception);
            }
        }

        /// <inheritdoc/>
        public async UniTask<AuthUser> SignInAnonymouslyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _supabaseClient.Auth.SignInAnonymously().AsUniTask().AttachExternalCancellation(cancellationToken);
                return UpdateSession(response);
            }
            catch (Exception exception)
            {
                throw MapException(exception);
            }
        }

        /// <inheritdoc/>
        public async UniTask<AuthUser> SignInWithGoogleAsync(string idToken, CancellationToken cancellationToken = default)
        {
            try
            {
                var credentials = new Supabase.Gotrue.Credentials
                {
                    Provider = Supabase.Gotrue.Constants.Provider.Google,
                    IdToken = idToken,
                };
                var response = await _supabaseClient.Auth.SignInWithIdToken(credentials).AsUniTask().AttachExternalCancellation(cancellationToken);
                return UpdateSession(response);
            }
            catch (Exception exception)
            {
                throw MapException(exception);
            }
        }

        /// <inheritdoc/>
        public async UniTask<AuthUser> SignInWithAppleAsync(string idToken, string nonce, CancellationToken cancellationToken = default)
        {
            try
            {
                var credentials = new Supabase.Gotrue.Credentials
                {
                    Provider = Supabase.Gotrue.Constants.Provider.Apple,
                    IdToken = idToken,
                    Nonce = nonce,
                };
                var response = await _supabaseClient.Auth.SignInWithIdToken(credentials).AsUniTask().AttachExternalCancellation(cancellationToken);
                return UpdateSession(response);
            }
            catch (Exception exception)
            {
                throw MapException(exception);
            }
        }

        /// <inheritdoc/>
        public async UniTask<AuthUser> RestoreSessionAsync(CancellationToken cancellationToken = default)
        {
            var cached = LocalSave.Load<AuthUser>();

            // LocalSave returns a new() instance when no data exists; treat empty UserId as cache miss.
            if (string.IsNullOrEmpty(cached.UserId))
            {
                return null;
            }

            var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var refreshThresholdSeconds = 300;

            if (cached.ExpiresAt - nowSeconds <= refreshThresholdSeconds)
            {
                try
                {
                    var refreshed = await RefreshSessionAsync(cached.RefreshToken, cancellationToken);
                    return refreshed;
                }
                catch (AuthException authException) when (authException.ErrorCode == AuthErrorCode.SessionExpired)
                {
                    // Cached session is no longer valid; clear it and require re-authentication.
                    ClearSession();
                    return null;
                }
            }

            SetCurrentUser(cached);
            return cached;
        }

        /// <inheritdoc/>
        public async UniTask<AuthUser> LinkIdentityAsync(string provider, string idToken, CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _supabaseClient.Auth.LinkIdentity(provider, idToken).AsUniTask().AttachExternalCancellation(cancellationToken);
                return UpdateSession(response);
            }
            catch (Exception exception)
            {
                throw MapException(exception);
            }
        }

        /// <inheritdoc/>
        public async UniTask SignOutAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _supabaseClient.Auth.SignOut().AsUniTask().AttachExternalCancellation(cancellationToken);
                ClearSession();
            }
            catch (Exception exception)
            {
                throw MapException(exception);
            }
        }

        /// <inheritdoc/>
        public async UniTask DeleteAccountAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _supabaseClient.Auth.Admin.DeleteUser(_currentUser.UserId).AsUniTask().AttachExternalCancellation(cancellationToken);
                ClearSession();
            }
            catch (Exception exception)
            {
                throw MapException(exception);
            }
        }

        /// <inheritdoc/>
        public async UniTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var refreshThresholdSeconds = 60;

            if (_currentUser != null && _currentUser.ExpiresAt - nowSeconds <= refreshThresholdSeconds)
            {
                await RefreshSessionAsync(_currentUser.RefreshToken, cancellationToken);
            }

            return _currentUser?.AccessToken;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _currentUserSubject.Dispose();
        }

        // --- Internal helpers ---

        private async UniTask<AuthUser> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _supabaseClient.Auth.RefreshSession(refreshToken).AsUniTask().AttachExternalCancellation(cancellationToken);
                return UpdateSession(response);
            }
            catch (Exception exception)
            {
                throw MapException(exception);
            }
        }

        private AuthUser UpdateSession(Supabase.Gotrue.Session session)
        {
            if (session?.User == null)
            {
                throw new AuthException(AuthErrorCode.Unknown, "Supabase returned a null session.");
            }

            var user = new AuthUser
            {
                UserId = session.User.Id,
                Email = session.User.Email ?? string.Empty,
                IsAnonymous = session.User.IsAnonymous,
                AccessToken = session.AccessToken,
                RefreshToken = session.RefreshToken,
                ExpiresAt = session.ExpiresAt(),
            };

            LocalSave.Save(user);
            SetCurrentUser(user);
            return user;
        }

        private void SetCurrentUser(AuthUser user)
        {
            _currentUser = user;
            _currentUserSubject.OnNext(user);
        }

        private void ClearSession()
        {
            LocalSave.Delete<AuthUser>();
            _currentUser = null;
            _currentUserSubject.OnNext(null);
        }

        /// <summary>
        /// Maps Supabase/Gotrue exceptions to normalized AuthErrorCode values.
        /// Re-throws AuthException as-is to avoid double-wrapping.
        /// </summary>
        private static AuthException MapException(Exception exception)
        {
            if (exception is AuthException authException)
            {
                return authException;
            }

            var message = exception.Message ?? string.Empty;

            // Gotrue error messages are not strongly typed, so we match on known substrings.
            if (message.Contains("Invalid login credentials") || message.Contains("invalid_credentials"))
            {
                return new AuthException(AuthErrorCode.InvalidCredentials, message);
            }

            if (message.Contains("User already registered") || message.Contains("email_exists"))
            {
                return new AuthException(AuthErrorCode.EmailAlreadyInUse, message);
            }

            if (message.Contains("User not found") || message.Contains("user_not_found"))
            {
                return new AuthException(AuthErrorCode.AccountNotFound, message);
            }

            if (message.Contains("Token has expired") || message.Contains("token_expired") || message.Contains("refresh_token_not_found"))
            {
                return new AuthException(AuthErrorCode.SessionExpired, message);
            }

            if (exception is System.Net.Http.HttpRequestException || exception is System.Net.WebException)
            {
                return new AuthException(AuthErrorCode.NetworkError, message);
            }

            return new AuthException(AuthErrorCode.Unknown, message);
        }
    }
}
#endif
