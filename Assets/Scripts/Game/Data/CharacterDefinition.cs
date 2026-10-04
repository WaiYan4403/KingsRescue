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

        public string Id => string.IsNullOrWhiteSpace(id) ? name : id;

        [Tooltip("Granted at run start. Other characters cannot pick this weapon up.")]
        public WeaponDefinition uniqueWeapon;
    }
}
