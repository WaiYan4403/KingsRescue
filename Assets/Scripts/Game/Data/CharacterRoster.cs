using UnityEngine;

namespace KingsRescue.Data
{
    [CreateAssetMenu(menuName = "King's Rescue/Character Roster", fileName = "CharacterRoster")]
    public class CharacterRoster : ScriptableObject
    {
        public CharacterDefinition[] characters = new CharacterDefinition[0];
    }
}
