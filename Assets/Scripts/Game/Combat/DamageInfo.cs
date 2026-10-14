using UnityEngine;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Everything a hit needs to carry. Passed by readonly reference so the hot
    /// combat loop does not allocate.
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector2 HitPoint;
        public readonly Vector2 Direction;
        public readonly float Knockback;
        public readonly GameObject Source;
        public readonly bool Critical;

        public DamageInfo(float amount, Vector2 hitPoint, Vector2 direction, float knockback = 0f, GameObject source = null, bool critical = false)
        {
            Amount = amount;
            HitPoint = hitPoint;
            Direction = direction;
            Knockback = knockback;
            Source = source;
            Critical = critical;
        }

        public static DamageInfo Simple(float amount, GameObject source = null)
        {
            return new DamageInfo(amount, Vector2.zero, Vector2.zero, 0f, source, false);
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }

        void TakeDamage(in DamageInfo info);
    }
}
