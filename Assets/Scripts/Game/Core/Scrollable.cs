using UnityEngine;

namespace KingsRescue.Core
{
    /// <summary>
    /// Drags a transform downward with the world scroll. Anything that should feel
    /// "left behind" as the player runs forward gets one of these.
    /// </summary>
    public class Scrollable : MonoBehaviour
    {
        [Tooltip("1 = moves with the world, 0 = pinned to the screen. Use less than 1 for parallax.")]
        [SerializeField, Range(0f, 2f)] private float scrollInfluence = 1f;

        public float ScrollInfluence
        {
            get => scrollInfluence;
            set => scrollInfluence = value;
        }

        private void Update()
        {
            float speed = ScrollDirector.Speed * scrollInfluence;
            if (speed == 0f)
            {
                return;
            }

            transform.position += Vector3.down * (speed * Time.deltaTime);
        }
    }
}
