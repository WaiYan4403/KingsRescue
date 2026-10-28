using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace KingsRescue.Online
{
    /// <summary>Thin async wrapper over the GameLeaderboard REST API.</summary>
    public sealed class ApiClient
    {
        private readonly OnlineConfig config;

        public ApiClient(OnlineConfig config)
        {
            this.config = config;
        }

        public Task<ApiResult<AuthResponse>> AuthGuest(string guestId)
        {
            string body = JsonUtility.ToJson(new GuestAuthRequest { game = config.NormalizedGameSlug, guestId = guestId });
            return Send<AuthResponse>("POST", "/api/auth/guest", body, null);
        }

        public Task<ApiResult<AuthResponse>> AuthGoogle(string idToken)
        {
            string body = JsonUtility.ToJson(new GoogleAuthRequest { game = config.NormalizedGameSlug, idToken = idToken });
            return Send<AuthResponse>("POST", "/api/auth/google", body, null);
        }

        public Task<ApiResult<AuthResponse>> LinkGoogle(string jwt, string idToken)
        {
            string body = JsonUtility.ToJson(new GoogleAuthRequest { game = config.NormalizedGameSlug, idToken = idToken });
            return Send<AuthResponse>("POST", "/api/auth/link-google", body, jwt);
        }

        public Task<ApiResult<MeResponse>> GetMe(string jwt)
        {
            return Send<MeResponse>("GET", "/api/me?game=" + Escape(config.NormalizedGameSlug), null, jwt);
        }

        public Task<ApiResult<PlayerPayload>> ClaimUsername(string jwt, string username)
        {
            string body = JsonUtility.ToJson(new ClaimUsernameRequest { game = config.NormalizedGameSlug, username = username });
            return Send<PlayerPayload>("POST", "/api/players", body, jwt);
        }

        public Task<ApiResult<SubmitScoreResponse>> SubmitScore(string jwt, int score, int coin, string character)
        {
            string body = JsonUtility.ToJson(new SubmitScoreRequest
            {
                game = config.NormalizedGameSlug,
                score = score,
                coin = coin,
                character = character ?? string.Empty,
            });
            return Send<SubmitScoreResponse>("POST", "/api/scores", body, jwt);
        }

        public Task<ApiResult<LeaderboardResponse>> GetLeaderboard(string jwt, int limit)
        {
            int clamped = Mathf.Clamp(limit, 1, 100);
            return Send<LeaderboardResponse>(
                "GET",
                "/api/leaderboard?game=" + Escape(config.NormalizedGameSlug) + "&limit=" + clamped,
                null,
                jwt);
        }

        public Task<ApiResult<SaveResponse>> GetSave(string jwt)
        {
            return Send<SaveResponse>("GET", "/api/save?game=" + Escape(config.NormalizedGameSlug), null, jwt);
        }

        public Task<ApiResult<SaveVersionResponse>> PutSave(string jwt, string saveData, int baseVersion)
        {
            string body = JsonUtility.ToJson(new SaveRequest
            {
                game = config.NormalizedGameSlug,
                saveData = saveData,
                baseVersion = baseVersion,
            });
            return Send<SaveVersionResponse>("PUT", "/api/save", body, jwt);
        }

        private async Task<ApiResult<T>> Send<T>(string method, string path, string jsonBody, string jwt)
        {
            string url = config.NormalizedBaseUrl + path;

            try
            {
                using var request = new UnityWebRequest(url, method);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Mathf.Max(3, config.requestTimeoutSeconds);

                if (!string.IsNullOrEmpty(jsonBody))
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                request.SetRequestHeader("x-api-key", config.apiKey ?? string.Empty);
                if (!string.IsNullOrEmpty(jwt))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + jwt);
                }

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    await Task.Yield();
                }

                int status = (int)request.responseCode;
                string text = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    ErrorResponse parsed = ParseError(text);
                    return ApiResult<T>.Fail(status, DescribeFailure(status, request.error, parsed), parsed?.code, parsed?.saveVersion ?? 0);
                }

                if (string.IsNullOrEmpty(text))
                {
                    return ApiResult<T>.Fail(status, "Empty response");
                }

                return new ApiResult<T>
                {
                    Ok = true,
                    Status = status,
                    Data = JsonUtility.FromJson<T>(text),
                };
            }
            catch (Exception ex)
            {
                return ApiResult<T>.Fail(0, ex.Message);
            }
        }

        // WebGL hides the body of a failed request and reports it as "Unknown Error".
        private static string DescribeFailure(int status, string transportError, ErrorResponse parsed)
        {
            if (parsed != null && !string.IsNullOrEmpty(parsed.error))
            {
                return parsed.error;
            }

            if (status == 401)
            {
                return "The server rejected the API key. Check that OnlineConfig matches the backend.";
            }

            if (status == 0 || string.Equals(transportError, "Unknown Error", StringComparison.OrdinalIgnoreCase))
            {
                return "Can't reach the server. Check that it is running and that the address in OnlineConfig is right.";
            }

            return string.IsNullOrEmpty(transportError) ? "HTTP " + status : transportError;
        }

        private static ErrorResponse ParseError(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains("\"error\""))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<ErrorResponse>(text);
            }
            catch
            {
                return null;
            }
        }

        private static string Escape(string value)
        {
            return UnityWebRequest.EscapeURL(value ?? string.Empty);
        }
    }
}
