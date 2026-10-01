using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.UI;

public static class FixXpBar
{
    public static object Run()
    {
        var hud = Object.FindAnyObjectByType<HudView>(FindObjectsInactive.Include);
        if (hud == null)
        {
            return "HudView missing";
        }

        var so = new SerializedObject(hud);
        var fillProp = so.FindProperty("xpFill");
        var fill = fillProp != null ? fillProp.objectReferenceValue as Image : null;
        if (fill == null)
        {
            return "xpFill not assigned";
        }

        Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Proto/White.png");
        Undo.RecordObject(fill, "Fix XP bar fill sprite");
        fill.sprite = white;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;
        fill.raycastTarget = false;
        EditorUtility.SetDirty(fill);

        var healthProp = so.FindProperty("healthFill");
        var healthFill = healthProp != null ? healthProp.objectReferenceValue as Image : null;
        if (healthFill != null)
        {
            Undo.RecordObject(healthFill, "Fix health bar fill sprite");
            healthFill.sprite = white;
            healthFill.type = Image.Type.Filled;
            healthFill.fillMethod = Image.FillMethod.Horizontal;
            healthFill.fillOrigin = 0;
            EditorUtility.SetDirty(healthFill);
        }

        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
        EditorSceneManager.SaveScene(hud.gameObject.scene);
        return $"xpFill sprite={(fill.sprite != null ? fill.sprite.name : "null")} type={fill.type} amount={fill.fillAmount}";
    }
}
