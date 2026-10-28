using UnityEngine;

namespace KingsRescue.Data
{
    /// <summary>
    /// The level-up and chest card list. Gameplay and the Collections page both read this
    /// so a new weapon or passive shows up in the book without a second copy of the list.
    /// </summary>
    [CreateAssetMenu(menuName = "King's Rescue/Upgrade Pool", fileName = "UpgradePool")]
    public class UpgradePool : ScriptableObject
    {
        public UpgradeDefinition[] entries = System.Array.Empty<UpgradeDefinition>();

        private static UpgradePool cached;

        public static UpgradePool Load()
        {
            if (cached == null)
            {
                cached = Resources.Load<UpgradePool>("UpgradePool");
            }

            return cached;
        }
    }
}
