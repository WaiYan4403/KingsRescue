using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.UI;

/// <summary>
/// Prototype cards and the level-up dimmer were authored with Image.raycastTarget off.
/// Clicks fell through to the virtual joystick. Flip those graphics back on and save.
/// </summary>
public static class FixUpgradeClicks
{
    public static object Run()
    {
        var panel = Object.FindAnyObjectByType<LevelUpPanel>(FindObjectsInactive.Include);
        if (panel == null)
        {
            return "LevelUpPanel not found";
        }

        Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "Fix upgrade card raycasts");

        var log = new StringBuilder();
        var overlay = panel.GetComponent<Image>();
        if (overlay != null)
        {
            overlay.raycastTarget = true;
            EditorUtility.SetDirty(overlay);
            log.Append($"overlay raycast={overlay.raycastTarget}; ");
        }

        var cards = panel.GetComponentsInChildren<UpgradeCardView>(true);
        log.Append($"cards={cards.Length};");
        for (int i = 0; i < cards.Length; i++)
        {
            var image = cards[i].GetComponent<Image>();
            var button = cards[i].GetComponent<Button>();
            if (image != null)
            {
                image.raycastTarget = true;
                if (button != null)
                {
                    button.targetGraphic = image;
                    EditorUtility.SetDirty(button);
                }

                EditorUtility.SetDirty(image);
            }

            log.Append($" [{i}] imageRaycast={(image != null && image.raycastTarget)} button={(button != null)}");
        }

        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        EditorSceneManager.SaveScene(panel.gameObject.scene);
        return log.ToString();
    }
}
