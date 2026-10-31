using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace KingsRescue.Online
{
    /// <summary>
    /// Bridge to Google Identity Services (Assets/Plugins/WebGL/GoogleAuth.jslib).
    /// Only works in a WebGL browser build; elsewhere RequestIdToken fails with a clear message.
    /// The jslib calls back into OnGoogleIdToken / OnGoogleAuthError by GameObject name.
    /// </summary>
    public sealed class GoogleSignIn : MonoBehaviour
    {
        private TaskCompletionSource<string> pending;
        private bool redirectDelivered;

        public event System.Action<string> IdTokenReceived;

        public static bool IsSupported
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public void Init(string googleWebClientId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GoogleAuth_Init((googleWebClientId ?? string.Empty).Trim(), gameObject.name);
#endif
        }

        /// <summary>Shows the Google button overlay and resolves with a Google ID token.</summary>
        public Task<string> RequestIdToken()
        {
            if (pending != null && !pending.Task.IsCompleted)
            {
                return pending.Task;
            }

            pending = new TaskCompletionSource<string>();

#if UNITY_WEBGL && !UNITY_EDITOR
            GoogleAuth_SignIn();
#else
            Fail("Google sign-in only works in the WebGL build. Run it in the browser, or play as a guest here.");
#endif
            return pending.Task;
        }

        public void SignOut()
        {
            pending = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            GoogleAuth_SignOut();
#endif
        }

        /// <summary>Token saved by a redirect that reloaded the page before this object existed.</summary>
        public string ConsumePendingToken()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return GoogleAuth_ConsumePending() ?? string.Empty;
#else
            return string.Empty;
#endif
        }

        // Called from GoogleAuth.jslib via SendMessage.
        public void OnGoogleIdToken(string idToken)
        {
            ClearStoredToken();

            if (string.IsNullOrEmpty(idToken))
            {
                Fail("Google did not return an ID token.");
                return;
            }

            if (pending != null && !pending.Task.IsCompleted)
            {
                pending.TrySetResult(idToken);
                return;
            }

            // The page was reloaded by the mobile / full-screen redirect. Nobody is awaiting a click.
            if (redirectDelivered)
            {
                return;
            }

            redirectDelivered = true;
            IdTokenReceived?.Invoke(idToken);
        }

        // Called from GoogleAuth.jslib via SendMessage.
        public void OnGoogleAuthError(string message)
        {
            Fail(string.IsNullOrEmpty(message) ? "Google sign-in failed." : message);
        }

        private void Fail(string message)
        {
            pending?.TrySetException(new InvalidOperationException(message));
        }

        private void ClearStoredToken()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            GoogleAuth_ClearPending();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void GoogleAuth_Init(string clientId, string gameObjectName);

        [DllImport("__Internal")]
        private static extern void GoogleAuth_SignIn();

        [DllImport("__Internal")]
        private static extern void GoogleAuth_SignOut();

        [DllImport("__Internal")]
        private static extern string GoogleAuth_ConsumePending();

        [DllImport("__Internal")]
        private static extern void GoogleAuth_ClearPending();
#endif
    }
}
