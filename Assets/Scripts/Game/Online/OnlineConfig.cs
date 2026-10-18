using UnityEngine;

namespace KingsRescue.Online
{
    /// <summary>
    /// Backend and Google settings. Loaded from Resources/OnlineConfig. The values are baked into the
    /// build, so the API key only identifies the game; it is not a secret.
    /// </summary>
    [CreateAssetMenu(fileName = "OnlineConfig", menuName = "King's Rescue/Online Config")]
    public class OnlineConfig : ScriptableObject
    {
        public const string ResourceName = "OnlineConfig";

        [Header("Switch")]
        [Tooltip("Off = no account screen and no cloud saves. The game behaves like the old offline build.")]
        public bool enabled = true;

        [Header("GameLeaderboard backend")]
        [Tooltip("No trailing slash. Must be HTTPS for a public WebGL build.")]
        public string apiBaseUrl = "http://localhost:3000";
        [Tooltip("Must match API_KEY in GameLeaderboard/.env")]
        public string apiKey = "change-me";
        [Tooltip("Everything is stored per game slug on the backend (lowercase, max 32).")]
        public string gameSlug = "kingsrescue";
        [Min(3)] public int requestTimeoutSeconds = 15;

        [Header("Google Cloud Web client ID")]
        [Tooltip("OAuth client type Web. Also set GOOGLE_CLIENT_ID in GameLeaderboard/.env. Add the WebGL page origin under Authorized JavaScript origins.")]
        public string googleWebClientId = "YOUR_GOOGLE_WEB_CLIENT_ID.apps.googleusercontent.com";

        [Header("Cloud save")]
        [Tooltip("Seconds after the last local change before progress is uploaded.")]
        [Min(1f)] public float saveDebounceSeconds = 4f;
        [Tooltip("Seconds to wait before retrying a failed upload.")]
        [Min(5f)] public float retrySeconds = 20f;

        public string NormalizedBaseUrl => (apiBaseUrl ?? string.Empty).TrimEnd('/');

        public string NormalizedGameSlug => (gameSlug ?? string.Empty).Trim().ToLowerInvariant();

        public bool HasGoogleClientId
        {
            get
            {
                string id = googleWebClientId ?? string.Empty;
                return id.EndsWith(".apps.googleusercontent.com")
                    && id.IndexOf("YOUR_GOOGLE_WEB_CLIENT_ID", System.StringComparison.OrdinalIgnoreCase) < 0;
            }
        }
    }
}
