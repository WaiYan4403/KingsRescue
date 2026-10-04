using System.Collections.Generic;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Everything a player weapon is allowed to damage: enemies and destructible props.
    /// Weapons sweep this list instead of querying physics, which keeps enemy prefabs
    /// free of colliders and rigidbodies.
    /// </summary>
    public static class HitRegistry
    {
        public static readonly List<Health> Targets = new List<Health>(320);

        private static readonly HashSet<Health> Membership = new HashSet<Health>();

        public static void Register(Health health)
        {
            if (health != null && Membership.Add(health))
            {
                Targets.Add(health);
            }
        }

        public static void Unregister(Health health)
        {
            if (health == null || !Membership.Remove(health))
            {
                return;
            }

            int last = Targets.Count - 1;
            int index = Targets.IndexOf(health);
            if (index < 0)
            {
                return;
            }

            Targets[index] = Targets[last];
            Targets.RemoveAt(last);
        }
    }
}
