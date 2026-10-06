using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Core;
using KingsRescue.Player;
using KingsRescue.Progression;

namespace KingsRescue.UI
{
    /// <summary>
    /// Portrait HUD: health and EXP bars, level, run clock, coins and kills.
    /// Bars are filled Images rather than Sliders so the prefab stays light.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [Header("Bars (Image type: Filled)")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Image xpFill;

        [Header("Labels")]
        [SerializeField] private TMP_Text healthLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField] private TMP_Text distanceLabel;
        [SerializeField] private TMP_Text coinLabel;
        [SerializeField] private TMP_Text killLabel;

        [Header("Scene References")]
        [SerializeField] private PlayerHealth playerHealth;

        [Tooltip("Seconds between clock refreshes. The clock only shows whole seconds.")]
        [SerializeField] private float clockInterval = 0.25f;

        private float nextClockUpdate;

        private static Sprite whiteFillSprite;

        private void Awake()
        {
            if (playerHealth == null)
            {
                playerHealth = FindAnyObjectByType<PlayerHealth>();
            }

            // uGUI Filled images with no sprite ignore fillAmount and always draw full.
            PrepareFillImage(healthFill);
            PrepareFillImage(xpFill);
        }

        private void OnEnable()
        {
            BindXp(true);
        }

        private void OnDisable()
        {
            BindXp(false);
        }

        private void Start()
        {
            if (playerHealth != null)
            {
                playerHealth.HealthChanged += OnHealthChanged;
                OnHealthChanged(playerHealth.Current, playerHealth.Max);
            }

            RefreshXp();

            GameManager game = GameManager.Instance;
            if (game != null)
            {
                game.CoinsChanged += OnCoinsChanged;
                game.KillsChanged += OnKillsChanged;
                OnCoinsChanged(game.Coins);
                OnKillsChanged(game.Kills);
            }
        }

        private void OnDestroy()
        {
            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= OnHealthChanged;
            }

            BindXp(false);

            GameManager game = GameManager.Instance;
            if (game != null)
            {
                game.CoinsChanged -= OnCoinsChanged;
                game.KillsChanged -= OnKillsChanged;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextClockUpdate)
            {
                return;
            }

            nextClockUpdate = Time.unscaledTime + clockInterval;

            GameManager game = GameManager.Instance;
            float runTime = game != null ? game.RunTime : 0f;
            string clock = FormatClock(runTime);
            string distance = FormatDistance(ScrollDirector.DistanceMeters);

            if (timerLabel != null)
            {
                timerLabel.text = clock;
            }

            if (distanceLabel != null)
            {
                distanceLabel.text = distance;
            }
            else if (timerLabel != null)
            {
                timerLabel.text = $"{clock}  {distance}";
            }
        }

        public static string FormatClock(float seconds)
        {
            int total = Mathf.FloorToInt(Mathf.Max(0f, seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        public static string FormatDistance(float meters)
        {
            float value = Mathf.Max(0f, meters);
            if (value >= 1000f)
            {
                return $"{value / 1000f:0.#}km";
            }

            return $"{Mathf.FloorToInt(value)}m";
        }

        private void OnHealthChanged(float current, float max)
        {
            if (healthFill != null)
            {
                healthFill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            }

            if (healthLabel != null)
            {
                healthLabel.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        private void BindXp(bool subscribe)
        {
            ExperienceSystem xp = ExperienceSystem.Instance;
            if (xp == null)
            {
                return;
            }

            xp.XpChanged -= OnXpChanged;
            if (subscribe)
            {
                xp.XpChanged += OnXpChanged;
            }
        }

        private void RefreshXp()
        {
            ExperienceSystem xp = ExperienceSystem.Instance;
            if (xp != null)
            {
                OnXpChanged(xp.CurrentXp, xp.XpToNext);
            }
            else if (xpFill != null)
            {
                xpFill.fillAmount = 0f;
            }
        }

        private void OnXpChanged(float current, float needed)
        {
            if (xpFill != null)
            {
                PrepareFillImage(xpFill);
                xpFill.fillAmount = needed > 0f ? Mathf.Clamp01(current / needed) : 0f;
            }

            if (levelLabel != null)
            {
                int level = ExperienceSystem.Instance != null ? ExperienceSystem.Instance.Level : 1;
                levelLabel.text = $"Lv {level}";
            }
        }

        private static void PrepareFillImage(Image image)
        {
            if (image == null)
            {
                return;
            }

            if (image.sprite == null)
            {
                image.sprite = WhiteFillSprite();
            }

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
        }

        private static Sprite WhiteFillSprite()
        {
            if (whiteFillSprite == null)
            {
                whiteFillSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                    new Vector2(0.5f, 0.5f),
                    4f);
                whiteFillSprite.name = "HudFillWhite";
            }

            return whiteFillSprite;
        }

        private void OnCoinsChanged(int coins)
        {
            if (coinLabel != null)
            {
                coinLabel.text = coins.ToString();
            }
        }

        private void OnKillsChanged(int kills)
        {
            if (killLabel != null)
            {
                killLabel.text = kills.ToString();
            }
        }
    }
}
