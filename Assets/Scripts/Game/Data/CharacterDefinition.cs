using UnityEngine;

namespace KingsRescue.Data
{
    /// <summary>
    /// One playable hero. Their unique weapon is granted at the start of a run
    /// and cannot be acquired by anyone else.
    /// </summary>
    [CreateAssetMenu(menuName = "King's Rescue/Character Definition", fileName = "Character_")]
    public class CharacterDefinition : ScriptableObject
    {
        [Tooltip("Stable save key. Defaults to the asset name.")]
        public string id = "";

        public string displayName = "Hero";
        [TextArea(2, 4)] public string description = "";
        public Sprite portrait;
        public GameObject playerPrefab;

        [Header("Visuals")]
        [Tooltip("Mirror the hero sprite when moving left. Leave off if the animator already faces both ways, or the art is not reversible.")]
        public bool canFlipSprite;

        [Tooltip("Hover over the road. Rocks and trees do not block this hero.")]
        public bool ignoresObstacles;

        public string Id => string.IsNullOrWhiteSpace(id) ? name : id;

        [Tooltip("Granted at run start. Other characters cannot pick this weapon up.")]
        public WeaponDefinition uniqueWeapon;

        [Header("Power-Up")]
        [Tooltip("Used while a rare arcade power-up is active. Leave empty if this hero has no powered form.")]
        public WeaponDefinition powerUpWeapon;

        [Tooltip("Seconds the powered form lasts before Duration is applied.")]
        public float powerUpDuration = 8f;

        [Header("Starting Stats")]
        [Tooltip("Applied on top of the player prefab's bases before meta upgrades.")]
        public StatModifier[] startingStats = System.Array.Empty<StatModifier>();

        [Tooltip("Times this hero can come back from a killing blow in one run.")]
        public int extraLives;
    }
}
