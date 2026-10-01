using UnityEditor;
using UnityEngine;
using KingsRescue.Data;

public static class TuneEnemyFollow
{
    public static object Run()
    {
        Set("Assets/Data/Enemies/Enemy_Goblin.asset", 1f, 0.35f);
        Set("Assets/Data/Enemies/Enemy_Orc.asset", 1f, 0.5f);
        Set("Assets/Data/Enemies/Enemy_Skelly.asset", 1f, 0.6f);
        AssetDatabase.SaveAssets();
        return "all follow; give-up when below player: goblin 35% orc 50% skelly 60%";
    }

    static void Set(string path, float follow, float giveUp)
    {
        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
        if (def == null)
        {
            return;
        }

        Undo.RecordObject(def, "Tune enemy give-up chance");
        def.followChance = follow;
        def.giveUpWhenPassedChance = giveUp;
        EditorUtility.SetDirty(def);
    }
}
