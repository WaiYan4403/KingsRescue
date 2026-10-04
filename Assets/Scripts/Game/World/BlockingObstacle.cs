using System.Collections.Generic;
using UnityEngine;

namespace KingsRescue.World
{
    /// <summary>
    /// Marks a prop collider as solid for the player. Rocks and trees use this;
    /// grass and breakables do not.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BlockingObstacle : MonoBehaviour
    {
        public static readonly List<BlockingObstacle> Active = new List<BlockingObstacle>(32);

        public Collider2D Collider { get; private set; }

        private void Awake()
        {
            Collider = GetComponent<Collider2D>();
        }

        private void OnEnable()
        {
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }
    }
}
