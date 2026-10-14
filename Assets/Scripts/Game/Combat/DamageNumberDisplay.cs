using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Spawns pooled damage numbers. Numbers are not attached to enemies, so a burst of hits
    /// can show several at once and a dead enemy does not take its numbers with it.
    /// </summary>
    public class DamageNumberDisplay : SingletonBehaviour<DamageNumberDisplay>
    {
        [SerializeField] private DamageNumber numberPrefab;

        public static void Popup(Vector2 world, float amount, bool critical)
        {
            DamageNumberDisplay display = Instance;
            if (display == null || display.numberPrefab == null || amount <= 0f)
            {
                return;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            Vector2 at = world + new Vector2(Random.Range(-0.22f, 0.22f), Random.Range(0.2f, 0.5f));
            DamageNumber number = pool.Spawn(display.numberPrefab, at, Quaternion.identity);
            if (number != null)
            {
                number.Show(amount, critical);
            }
        }
    }
}
