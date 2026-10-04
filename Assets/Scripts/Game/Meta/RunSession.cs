using KingsRescue.Data;

namespace KingsRescue.Meta
{
    /// <summary>
    /// Values chosen on the main menu and read when Gameplay boots.
    /// </summary>
    public static class RunSession
    {
        public const string GameplayScene = "Gameplay";
        public const string MenuScene = "MainMenu";

        public static CharacterDefinition SelectedCharacter { get; set; }
    }
}
