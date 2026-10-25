using UnityEngine;
using KingsRescue.Meta;

namespace KingsRescue.Core
{
    /// <summary>
    /// Offsets the camera for a short hit shake. Does nothing while the setting is off.
    /// The camera is otherwise stationary; the world scrolls under it.
    /// </summary>
    public class ScreenShake : MonoBehaviour
    {
        public static ScreenShake Instance { get; private set; }

        private Vector3 rest;
        private float trauma;

        private void Awake()
        {
            Instance = this;
            rest = transform.localPosition;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void LateUpdate()
        {
            if (trauma <= 0f || !GameSettings.ScreenShake || IsHeldByUi())
            {
                trauma = 0f;
                transform.localPosition = rest;
                return;
            }

            float magnitude = trauma * trauma * 0.22f;
            Vector2 offset = Random.insideUnitCircle * magnitude;
            transform.localPosition = rest + new Vector3(offset.x, offset.y, 0f);
            trauma = Mathf.Max(0f, trauma - Time.deltaTime * 1.8f);
        }

        public static void Add(float amount)
        {
            if (Instance == null || amount <= 0f || !GameSettings.ScreenShake || IsHeldByUi())
            {
                return;
            }

            Instance.trauma = Mathf.Clamp01(Instance.trauma + amount);
        }

        /// <summary>
        /// Level-up, chest, pause, and game over freeze time. Shake would otherwise
        /// keep offsetting the camera, because its fade uses scaled time.
        /// </summary>
        private static bool IsHeldByUi()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                return false;
            }

            switch (game.State)
            {
                case GameState.LevelUp:
                case GameState.Chest:
                case GameState.Paused:
                case GameState.GameOver:
                    return true;
                default:
                    return false;
            }
        }
    }
}
