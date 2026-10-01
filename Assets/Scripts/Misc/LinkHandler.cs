using UnityEngine;

public class LinkHandler: MonoBehaviour
{
    [SerializeField] string url = "";

    public void OpenLink()
    {
        Application.OpenURL(url);
    }
}