using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneHandler : MonoBehaviour
{
    [SerializeField] bool isAutoChangeScene;
    [SerializeField] int nextSceneIndex = 1;
    [SerializeField] float autoChangeTime = 2f;

    void Start()
    {
        if (isAutoChangeScene)
        {
            Invoke("ChangeScene", autoChangeTime);
        }
    }

    void ChangeScene()
    {
        SceneManager.LoadScene(nextSceneIndex);
    }
}
