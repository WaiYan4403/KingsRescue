using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class SafeAreaHandler : MonoBehaviour
{
    #region Private Fields
    private RectTransform m_RectTransform;
    private Rect m_LastSafeArea = Rect.zero;
    private Vector2Int m_LastScreenSize = Vector2Int.zero;
    private ScreenOrientation m_LastOrientation = ScreenOrientation.AutoRotation;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        m_RectTransform = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        if (m_LastScreenSize.x != Screen.width || 
            m_LastScreenSize.y != Screen.height || 
            m_LastOrientation != Screen.orientation)
        {
            ApplySafeArea();
        }
    }
    #endregion

    #region Private Methods
    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        m_RectTransform.anchorMin = anchorMin;
        m_RectTransform.anchorMax = anchorMax;

        m_LastSafeArea = Screen.safeArea;
        m_LastScreenSize = new Vector2Int(Screen.width, Screen.height);
        m_LastOrientation = Screen.orientation;
    }
    #endregion

    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (m_RectTransform == null)
        {
            m_RectTransform = GetComponent<RectTransform>();
        }
    }
    #endif
} 