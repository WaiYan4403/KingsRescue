using UnityEditor;
using UnityEngine;
using KingsRescue.Data;

public static class TuneEnemySeparation
{
    public static object Run()
    {
        Set("Assets/Data/Enemies/Enemy_Goblin.asset", 0.7f, 3.5f);
        Set("Assets/Data/Enemies/Enemy_Orc.asset", 0.9f, 3.25f);
        Set("Assets/Data/Enemies/Enemy_Skelly.asset", 1.05f, 2.8f);
        AssetDatabase.SaveAssets();
        return "goblin 0.7/3.5 orc 0.9/3.25 skelly 1.05/2.8";
    }

    static void Set(string path, float radius, float strength)
    {
        var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
        if (def == null)
        {
            return;
        }

        Undo.RecordObject(def, "Tune enemy separation");
        def.separationRadius = radius;
        def.separationStrength = strength;
        EditorUtility.SetDirty(def);
    }
}
