using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;
using KingsRescue.Progression;
using KingsRescue.Spawning;
using KingsRescue.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PrototypeWire
{
    public static string Run()
    {
        var player = GameObject.Find("Player");
        var systems = GameObject.Find("Systems");
        var levelUp = GameObject.Find("LevelUpPanel");
        if (player == null || systems == null)
        {
            return "Player or Systems missing";
        }

        var sword = Load<WeaponDefinition>("Assets/Data/Weapons/Weapon_Sword.asset");
        var loadout = player.GetComponent<WeaponLoadout>();
        SetObj(loadout, "startingWeapon", sword);

        var stats = player.GetComponent<PlayerStats>();
        var soStats = new SerializedObject(stats);
        var overrides = soStats.FindProperty("baseOverrides");
        overrides.arraySize = 3;
        overrides.GetArrayElementAtIndex(0).FindPropertyRelative("stat").enumValueIndex = (int)StatType.MoveSpeed;
        overrides.GetArrayElementAtIndex(0).FindPropertyRelative("value").floatValue = 6f;
        overrides.GetArrayElementAtIndex(1).FindPropertyRelative("stat").enumValueIndex = (int)StatType.MaxHp;
        overrides.GetArrayElementAtIndex(1).FindPropertyRelative("value").floatValue = 120f;
        overrides.GetArrayElementAtIndex(2).FindPropertyRelative("stat").enumValueIndex = (int)StatType.MagnetRadius;
        overrides.GetArrayElementAtIndex(2).FindPropertyRelative("value").floatValue = 2.5f;
        soStats.ApplyModifiedPropertiesWithoutUndo();

        var waves = systems.GetComponent<WaveDirector>();
        var soWaves = new SerializedObject(waves);
        var list = soWaves.FindProperty("waves");
        list.arraySize = 3;
        list.GetArrayElementAtIndex(0).objectReferenceValue = Load<WaveDefinition>("Assets/Data/Waves/Wave_01.asset");
        list.GetArrayElementAtIndex(1).objectReferenceValue = Load<WaveDefinition>("Assets/Data/Waves/Wave_02.asset");
        list.GetArrayElementAtIndex(2).objectReferenceValue = Load<WaveDefinition>("Assets/Data/Waves/Wave_03.asset");
        soWaves.FindProperty("endlessWave").objectReferenceValue = Load<WaveDefinition>("Assets/Data/Waves/Wave_Endless.asset");
        soWaves.FindProperty("maxAliveEnemies").intValue = 140;
        soWaves.ApplyModifiedPropertiesWithoutUndo();

        var upgrades = systems.GetComponent<UpgradeService>();
        var soUp = new SerializedObject(upgrades);
        soUp.FindProperty("fallback").objectReferenceValue = Load<UpgradeDefinition>("Assets/Data/Upgrades/Upgrade_Coins.asset");
        soUp.FindProperty("loadout").objectReferenceValue = loadout;
        soUp.FindProperty("stats").objectReferenceValue = stats;
        var pool = soUp.FindProperty("pool");
        string[] poolPaths =
        {
            "Assets/Data/Upgrades/Upgrade_Sword_Level.asset",
            "Assets/Data/Upgrades/Upgrade_Bow_New.asset",
            "Assets/Data/Upgrades/Upgrade_Bow_Level.asset",
            "Assets/Data/Upgrades/Upgrade_Orbit_New.asset",
            "Assets/Data/Upgrades/Upgrade_Orbit_Level.asset",
            "Assets/Data/Upgrades/Upgrade_Aura_New.asset",
            "Assets/Data/Upgrades/Upgrade_Aura_Level.asset",
            "Assets/Data/Upgrades/Upgrade_Boots.asset",
            "Assets/Data/Upgrades/Upgrade_Gauntlet.asset",
            "Assets/Data/Upgrades/Upgrade_Hourglass.asset",
            "Assets/Data/Upgrades/Upgrade_Lodestone.asset",
            "Assets/Data/Upgrades/Upgrade_Plate.asset",
            "Assets/Data/Upgrades/Upgrade_Tome.asset"
        };
        pool.arraySize = poolPaths.Length;
        for (int i = 0; i < poolPaths.Length; i++)
        {
            pool.GetArrayElementAtIndex(i).objectReferenceValue = Load<UpgradeDefinition>(poolPaths[i]);
        }

        soUp.ApplyModifiedPropertiesWithoutUndo();

        var props = systems.GetComponent<PropSpawner>();
        var soProps = new SerializedObject(props);
        var breakables = soProps.FindProperty("breakables");
        breakables.arraySize = 2;
        breakables.GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue = Load<GameObject>("Assets/Prefabs/Props/Prop_Pot.prefab");
        breakables.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 70f;
        breakables.GetArrayElementAtIndex(1).FindPropertyRelative("prefab").objectReferenceValue = Load<GameObject>("Assets/Prefabs/Props/Prop_Chest.prefab");
        breakables.GetArrayElementAtIndex(1).FindPropertyRelative("weight").floatValue = 30f;
        var decorations = soProps.FindProperty("decorations");
        decorations.arraySize = 2;
        decorations.GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue = Load<GameObject>("Assets/Prefabs/Props/Prop_Tree.prefab");
        decorations.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 60f;
        decorations.GetArrayElementAtIndex(1).FindPropertyRelative("prefab").objectReferenceValue = Load<GameObject>("Assets/Prefabs/Props/Prop_Rock.prefab");
        decorations.GetArrayElementAtIndex(1).FindPropertyRelative("weight").floatValue = 40f;
        soProps.ApplyModifiedPropertiesWithoutUndo();

        var poolMgr = systems.GetComponent<ObjectPoolManager>();
        var soPool = new SerializedObject(poolMgr);
        var prewarm = soPool.FindProperty("pools");
        prewarm.arraySize = 8;
        SetPrewarm(prewarm, 0, "Assets/Prefabs/Enemies/Enemy_Orc.prefab", 80);
        SetPrewarm(prewarm, 1, "Assets/Prefabs/Enemies/Enemy_Goblin.prefab", 80);
        SetPrewarm(prewarm, 2, "Assets/Prefabs/Enemies/Enemy_Skelly.prefab", 80);
        SetPrewarm(prewarm, 3, "Assets/Prefabs/Pickups/Pickup_XpGem.prefab", 60);
        SetPrewarm(prewarm, 4, "Assets/Prefabs/Pickups/Pickup_Coin.prefab", 40);
        SetPrewarm(prewarm, 5, "Assets/Prefabs/Weapons/Projectile_Arrow.prefab", 60);
        SetPrewarm(prewarm, 6, "Assets/Prefabs/Props/Prop_Pot.prefab", 20);
        SetPrewarm(prewarm, 7, "Assets/Prefabs/Props/Prop_Chest.prefab", 12);
        soPool.ApplyModifiedPropertiesWithoutUndo();

        if (levelUp != null)
        {
            var panel = levelUp.GetComponent<LevelUpPanel>();
            var soPanel = new SerializedObject(panel);
            var cards = soPanel.FindProperty("cards");
            var views = levelUp.GetComponentsInChildren<UpgradeCardView>(true);
            cards.arraySize = views.Length;
            for (int i = 0; i < views.Length; i++)
            {
                cards.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
            }

            soPanel.FindProperty("root").objectReferenceValue = levelUp;
            soPanel.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        return "Rewired waves, upgrades, props, pool prewarm, player stats, and level-up cards.";
    }

    static void SetPrewarm(SerializedProperty list, int index, string path, int count)
    {
        var el = list.GetArrayElementAtIndex(index);
        el.FindPropertyRelative("tag").stringValue = System.IO.Path.GetFileNameWithoutExtension(path);
        el.FindPropertyRelative("prefab").objectReferenceValue = Load<GameObject>(path);
        el.FindPropertyRelative("size").intValue = count;
    }

    static void SetObj(Object obj, string field, Object value)
    {
        var so = new SerializedObject(obj);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
}
