using System.Threading.Tasks;
using UnityEngine;

namespace KingsRescue.Online
{
    /// <summary>
    /// Single entry point for accounts and cloud saves. Created once before the first scene loads and kept
    /// across scene changes, so the menu and the run both reach the same session.
    /// When Resources/OnlineConfig is missing or disabled, <see cref="Instance"/> stays null and the game
    /// plays offline exactly as before.
    /// </summary>
    public sealed class OnlineServices : MonoBehaviour
    {
        // SendMessage from GoogleAuth.jslib finds this object by name.
        private const string ObjectName = "OnlineServices";

        public static OnlineServices Instance { get; private set; }

        public OnlineConfig Config { get; private set; }
        public OnlineAccount Account { get; private set; }
        public CloudSave Cloud { get; private set; }
        public GoogleSignIn Google { get; private set; }

        private ApiClient api;
        private bool restoring;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            OnlineConfig config = Resources.Load<OnlineConfig>(OnlineConfig.ResourceName);
            if (config == null || !config.enabled)
            {
                return;
            }

            var host = new GameObject(ObjectName);
            DontDestroyOnLoad(host);
            host.AddComponent<GoogleSignIn>();
            host.AddComponent<OnlineServices>();
        }

        /// <summary>Top scores for one game. Sends the session when there is one, so the caller's own rank comes back.</summary>
        public Task<ApiResult<LeaderboardResponse>> GetLeaderboard(int limit)
        {
            if (Account.Kind == AccountKind.None || string.IsNullOrEmpty(Account.Jwt))
            {
                return api.GetLeaderboard(null, limit);
            }

            return Account.Authed(jwt => api.GetLeaderboard(jwt, limit));
        }

        /// <summary>Called by GameManager when a run ends. Safe to call when online features are off.</summary>
        public static void NotifyRunFinished(float distanceMeters)
        {
            if (Instance == null)
            {
                return;
            }

            _ = Instance.Cloud.OnRunFinishedAsync(Mathf.FloorToInt(Mathf.Max(0f, distanceMeters)));
        }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Config = Resources.Load<OnlineConfig>(OnlineConfig.ResourceName);
            Google = GetComponent<GoogleSignIn>();

            api = new ApiClient(Config);
            Account = new OnlineAccount(Config, api, Google);
            Cloud = new CloudSave(Config, api, Account);
            Account.Cloud = Cloud;
        }

        private async void Start()
        {
            if (Instance != this)
            {
                return;
            }

            Google.Init(Config.googleWebClientId);

            if (Account.Kind == AccountKind.None)
            {
                return;
            }

            // Returning player: renew the session in the background; the menu does not wait for it.
            restoring = true;
            try
            {
                await Account.RestoreSession();
            }
            finally
            {
                restoring = false;
            }
        }

        private void Update()
        {
            if (!restoring)
            {
                Cloud.Tick();
            }
        }

        // The browser tab being hidden is the last reliable moment to upload in WebGL.
        private void OnApplicationPause(bool paused)
        {
            if (paused && Cloud != null)
            {
                Cloud.PushSoon();
            }
        }

        private void OnApplicationQuit()
        {
            Cloud?.PushSoon();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Cloud.Dispose();
                Instance = null;
            }
        }
    }
}
