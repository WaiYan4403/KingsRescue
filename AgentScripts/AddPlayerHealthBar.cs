using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Player;
using KingsRescue.UI;

public static class AddPlayerHealthBar
{
    public static object Run()
    {
        var existing = Object.FindAnyObjectByType<PlayerWorldHealthBar>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.transform.SetSiblingIndex(GetInsertIndex(existing.transform.parent));
            HideHudHealthBar();
            EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
            EditorSceneManager.SaveScene(existing.gameObject.scene);
            return "PlayerHealthBar already present; HUD HealthBar hidden";
        }

        var canvas = Object.FindAnyObjectByType<Canvas>();
        var playerHealth = Object.FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
        if (canvas == null || playerHealth == null)
        {
            return $"Missing refs canvas={canvas != null} player={playerHealth != null}";
        }

        Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Proto/White.png");
        if (white == null)
        {
            var textures = AssetDatabase.FindAssets("t:Sprite White", new[] { "Assets/Art" });
            if (textures.Length > 0)
            {
                white = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(textures[0]));
            }
        }

        var rootGo = new GameObject("PlayerHealthBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var root = rootGo.GetComponent<RectTransform>();
        root.SetParent(canvas.transform, false);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(132f, 22f);

        var frame = rootGo.GetComponent<Image>();
        frame.sprite = white;
        frame.color = Color.black;
        frame.raycastTarget = false;

        var bgRt = MakeImage("Background", root, white, new Color(0.08f, 0.02f, 0.02f, 1f), new Vector2(126f, 16f));
        var fillRt = MakeImage("Fill", bgRt, white, new Color(0.22f, 0.86f, 0.32f, 1f), new Vector2(126f, 16f));
        var fill = fillRt.GetComponent<Image>();
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;

        var bar = rootGo.AddComponent<PlayerWorldHealthBar>();
        var so = new SerializedObject(bar);
        so.FindProperty("health").objectReferenceValue = playerHealth;
        so.FindProperty("followRenderer").objectReferenceValue = playerHealth.GetComponentInChildren<SpriteRenderer>();
        so.FindProperty("fill").objectReferenceValue = fill;
        so.ApplyModifiedPropertiesWithoutUndo();

        root.SetSiblingIndex(GetInsertIndex(canvas.transform));
        HideHudHealthBar();

        Undo.RegisterCreatedObjectUndo(rootGo, "Add player world health bar");
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        return $"created PlayerHealthBar sibling={root.GetSiblingIndex()}";
    }

    static int GetInsertIndex(Transform canvas)
    {
        int levelUp = IndexOf(canvas, "LevelUpPanel");
        int joystick = IndexOf(canvas, "Joystick");
        if (levelUp >= 0)
        {
            return levelUp;
        }

        return joystick >= 0 ? joystick + 1 : canvas.childCount;
    }

    static int IndexOf(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            if (parent.GetChild(i).name == name)
            {
                return i;
            }
        }

        return -1;
    }

    static void HideHudHealthBar()
    {
        var hudBar = GameObject.Find("HealthBar");
        if (hudBar != null && hudBar.transform.parent != null && hudBar.transform.parent.name == "HUD")
        {
            hudBar.SetActive(false);
            EditorUtility.SetDirty(hudBar);
        }
    }

    static RectTransform MakeImage(string name, Transform parent, Sprite sprite, Color color, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return rt;
    }
}
