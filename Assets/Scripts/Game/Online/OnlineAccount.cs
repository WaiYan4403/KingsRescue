using System;
using System.Threading.Tasks;
using UnityEngine;

namespace KingsRescue.Online
{
    public enum AccountKind
    {
        None = 0,
        Guest = 1,
        Google = 2,
    }

    /// <summary>Which menu screen a launching player should land on.</summary>
    public enum AccountStep
    {
        ChooseAccount,
        ChooseUsername,
        Ready,
    }

    /// <summary>
    /// Who the player is: guest or Google, their username, and the backend session.
    /// Identity lives in PlayerPrefs so the player is recognised on the next launch; progress
    /// itself is handled by CloudSave.
    /// </summary>
    public sealed class OnlineAccount
    {
        private const string KindKey = "KingsRescue.Account.Kind";
        private const string JwtKey = "KingsRescue.Account.Jwt";
        private const string GuestIdKey = "KingsRescue.Account.GuestId";
        private const string UsernameKey = "KingsRescue.Account.Username";
        private const string GoogleLinkedKey = "KingsRescue.Account.GoogleLinked";

        public const int MinUsernameLength = 3;
        public const int MaxUsernameLength = 24;

        public const string CodeGoogleInUse = "google_account_in_use";
        public const string CodeSessionExpired = "session_expired";

        private readonly OnlineConfig config;
        private readonly ApiClient api;
        private readonly GoogleSignIn google;

        private string pendingGoogleIdToken;

        public OnlineAccount(OnlineConfig config, ApiClient api, GoogleSignIn google)
        {
            this.config = config;
            this.api = api;
            this.google = google;
            Load();
        }

        /// <summary>Set by OnlineServices. Pulled after every successful sign-in.</summary>
        public CloudSave Cloud { get; set; }

        public AccountKind Kind { get; private set; }
        public string Username { get; private set; }
        public bool GoogleLinked { get; private set; }

        /// <summary>True when a Google-only session ran out and the player must sign in again to sync.</summary>
        public bool SessionExpired { get; private set; }

        public string Jwt { get; private set; }

        public bool HasUsername => !string.IsNullOrEmpty(Username);

        public bool CanSync => Kind != AccountKind.None && HasUsername && !string.IsNullOrEmpty(Jwt) && !SessionExpired;

        public bool GoogleSupported => GoogleSignIn.IsSupported && config.HasGoogleClientId;

        private string GuestId => PlayerPrefs.GetString(GuestIdKey, string.Empty);

        public event Action Changed;

        public AccountStep GetStartStep()
        {
            if (Kind == AccountKind.None)
            {
                return AccountStep.ChooseAccount;
            }

            return HasUsername ? AccountStep.Ready : AccountStep.ChooseUsername;
        }

        // ---------------------------------------------------------------- sign-in

        public async Task<OnlineResult> ContinueAsGuest()
        {
            string guestId = EnsureGuestId();
            ApiResult<AuthResponse> result = await api.AuthGuest(guestId);
            if (!result.Ok)
            {
                return Describe(result);
            }

            Kind = AccountKind.Guest;
            SessionExpired = false;
            ApplyAuth(result.Data);
            return await FinishSignIn();
        }

        public async Task<OnlineResult> SignInWithGoogle()
        {
            (bool tokenOk, string idToken, OnlineResult failure) = await GetGoogleToken();
            if (!tokenOk)
            {
                return failure;
            }

            ApiResult<AuthResponse> result = await api.AuthGoogle(idToken);
            if (!result.Ok)
            {
                return Describe(result);
            }

            Kind = AccountKind.Google;
            SessionExpired = false;
            ApplyAuth(result.Data);
            return await FinishSignIn();
        }

        /// <summary>Attaches Google to the current (guest) player so the account can be used anywhere.</summary>
        public async Task<OnlineResult> ConnectGoogle()
        {
            if (Kind == AccountKind.None)
            {
                return OnlineResult.Fail("Choose an account first.");
            }

            (bool tokenOk, string idToken, OnlineResult failure) = await GetGoogleToken();
            if (!tokenOk)
            {
                return failure;
            }

            ApiResult<AuthResponse> result = await Authed(jwt => api.LinkGoogle(jwt, idToken));
            if (!result.Ok)
            {
                if (result.Code == CodeGoogleInUse)
                {
                    // Keep the token so the player can switch to that account without signing in again.
                    pendingGoogleIdToken = idToken;
                }

                return Describe(result);
            }

            Kind = AccountKind.Google;
            SessionExpired = false;
            ApplyAuth(result.Data);
            return await FinishSignIn();
        }

        /// <summary>
        /// After ConnectGoogle reported google_account_in_use: sign in to that existing profile.
        /// Progress on this device is replaced by that profile's cloud save.
        /// </summary>
        public async Task<OnlineResult> SwitchToGoogleAccount()
        {
            if (string.IsNullOrEmpty(pendingGoogleIdToken))
            {
                return OnlineResult.Fail("Connect Google again to continue.");
            }

            string idToken = pendingGoogleIdToken;
            pendingGoogleIdToken = null;

            ApiResult<AuthResponse> result = await api.AuthGoogle(idToken);
            if (!result.Ok)
            {
                return Describe(result);
            }

            Kind = AccountKind.Google;
            SessionExpired = false;
            ApplyAuth(result.Data);
            return await FinishSignIn();
        }

        public void ForgetPendingGoogleSwitch()
        {
            pendingGoogleIdToken = null;
        }

        public async Task<OnlineResult> ClaimUsername(string requested)
        {
            if (!ValidateUsername(requested, out string cleaned, out string problem))
            {
                return OnlineResult.Fail(problem);
            }

            if (Kind == AccountKind.None)
            {
                return OnlineResult.Fail("Choose an account first.");
            }

            ApiResult<PlayerPayload> result = await Authed(jwt => api.ClaimUsername(jwt, cleaned));
            if (!result.Ok)
            {
                if (result.Status == 409)
                {
                    return OnlineResult.Fail("That name is already taken. Try another one.", "username_taken");
                }

                return Describe(result);
            }

            Username = result.Data.username;
            GoogleLinked = result.Data.googleLinked;
            Persist();
            return await FinishSignIn();
        }

        /// <summary>Quietly renews the session for a returning player. Never throws.</summary>
        public async Task<OnlineResult> RestoreSession()
        {
            if (Kind == AccountKind.None)
            {
                return OnlineResult.Success;
            }

            if (!string.IsNullOrEmpty(GuestId))
            {
                return await RefreshGuestSession();
            }

            // Google-only account: validate the stored token; it cannot be renewed silently.
            if (string.IsNullOrEmpty(Jwt))
            {
                SessionExpired = true;
                Changed?.Invoke();
                return OnlineResult.Fail("Sign in with Google to sync your progress.", CodeSessionExpired);
            }

            ApiResult<MeResponse> me = await api.GetMe(Jwt);
            if (me.Ok)
            {
                SessionExpired = false;
                if (me.Data.player != null && me.Data.player.IsValid)
                {
                    Username = me.Data.player.username;
                    GoogleLinked = me.Data.player.googleLinked;
                    Persist();
                }

                return OnlineResult.Success;
            }

            if (me.Unauthorized)
            {
                SessionExpired = true;
                Changed?.Invoke();
                return OnlineResult.Fail("Your Google session expired. Sign in again to sync.", CodeSessionExpired);
            }

            return Describe(me);
        }

        /// <summary>
        /// Syncs, then forgets this account on the device so the next player starts clean.
        /// Only offered for Google accounts, whose progress can always be restored by signing in again.
        /// </summary>
        public async Task<OnlineResult> SignOut()
        {
            if (Cloud != null)
            {
                bool flushed = await Cloud.FlushAsync();
                if (!flushed)
                {
                    return OnlineResult.Fail("Could not sync your progress. Check your connection and try again.");
                }
            }

            Cloud?.ClearLocalData();
            google.SignOut();

            PlayerPrefs.DeleteKey(KindKey);
            PlayerPrefs.DeleteKey(JwtKey);
            PlayerPrefs.DeleteKey(GuestIdKey);
            PlayerPrefs.DeleteKey(UsernameKey);
            PlayerPrefs.DeleteKey(GoogleLinkedKey);
            PlayerPrefs.Save();

            Kind = AccountKind.None;
            Username = string.Empty;
            GoogleLinked = false;
            Jwt = string.Empty;
            SessionExpired = false;
            pendingGoogleIdToken = null;
            Changed?.Invoke();
            return OnlineResult.Success;
        }

        // ---------------------------------------------------------------- authenticated calls

        /// <summary>
        /// Runs an API call with the current token. On a 401 the session is renewed once (guest
        /// credentials can do that silently) and the call is repeated.
        /// </summary>
        public async Task<ApiResult<T>> Authed<T>(Func<string, Task<ApiResult<T>>> call)
        {
            ApiResult<T> result = await call(Jwt);
            if (!result.Unauthorized)
            {
                return result;
            }

            OnlineResult refreshed = await RefreshGuestSession();
            if (!refreshed.Ok)
            {
                if (refreshed.Code == CodeSessionExpired)
                {
                    return ApiResult<T>.Fail(401, refreshed.Error, CodeSessionExpired);
                }

                return ApiResult<T>.Fail(0, refreshed.Error);
            }

            return await call(Jwt);
        }

        // ---------------------------------------------------------------- helpers

        public static bool ValidateUsername(string raw, out string cleaned, out string problem)
        {
            cleaned = (raw ?? string.Empty).Trim();
            problem = null;

            if (cleaned.Length < MinUsernameLength)
            {
                problem = $"Name must be at least {MinUsernameLength} characters.";
                return false;
            }

            if (cleaned.Length > MaxUsernameLength)
            {
                problem = $"Name can be at most {MaxUsernameLength} characters.";
                return false;
            }

            for (int i = 0; i < cleaned.Length; i++)
            {
                char c = cleaned[i];
                bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                               || c == '_' || c == '-' || c == '.' || c == ' ';
                if (!allowed)
                {
                    problem = "Use letters, numbers, spaces, '_', '-' or '.' only.";
                    return false;
                }
            }

            return true;
        }

        private async Task<OnlineResult> RefreshGuestSession()
        {
            string guestId = GuestId;
            if (string.IsNullOrEmpty(guestId))
            {
                SessionExpired = true;
                Changed?.Invoke();
                return OnlineResult.Fail("Your session expired. Sign in with Google again.", CodeSessionExpired);
            }

            ApiResult<AuthResponse> result = await api.AuthGuest(guestId);
            if (!result.Ok)
            {
                return Describe(result);
            }

            SessionExpired = false;
            Jwt = result.Data.token;

            if (result.Data.needsUsername)
            {
                // The server has no player for this guest (for example a wiped database).
                Username = string.Empty;
            }
            else if (result.Data.player != null && result.Data.player.IsValid)
            {
                Username = result.Data.player.username;
                GoogleLinked = result.Data.player.googleLinked;
            }

            Persist();
            return OnlineResult.Success;
        }

        private async Task<(bool ok, string idToken, OnlineResult failure)> GetGoogleToken()
        {
            if (!GoogleSignIn.IsSupported)
            {
                return (false, null, OnlineResult.Fail("Google sign-in is only available in the web version of the game."));
            }

            if (!config.HasGoogleClientId)
            {
                return (false, null, OnlineResult.Fail("Google sign-in is not configured yet (missing Google client ID)."));
            }

            try
            {
                string idToken = await google.RequestIdToken();
                return (true, idToken, OnlineResult.Success);
            }
            catch (Exception ex)
            {
                return (false, null, OnlineResult.Fail(ex.Message));
            }
        }

        private void ApplyAuth(AuthResponse auth)
        {
            Jwt = auth.token;

            if (!auth.needsUsername && auth.player != null && auth.player.IsValid)
            {
                Username = auth.player.username;
                GoogleLinked = auth.player.googleLinked;
            }
            else
            {
                Username = string.Empty;
                GoogleLinked = Kind == AccountKind.Google;
            }

            Persist();
        }

        private async Task<OnlineResult> FinishSignIn()
        {
            if (HasUsername && Cloud != null)
            {
                Cloud.ResetSync();
                await Cloud.SyncAfterLoginAsync();
            }

            Changed?.Invoke();
            return OnlineResult.Success;
        }

        private string EnsureGuestId()
        {
            string id = GuestId;
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }

            // Two GUIDs = 64 hex characters of randomness; this value is the guest's password.
            id = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(GuestIdKey, id);
            PlayerPrefs.Save();
            return id;
        }

        private OnlineResult Describe<T>(ApiResult<T> result)
        {
            if (result.NetworkError)
            {
                return OnlineResult.Fail("Can't reach the server. Check your connection and try again.", "network");
            }

            return OnlineResult.Fail(result.Error ?? "Something went wrong.", result.Code);
        }

        private void Load()
        {
            Kind = (AccountKind)PlayerPrefs.GetInt(KindKey, 0);
            Jwt = PlayerPrefs.GetString(JwtKey, string.Empty);
            Username = PlayerPrefs.GetString(UsernameKey, string.Empty);
            GoogleLinked = PlayerPrefs.GetInt(GoogleLinkedKey, 0) == 1;
        }

        private void Persist()
        {
            PlayerPrefs.SetInt(KindKey, (int)Kind);
            PlayerPrefs.SetString(JwtKey, Jwt ?? string.Empty);
            PlayerPrefs.SetString(UsernameKey, Username ?? string.Empty);
            PlayerPrefs.SetInt(GoogleLinkedKey, GoogleLinked ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
