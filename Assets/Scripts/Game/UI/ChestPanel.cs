using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.InputControl;
using KingsRescue.Player;
using KingsRescue.Progression;

namespace KingsRescue.UI
{
    /// <summary>
    /// Vampire Survivors-style chest overlay: pause, open the chest, then reveal
    /// rewards one at a time. The world prop stays on its opened sprite.
    /// </summary>
    public class ChestPanel : SingletonBehaviour<ChestPanel>
    {
        private struct Reward
        {
            public UpgradeDefinition upgrade;
            public int coins;
        }

        [SerializeField] private GameObject root;
        [SerializeField] private Animator chestAnimator;
        [SerializeField] private GameObject sparkle;
        [SerializeField] private Button openButton;
        [SerializeField] private Button okButton;
        [SerializeField] private GameObject rewardRoot;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private TMP_Text rewardTitle;
        [SerializeField] private TMP_Text rewardBody;
        [SerializeField] private Sprite coinIcon;

        [SerializeField] private float openRevealDelay = 0.7f;

        private readonly Queue<Vector3Int> waiting = new Queue<Vector3Int>(4);
        private readonly List<Reward> rewards = new List<Reward>(6);
        private VirtualJoystick joystick;
        private Graphic joystickZone;
        private int revealIndex;
        private bool opening;

        protected override void Awake()
        {
            base.Awake();

            if (root == null)
            {
                root = gameObject;
            }

            joystick = FindAnyObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
            if (joystick != null)
            {
                joystickZone = joystick.GetComponent<Graphic>();
            }

            if (openButton != null)
            {
                openButton.onClick.AddListener(OnOpenClicked);
            }

            if (okButton != null)
            {
                okButton.onClick.AddListener(OnOkClicked);
            }
        }

        private void Start()
        {
            GameManager game = GameManager.Instance;
            if (game != null)
            {
                game.StateChanged += OnStateChanged;
                game.HeroDied += DismissImmediate;
            }

            root.SetActive(false);
        }

        protected override void OnDestroy()
        {
            GameManager game = GameManager.Instance;
            if (game != null)
            {
                game.StateChanged -= OnStateChanged;
                game.HeroDied -= DismissImmediate;
            }

            if (openButton != null)
            {
                openButton.onClick.RemoveListener(OnOpenClicked);
            }

            if (okButton != null)
            {
                okButton.onClick.RemoveListener(OnOkClicked);
            }

            base.OnDestroy();
        }

        /// <summary>
        /// Opens the overlay after the player walks up to a chest. Extra arguments are the coin purse.
        /// </summary>
        public void Open(int minCoins, int maxCoins, int coinValue)
        {
            GameManager game = GameManager.Instance;
            if (game == null || game.State == GameState.GameOver || game.State == GameState.Boot)
            {
                return;
            }

            int min = Mathf.Max(0, minCoins);
            int max = Mathf.Max(min, maxCoins);
            int value = Mathf.Max(1, coinValue);
            waiting.Enqueue(new Vector3Int(min, max, value));

            if (!root.activeSelf && !opening)
            {
                PresentNext();
            }
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
            {
                DismissImmediate();
                return;
            }

            if (state == GameState.Playing && waiting.Count > 0 && !root.activeSelf && !opening)
            {
                PresentNext();
            }
        }

        private void PresentNext()
        {
            if (waiting.Count == 0)
            {
                return;
            }

            GameManager game = GameManager.Instance;
            if (game == null || game.State == GameState.GameOver)
            {
                waiting.Clear();
                return;
            }

            if (game.State == GameState.LevelUp)
            {
                return;
            }

            Vector3Int purse = waiting.Dequeue();
            BuildRewards(purse.x, purse.y, purse.z);
            revealIndex = 0;
            opening = false;

            ResetChestVisual();
            if (sparkle != null)
            {
                sparkle.SetActive(false);
            }

            if (rewardRoot != null)
            {
                rewardRoot.SetActive(false);
            }

            SetOpenVisible(true);
            SetOkVisible(false);
            SetJoystickRaycasts(false);

            game.PushChestPause();
            root.transform.SetAsLastSibling();
            root.SetActive(true);
        }

        private void BuildRewards(int minCoins, int maxCoins, int coinValue)
        {
            rewards.Clear();

            PlayerStats stats = FindAnyObjectByType<PlayerStats>();
            float luck = stats != null ? Mathf.Max(0.1f, stats.Luck) : 1f;
            float coinGain = stats != null ? stats.CoinGain : 1f;

            UpgradeService service = UpgradeService.Instance;
            int rolls = 1;
            if (Random.value < Mathf.Clamp01(0.38f * luck))
            {
                rolls++;
            }

            if (Random.value < Mathf.Clamp01(0.14f * luck))
            {
                rolls++;
            }

            if (service != null)
            {
                for (int i = 0; i < rolls; i++)
                {
                    if (service.TryPickRandomItem(out UpgradeDefinition picked))
                    {
                        rewards.Add(new Reward { upgrade = picked });
                    }
                }
            }

            int raw = Random.Range(minCoins, maxCoins + 1);
            int coins = Mathf.Max(1, Mathf.RoundToInt(raw * coinValue * coinGain));
            rewards.Add(new Reward { coins = coins });
        }

        private void OnOpenClicked()
        {
            if (opening || !root.activeSelf)
            {
                return;
            }

            opening = true;
            SetOpenVisible(false);
            if (chestAnimator != null)
            {
                chestAnimator.ResetTrigger("Open");
                chestAnimator.SetTrigger("Open");
            }

            StartCoroutine(RevealAfterOpen());
        }

        private IEnumerator RevealAfterOpen()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, openRevealDelay));
            if (sparkle != null)
            {
                sparkle.SetActive(true);
            }

            ShowReward(0);
            SetOkVisible(true);
        }

        private void OnOkClicked()
        {
            if (!opening)
            {
                return;
            }

            int next = revealIndex + 1;
            if (next < rewards.Count)
            {
                ShowReward(next);
                return;
            }

            Close();
        }

        private void ShowReward(int index)
        {
            if (index < 0 || index >= rewards.Count)
            {
                return;
            }

            revealIndex = index;
            Reward reward = rewards[index];
            UpgradeService service = UpgradeService.Instance;
            WeaponLoadout loadout = service != null ? service.Loadout : null;

            if (reward.upgrade != null)
            {
                service?.Apply(reward.upgrade);
                BindVisual(
                    reward.upgrade.Icon(),
                    reward.upgrade.Title(loadout),
                    reward.upgrade.Description(loadout));
            }
            else
            {
                GameManager.Instance?.AddCoins(reward.coins);
                BindVisual(coinIcon, $"+{reward.coins} Coins", "Taken from the chest.");
            }

            if (rewardRoot != null)
            {
                rewardRoot.SetActive(true);
            }
        }

        private void BindVisual(Sprite icon, string title, string body)
        {
            if (rewardIcon != null)
            {
                rewardIcon.sprite = icon;
                rewardIcon.enabled = icon != null;
            }

            if (rewardTitle != null)
            {
                rewardTitle.text = title ?? "";
            }

            if (rewardBody != null)
            {
                rewardBody.text = body ?? "";
            }
        }

        private void Close()
        {
            StopAllCoroutines();
            opening = false;
            root.SetActive(false);
            SetJoystickRaycasts(true);
            GameManager.Instance?.PopChestPause();

            if (waiting.Count > 0 && GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            {
                PresentNext();
            }
        }

        private void DismissImmediate()
        {
            StopAllCoroutines();
            opening = false;
            waiting.Clear();
            rewards.Clear();
            if (root != null)
            {
                root.SetActive(false);
            }

            SetJoystickRaycasts(true);
            if (GameManager.Instance != null && GameManager.Instance.State == GameState.Chest)
            {
                GameManager.Instance.PopChestPause();
            }
        }

        private void ResetChestVisual()
        {
            if (chestAnimator == null)
            {
                return;
            }

            chestAnimator.ResetTrigger("Open");
            chestAnimator.Play("Chest_Idle", 0, 0f);
            chestAnimator.Update(0f);
        }

        private void SetOpenVisible(bool visible)
        {
            if (openButton != null)
            {
                openButton.gameObject.SetActive(visible);
            }
        }

        private void SetOkVisible(bool visible)
        {
            if (okButton != null)
            {
                okButton.gameObject.SetActive(visible);
            }
        }

        private void SetJoystickRaycasts(bool enabled)
        {
            if (joystickZone != null)
            {
                joystickZone.raycastTarget = enabled;
            }
        }
    }
}
