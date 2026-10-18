using System;

namespace KingsRescue.Online
{
    // JsonUtility DTOs that mirror the GameLeaderboard backend. Nested objects are never null
    // after parsing, so check IsValid / IsEmpty instead of comparing with null.

    [Serializable]
    public class ErrorResponse
    {
        public string error;
        public string code;
        public int saveVersion;
    }

    [Serializable]
    public class GoogleProfile
    {
        public string googleSub;
        public string email;
        public string name;
        public string picture;
    }

    [Serializable]
    public class PlayerPayload
    {
        public string game;
        public string username;
        public int score;
        public int rank;
        public int coin;
        public bool googleLinked;

        public bool IsValid => !string.IsNullOrEmpty(username);
    }

    [Serializable]
    public class GuestAuthRequest
    {
        public string game;
        public string guestId;
    }

    [Serializable]
    public class GoogleAuthRequest
    {
        public string idToken;
        public string game;
    }

    /// <summary>Response of /auth/guest, /auth/google and /auth/link-google.</summary>
    [Serializable]
    public class AuthResponse
    {
        public string token;
        public GoogleProfile profile;
        public bool needsUsername;
        public PlayerPayload player;
    }

    [Serializable]
    public class MeResponse
    {
        public bool needsUsername;
        public PlayerPayload player;
    }

    [Serializable]
    public class ClaimUsernameRequest
    {
        public string game;
        public string username;
    }

    [Serializable]
    public class SubmitScoreRequest
    {
        public string game;
        public int score;
        public int coin;
    }

    [Serializable]
    public class SubmitScoreResponse
    {
        public string username;
        public int score;
        public int rank;
        public bool updated;
    }

    [Serializable]
    public class LeaderboardEntryDto
    {
        public int rank;
        public string username;
        public int score;
    }

    [Serializable]
    public class LeaderboardResponse
    {
        public string game;
        public LeaderboardEntryDto[] entries;
        public PlayerPayload currentPlayer;
    }

    [Serializable]
    public class SaveResponse
    {
        public int saveVersion;
        public string saveData;

        public bool IsEmpty => string.IsNullOrEmpty(saveData);
    }

    [Serializable]
    public class SaveRequest
    {
        public string game;
        public string saveData;
        public int baseVersion;
    }

    [Serializable]
    public class SaveVersionResponse
    {
        public int saveVersion;
    }

    public class ApiResult<T>
    {
        public bool Ok;
        public int Status;
        public T Data;
        public string Error;
        public string Code;
        /// <summary>Latest server save version, set on a save_conflict response.</summary>
        public int ServerSaveVersion;

        /// <summary>True when the request never reached the server (offline, DNS, CORS, timeout).</summary>
        public bool NetworkError => !Ok && Status == 0;

        public bool Unauthorized => !Ok && Status == 401;

        public static ApiResult<T> Fail(int status, string error, string code = null, int serverSaveVersion = 0)
        {
            return new ApiResult<T>
            {
                Ok = false,
                Status = status,
                Error = error,
                Code = code,
                ServerSaveVersion = serverSaveVersion,
            };
        }
    }

    public readonly struct OnlineResult
    {
        public readonly bool Ok;
        public readonly string Error;
        public readonly string Code;

        private OnlineResult(bool ok, string error, string code)
        {
            Ok = ok;
            Error = error;
            Code = code;
        }

        public static OnlineResult Success => new OnlineResult(true, null, null);

        public static OnlineResult Fail(string error, string code = null)
        {
            return new OnlineResult(false, error, code);
        }
    }
}
