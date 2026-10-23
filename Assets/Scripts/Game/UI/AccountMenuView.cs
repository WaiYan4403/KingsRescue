using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KingsRescue.Meta;
using KingsRescue.Online;

namespace KingsRescue.UI
{
    /// <summary>
    /// The account screen (guest / Google, connect Google later, sign out) and the username screen.
    /// Shown automatically on first launch; reachable afterwards from the Account button on the home panel.
    /// </summary>
    public class AccountMenuView : MonoBehaviour
    {
        private static readonly Color ErrorColor = new Color(1f, 0.45f, 0.4f, 1f);

        [SerializeField] private MainMenuController menu;
        [Tooltip("Home panel button that opens the account screen. Hidden when online features are off.")]
        [SerializeField] private GameObject homeAccountButton;

        [Header("Account screen")]
        [SerializeField] private TMP_Text accountTitle;
        [SerializeField] private TMP_Text accountInfo;
        [SerializeField] private TMP_Text accountStatus;
        [SerializeField] private Button guestButton;
        [SerializeField] private Button googleButton;
        [SerializeField] private Button connectButton;
        [SerializeField] private TMP_Text connectLabel;
        [SerializeField] private Button signOutButton;
        [SerializeField] private Button backButton;

        [Header("Username screen")]
        [SerializeField] private TMP_InputField usernameInput;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text usernameStatus;

        private bool busy;
        private bool switchPending;

        private OnlineAccount Account => OnlineServices.Instance != null ? OnlineServices.Instance.Account : null;

        private void Awake()
        {
            if (homeAccountButton != null)
            {
                homeAccountButton.SetActive(OnlineServices.Instance != null);
            }

            Bind(guestButton, OnGuestClicked);
            Bind(googleButton, OnGoogleClicked);
            Bind(connectButton, OnConnectClicked);
            Bind(signOutButton, OnSignOutClicked);
            Bind(backButton, OnBackClicked);
            Bind(confirmButton, OnConfirmClicked);

            if (usernameInput != null)
            {
                usernameInput.characterLimit = OnlineAccount.MaxUsernameLength;
                usernameInput.onSubmit.AddListener(_ => OnConfirmClicked());
            }
        }

        /// <summary>
        /// Called by the menu on launch. Returns true when a setup screen was opened, so the menu should
        /// not show the home panel yet.
        /// </summary>
        public bool TryBeginSetup()
        {
            OnlineAccount account = Account;
            if (account == null)
            {
                return false;
            }

            switch (account.GetStartStep())
            {
                case AccountStep.ChooseAccount:
                    menu.ShowAccount();
                    return true;
                case AccountStep.ChooseUsername:
                    menu.ShowUsername();
                    return true;
                default:
                    return false;
            }
        }

        public void RefreshAccount()
        {
            OnlineAccount account = Account;
            switchPending = false;
            if (account == null)
            {
                return;
            }

            ApplyAccountState(account);
            SetAccountStatus(StatusLine(), false);
        }

        public void RefreshUsername()
        {
            if (usernameInput != null)
            {
                usernameInput.SetTextWithoutNotify(string.Empty);
            }

            SetUsernameStatus($"{OnlineAccount.MinUsernameLength}-{OnlineAccount.MaxUsernameLength} characters. Letters, numbers, spaces, _ - .", false);
        }

        private void ApplyAccountState(OnlineAccount account)
        {
            bool first = account.Kind == AccountKind.None;
            bool guest = account.Kind == AccountKind.Guest;
            bool google = account.Kind == AccountKind.Google;

            SetActive(guestButton, first);
            SetActive(googleButton, first);
            SetActive(connectButton, guest || (google && account.SessionExpired) || switchPending);
            SetActive(signOutButton, google && !switchPending);
            SetActive(backButton, !first);

            if (accountTitle != null)
            {
                accountTitle.text = first ? "WELCOME" : "ACCOUNT";
            }

            if (connectLabel != null)
            {
                connectLabel.text = switchPending ? "SWITCH TO THAT ACCOUNT"
                    : google ? "SIGN IN WITH GOOGLE AGAIN"
                    : "CONNECT GOOGLE";
            }

            if (accountInfo == null)
            {
                return;
            }

            if (first)
            {
                accountInfo.text = "Play offline as a guest, or sign in with Google to keep your progress on every device.\n\nA guest is saved on this device only. Connecting Google later creates the online account with the name you chose.";
            }
            else if (guest)
            {
                accountInfo.text = $"Playing as <color=#F2C94C>{account.Username}</color>\nSaved on this device. Connect Google to create your online account.";
            }
            else
            {
                accountInfo.text = $"Playing as <color=#F2C94C>{account.Username}</color>\nGoogle account connected.";
            }
        }

        private string StatusLine()
        {
            OnlineServices services = OnlineServices.Instance;
            return services != null && services.Cloud != null ? services.Cloud.StatusText : string.Empty;
        }

        // ---------------------------------------------------------------- actions

        private void OnGuestClicked()
        {
            if (busy || Account == null)
            {
                return;
            }

            OnlineResult result = Account.ContinueAsGuest();
            if (!result.Ok)
            {
                SetAccountStatus(result.Error, true);
                return;
            }

            Continue();
        }

        private async void OnGoogleClicked()
        {
            if (busy || Account == null)
            {
                return;
            }

            SetBusy(true);
            SetAccountStatus("Waiting for Google...", false);
            OnlineResult result = await Account.SignInWithGoogle();
            if (this == null)
            {
                return;
            }

            SetBusy(false);
            if (!result.Ok)
            {
                SetAccountStatus(result.Error, true);
                return;
            }

            Continue();
        }

        private async void OnConnectClicked()
        {
            if (busy || Account == null)
            {
                return;
            }

            SetBusy(true);
            OnlineResult result;
            if (switchPending)
            {
                SetAccountStatus("Switching account...", false);
                result = await Account.SwitchToGoogleAccount();
            }
            else
            {
                SetAccountStatus("Waiting for Google...", false);
                result = Account.Kind == AccountKind.Guest
                    ? await Account.ConnectGoogle()
                    : await Account.SignInWithGoogle();
            }

            if (this == null)
            {
                return;
            }

            SetBusy(false);

            if (result.Ok)
            {
                switchPending = false;
                ApplyAccountState(Account);
                SetAccountStatus("Google connected.", false);
                return;
            }

            if (result.Code == OnlineAccount.CodeGoogleInUse)
            {
                switchPending = true;
                ApplyAccountState(Account);
                SetAccountStatus("That Google account already has a King's Rescue profile. Switching to it replaces the progress on this device.", true);
                return;
            }

            if (result.Code == "username_taken")
            {
                menu.ShowUsername();
                SetUsernameStatus(result.Error, true);
                return;
            }

            SetAccountStatus(result.Error, true);
        }

        private async void OnSignOutClicked()
        {
            if (busy || Account == null)
            {
                return;
            }

            SetBusy(true);
            SetAccountStatus("Saving your progress...", false);
            OnlineResult result = await Account.SignOut();
            if (this == null)
            {
                return;
            }

            SetBusy(false);
            if (!result.Ok)
            {
                SetAccountStatus(result.Error, true);
                return;
            }

            // Reload so every screen drops the previous player's data.
            SceneManager.LoadScene(RunSession.MenuScene);
        }

        private void OnBackClicked()
        {
            if (busy)
            {
                return;
            }

            if (switchPending)
            {
                Account?.ForgetPendingGoogleSwitch();
                switchPending = false;
            }

            menu.ShowHome();
        }

        private async void OnConfirmClicked()
        {
            if (busy || Account == null || usernameInput == null)
            {
                return;
            }

            if (!OnlineAccount.ValidateUsername(usernameInput.text, out _, out string problem))
            {
                SetUsernameStatus(problem, true);
                return;
            }

            SetBusy(true);
            SetUsernameStatus("Saving your name...", false);
            OnlineResult result = await Account.ClaimUsername(usernameInput.text);
            if (this == null)
            {
                return;
            }

            SetBusy(false);
            if (!result.Ok)
            {
                SetUsernameStatus(result.Error, true);
                return;
            }

            if (usernameInput != null)
            {
                usernameInput.DeactivateInputField();
            }

            WebPageReload.ResetZoom();
            menu.ShowHome();
        }

        private void Continue()
        {
            if (Account.HasUsername)
            {
                menu.ShowHome();
            }
            else
            {
                menu.ShowUsername();
            }
        }

        // ---------------------------------------------------------------- helpers

        private void SetBusy(bool value)
        {
            busy = value;
            SetInteractable(guestButton, !value);
            SetInteractable(googleButton, !value);
            SetInteractable(connectButton, !value);
            SetInteractable(signOutButton, !value);
            SetInteractable(backButton, !value);
            SetInteractable(confirmButton, !value);
            if (usernameInput != null)
            {
                usernameInput.interactable = !value;
            }
        }

        private void SetAccountStatus(string message, bool error)
        {
            SetStatus(accountStatus, message, error);
        }

        private void SetUsernameStatus(string message, bool error)
        {
            SetStatus(usernameStatus, message, error);
        }

        private static void SetStatus(TMP_Text label, string message, bool error)
        {
            if (label == null)
            {
                return;
            }

            label.text = message ?? string.Empty;
            label.color = error ? ErrorColor : MenuUi.Dim;
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void SetActive(Component target, bool active)
        {
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }

        private static void SetInteractable(Selectable target, bool value)
        {
            if (target != null)
            {
                target.interactable = value;
            }
        }
    }
}
