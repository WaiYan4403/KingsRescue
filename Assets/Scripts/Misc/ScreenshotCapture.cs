#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.InputSystem;
using System.IO;

[ExecuteInEditMode]
public class ScreenshotCapture : MonoBehaviour
{
    [SerializeField] private int superSize = 2; // 2x resolution multiplier for higher quality

    private void Update()
    {
        // ✔ Runtime screenshot (Play mode)
        if (Application.isPlaying)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.leftShiftKey.wasPressedThisFrame))
            {
                TakeScreenshot();
            }
        }
#if UNITY_EDITOR
        // ✔ Editor screenshot (Edit mode)
        if (!Application.isPlaying)
        {
            // Process keyboard events in Scene view
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }
#endif
    }

#if UNITY_EDITOR
    private void OnSceneGUI(SceneView view)
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space)
        {
            TakeScreenshot();
            e.Use(); // Stop Unity from using the key for other things
        }
    }
#endif

    private void TakeScreenshot()
    {
        string folder = Path.Combine(Application.dataPath, "../Screenshots");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        string filename = "Screenshot_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png";
        string path = Path.Combine(folder, filename);

        // Use super-sampling for higher quality (2x = 2x resolution, 4x = 4x resolution)
        int multiplier = Mathf.Clamp(superSize, 1, 8);
        ScreenCapture.CaptureScreenshot(path, multiplier);

        int width = Screen.width * multiplier;
        int height = Screen.height * multiplier;
        Debug.Log($"📷 Screenshot saved to: {path} ({width}x{height} @ {multiplier}x)");
    }
}
