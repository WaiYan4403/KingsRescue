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
        public string displayName = "Hero";
        [TextArea(2, 4)] public string description = "";
        public Sprite portrait;

        [Tooltip("Granted at run start. Other characters cannot pick this weapon up.")]
        public WeaponDefinition uniqueWeapon;
    }
}
