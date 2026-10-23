using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// iPhone Safari zooms in on a text field whose font is under 16px and keeps that zoom
/// after the keyboard closes. This locks the page scale back to 1 without reloading.
/// </summary>
public class WebPageReload : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void InstallMobileInputFix();

    [DllImport("__Internal")]
    private static extern void ResetPageZoom();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        InstallMobileInputFix();
#endif
    }

    public void ReloadWebPage()
    {
        ResetZoom();
    }

    public static void ResetZoom()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ResetPageZoom();
#endif
    }
}
