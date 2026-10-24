using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
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
        /// <summary>
        /// Spin and pulse for one sparkle child. Turns below zero spin the other way.
        /// </summary>
        [System.Serializable]
        public class SparkleSpin
        {
            [Tooltip("Full rotations per loop. Negative spins the other way.")]
            public float turns = 1f;

            [Tooltip("Seconds for one full spin loop.")]
            public float spinSeconds = 2.5f;

            [Tooltip("Starting angle, in degrees.")]
            public float startAngle;

            [Tooltip("How far the sparkle grows past its rest size. 0 keeps it still.")]
            public float pulse = 0.14f;

            [Tooltip("Seconds to grow to the pulse size and back.")]
            public float pulseSeconds = 0.45f;
        }

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

        [Header("Layout")]
        [SerializeField] private RectTransform card;
        [SerializeField] private RectTransform buttons;
        [SerializeField] private RectTransform sparkles;
        [SerializeField] private RectTransform nameRoot;
        [SerializeField] private RectTransform descriptionRoot;

        [Header("Sparkles")]
        [Tooltip("Seconds for the sparkle group to scale in before the reward, its name, and its description appear.")]
        [SerializeField] private float openRevealDelay = 0.7f;

        [Tooltip("Seconds for the sparkles and the current reward to scale out when the player moves to the next reward.")]
        [SerializeField] private float sparkleScaleOut = 0.22f;

        [Tooltip("One entry per sparkle child, top to bottom. Extra sparkles use a gentle alternating spin.")]
        [SerializeField] private SparkleSpin[] sparkleSpins =
        {
            new SparkleSpin { turns = 1f, spinSeconds = 2.6f, startAngle = 0f, pulse = 0.14f, pulseSeconds = 0.42f },
            new SparkleSpin { turns = -1f, spinSeconds = 3.8f, startAngle = 47f, pulse = 0.14f, pulseSeconds = 0.52f },
            new SparkleSpin { turns = 1.35f, spinSeconds = 1.9f, startAngle = 94f, pulse = 0.14f, pulseSeconds = 0.62f },
        };

        private readonly Queue<Vector3Int> waiting = new Queue<Vector3Int>(4);
        private readonly List<Reward> rewards = new List<Reward>(6);
        private Graphic joystickZone;
        private int revealIndex;
        private bool opening;
        private bool revealBusy;
        private bool closing;

        private Vector3 cardScale = Vector3.one;
        private Vector3 buttonsScale = Vector3.one;
        private Vector3 sparkleScale = Vector3.one;
        private Vector3 rewardScale = Vector3.one;
        private Vector3 descriptionScale = Vector3.one;
        private Vector3 nameScale = Vector3.one;
        private Vector3 openScale = Vector3.one;
        private Vector3 okScale = Vector3.one;
        private Sequence motion;

        protected override void Awake()
        {
            base.Awake();

            if (root == null)
            {
                root = gameObject;
            }

            if (openButton != null)
            {
                openButton.onClick.AddListener(OnOpenClicked);
            }

            if (okButton != null)
            {
                okButton.onClick.AddListener(OnOkClicked);
            }

            CacheMotionTargets();
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

            KillMotion();
            StopSparkleSpin();
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
            revealBusy = false;
            closing = false;

            KillMotion();
            StopSparkleSpin();
            ResetChestVisual();
            PrepareHidden();

            SetOpenVisible(true);
            SetOkVisible(false);
            SetJoystickRaycasts(false);

            game.PushChestPause();
            root.transform.SetAsLastSibling();
            root.SetActive(true);
            PlayIntro();
        }

        private void BuildRewards(int minCoins, int maxCoins, int coinValue)
        {
            rewards.Clear();

            PlayerStats stats = GameManager.Instance != null ? GameManager.Instance.PlayerStats : null;
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
            if (opening || closing || revealBusy || !root.activeSelf)
            {
                return;
            }

            opening = true;
            SnapShown();
            HideOpenButton();

            if (chestAnimator != null)
            {
                chestAnimator.ResetTrigger("Open");
                chestAnimator.SetTrigger("Open");
            }

            StartCoroutine(DelayReveal());
        }

        IEnumerator DelayReveal()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            PlayReveal();
        }

        private void OnOkClicked()
        {
            if (!opening || revealBusy || closing)
            {
                return;
            }

            int next = revealIndex + 1;
            if (next < rewards.Count)
            {
                PlayNextReward(next);
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

        }

        private void BindVisual(Sprite icon, string title, string body)
        {
            if (rewardIcon != null)
            {
                rewardIcon.sprite = icon;
                rewardIcon.enabled = icon != null;
            }

            bool separateTitle = rewardTitle != null && (descriptionRoot == null || !rewardTitle.transform.IsChildOf(descriptionRoot));
            if (separateTitle)
            {
                rewardTitle.text = title ?? "";
            }

            if (rewardBody != null)
            {
                rewardBody.text = separateTitle || string.IsNullOrEmpty(title)
                    ? (body ?? "")
                    : $"<b>{title}</b>\n{body}";
            }
        }

        private void Close()
        {
            if (closing)
            {
                return;
            }

            closing = true;
            revealBusy = false;
            KillMotion();
            StopSparkleSpin();

            motion = DOTween.Sequence().SetUpdate(true);
            JoinScale(motion, card, Vector3.zero, 0.28f, Ease.InBack);
            JoinScale(motion, buttons, Vector3.zero, 0.28f, Ease.InBack);
            JoinScale(motion, sparkles, Vector3.zero, 0.22f, Ease.InBack);
            JoinScale(motion, rewardRoot != null ? rewardRoot.transform : null, Vector3.zero, 0.2f, Ease.InBack);
            JoinScale(motion, nameRoot, Vector3.zero, 0.2f, Ease.InBack);
            JoinScale(motion, descriptionRoot, Vector3.zero, 0.2f, Ease.InBack);
            motion.OnComplete(FinishClose);
        }

        private void FinishClose()
        {
            opening = false;
            closing = false;
            motion = null;
            if (root != null)
            {
                root.SetActive(false);
            }

            SetJoystickRaycasts(true);
            GameManager.Instance?.PopChestPause();

            if (waiting.Count > 0 && GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
            {
                PresentNext();
            }
        }

        private void DismissImmediate()
        {
            opening = false;
            revealBusy = false;
            closing = false;
            KillMotion();
            StopSparkleSpin();
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

        private void CacheMotionTargets()
        {
            if (root == null)
            {
                return;
            }

            cardScale = RestScale(card);
            buttonsScale = RestScale(buttons);
            sparkleScale = RestScale(sparkles);
            rewardScale = RestScale(rewardRoot != null ? rewardRoot.transform : null);
            descriptionScale = RestScale(descriptionRoot);
            nameScale = RestScale(nameRoot);
            openScale = RestScale(openButton != null ? openButton.transform : null);
            okScale = RestScale(okButton != null ? okButton.transform : null);
        }

        private static Vector3 RestScale(Transform target)
        {
            if (target == null || target.localScale.sqrMagnitude < 0.0001f)
            {
                return Vector3.one;
            }

            return target.localScale;
        }

        private void PrepareHidden()
        {
            SetScale(card, Vector3.zero);
            SetScale(buttons, Vector3.zero);
            SetScale(sparkles, Vector3.zero);
            SetScale(rewardRoot != null ? rewardRoot.transform : null, Vector3.zero);
            SetScale(descriptionRoot, Vector3.zero);
            SetScale(openButton != null ? openButton.transform : null, openScale);
            SetScale(okButton != null ? okButton.transform : null, Vector3.zero);

            if (sparkles != null)
            {
                sparkles.gameObject.SetActive(false);
            }

            if (rewardRoot != null)
            {
                rewardRoot.SetActive(false);
            }

            if (descriptionRoot != null)
            {
                descriptionRoot.gameObject.SetActive(false);
            }

            if (nameRoot != null)
            {
                SetScale(nameRoot, Vector3.zero);
                nameRoot.gameObject.SetActive(false);
            }
        }

        private void PlayIntro()
        {
            KillMotion();
            motion = DOTween.Sequence().SetUpdate(true);
            JoinScale(motion, card, cardScale, 0.42f, Ease.OutBack);
            JoinScale(motion, buttons, buttonsScale, 0.42f, Ease.OutBack);
        }

        private void PlayReveal()
        {
            KillMotion();
            revealBusy = true;
            motion = DOTween.Sequence().SetUpdate(true);
            AppendSparkleBloom(motion);
            motion.AppendCallback(() => ShowReward(0));
            AppendRewardPop(motion);
            motion.AppendCallback(() =>
            {
                SetOkVisible(true);
                SetScale(okButton != null ? okButton.transform : null, Vector3.zero);
            });
            AppendPop(motion, okButton != null ? okButton.transform : null, okScale, 0.26f);
            motion.OnComplete(() => revealBusy = false);
        }

        private void PlayNextReward(int index)
        {
            KillMotion();
            revealBusy = true;
            float shrink = Mathf.Max(0.08f, sparkleScaleOut);
            motion = DOTween.Sequence().SetUpdate(true);
            JoinScale(motion, sparkles, Vector3.zero, shrink, Ease.InBack);
            JoinScale(motion, rewardRoot != null ? rewardRoot.transform : null, Vector3.zero, shrink, Ease.InBack);
            JoinScale(motion, nameRoot, Vector3.zero, shrink, Ease.InBack);
            JoinScale(motion, descriptionRoot, Vector3.zero, shrink, Ease.InBack);
            AppendSparkleBloom(motion);
            motion.AppendCallback(() => ShowReward(index));
            AppendRewardPop(motion);
            motion.OnComplete(() => revealBusy = false);
        }

        /// <summary>Scales the sparkle group up from nothing and keeps the children spinning.</summary>
        private void AppendSparkleBloom(Sequence sequence)
        {
            if (sequence == null)
            {
                return;
            }

            sequence.AppendCallback(() =>
            {
                if (sparkles == null)
                {
                    return;
                }

                sparkles.gameObject.SetActive(true);
                sparkles.localScale = Vector3.zero;
                SpinSparkles();
            });

            if (sparkles != null)
            {
                float bloom = Mathf.Max(0.35f, openRevealDelay);
                sequence.Append(sparkles.DOScale(sparkleScale, bloom).SetEase(Ease.OutBack));
            }
        }

        private void AppendRewardPop(Sequence sequence)
        {
            AppendPop(sequence, rewardRoot != null ? rewardRoot.transform : null, rewardScale, 0.34f);
            AppendPop(sequence, nameRoot, nameScale, 0.28f);
            AppendPop(sequence, descriptionRoot, descriptionScale, 0.3f);
        }

        private void HideOpenButton()
        {
            if (openButton == null)
            {
                return;
            }

            openButton.transform.DOKill();
            openButton.transform.DOScale(Vector3.zero, 0.16f)
                .SetEase(Ease.InBack)
                .SetUpdate(true)
                .OnComplete(() => SetOpenVisible(false));
        }

        private void SnapShown()
        {
            SetScale(card, cardScale);
            SetScale(buttons, buttonsScale);
        }

        private void SpinSparkles()
        {
            if (sparkles == null)
            {
                return;
            }

            for (int i = 0; i < sparkles.childCount; i++)
            {
                Transform child = sparkles.GetChild(i);
                child.DOKill();
                SparkleSpin spin = SpinFor(i);
                child.localRotation = Quaternion.Euler(0f, 0f, spin.startAngle);
                child.localScale = Vector3.one;

                float turns = spin.turns;
                if (Mathf.Abs(turns) > 0.001f)
                {
                    float duration = Mathf.Max(0.05f, spin.spinSeconds);
                    child.DOLocalRotate(new Vector3(0f, 0f, 360f * turns), duration, RotateMode.FastBeyond360)
                        .SetEase(Ease.Linear)
                        .SetLoops(-1, LoopType.Incremental)
                        .SetUpdate(true);
                }

                if (spin.pulse > 0.001f)
                {
                    float pulseDuration = Mathf.Max(0.05f, spin.pulseSeconds);
                    child.DOScale(1f + spin.pulse, pulseDuration)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetUpdate(true);
                }
            }
        }

        private SparkleSpin SpinFor(int index)
        {
            if (sparkleSpins != null && index >= 0 && index < sparkleSpins.Length && sparkleSpins[index] != null)
            {
                return sparkleSpins[index];
            }

            if (index == 0)
            {
                return new SparkleSpin { turns = 1f, spinSeconds = 2.6f, startAngle = 0f, pulse = 0.14f, pulseSeconds = 0.42f };
            }

            if (index == 1)
            {
                return new SparkleSpin { turns = -1f, spinSeconds = 3.8f, startAngle = 47f, pulse = 0.14f, pulseSeconds = 0.52f };
            }

            if (index == 2)
            {
                return new SparkleSpin { turns = 1.35f, spinSeconds = 1.9f, startAngle = 94f, pulse = 0.14f, pulseSeconds = 0.62f };
            }

            return new SparkleSpin
            {
                turns = index % 2 == 0 ? 1f : -1f,
                spinSeconds = 2.4f,
                startAngle = (index * 47f) % 360f,
                pulse = 0.14f,
                pulseSeconds = 0.42f + (index * 0.1f),
            };
        }

        private void StopSparkleSpin()
        {
            if (sparkles == null)
            {
                return;
            }

            sparkles.DOKill();
            for (int i = 0; i < sparkles.childCount; i++)
            {
                Transform child = sparkles.GetChild(i);
                child.DOKill();
                child.localScale = Vector3.one;
            }
        }

        private void AppendPop(Sequence sequence, Transform target, Vector3 rest, float duration)
        {
            if (sequence == null || target == null)
            {
                return;
            }

            // Scale is applied when this step starts, so an earlier shrink tween is not wiped
            // while the sequence is still being built.
            sequence.AppendCallback(() =>
            {
                if (target == null)
                {
                    return;
                }

                target.gameObject.SetActive(true);
                target.localScale = Vector3.zero;
            });
            sequence.Append(target.DOScale(rest, duration).SetEase(Ease.OutBack));
        }

        private static void JoinScale(Sequence sequence, Transform target, Vector3 scale, float duration, Ease ease)
        {
            if (sequence == null || target == null)
            {
                return;
            }

            sequence.Join(target.DOScale(scale, duration).SetEase(ease));
        }

        private void KillMotion()
        {
            if (motion != null)
            {
                motion.Kill();
                motion = null;
            }
        }

        private static void SetScale(Transform target, Vector3 scale)
        {
            if (target != null)
            {
                target.localScale = scale;
            }
        }

        private void SetOpenVisible(bool visible)
        {
            if (openButton != null)
            {
                openButton.gameObject.SetActive(visible);
                if (visible)
                {
                    openButton.transform.localScale = openScale;
                }
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
            if (joystickZone == null && VirtualJoystick.Instance != null)
            {
                joystickZone = VirtualJoystick.Instance.GetComponent<Graphic>();
            }

            if (joystickZone != null)
            {
                joystickZone.raycastTarget = enabled;
            }
        }
    }
}
