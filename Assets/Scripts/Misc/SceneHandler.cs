using UnityEngine;
using UnityEngine.SceneManagement;
using KingsRescue.Story;

public class SceneHandler : MonoBehaviour
{
    [SerializeField] bool isAutoChangeScene;
    [SerializeField] int nextSceneIndex = 1;
    [SerializeField] float autoChangeTime = 2f;

    [Header("First launch")]
    [SerializeField] bool openStoryOnFirstRun;
    [SerializeField] string storySceneName = "Story";
    [SerializeField] string menuSceneName = "MainMenu";

    void Start()
    {
        if (isAutoChangeScene)
        {
            Invoke(nameof(ChangeScene), autoChangeTime);
        }
    }

    void ChangeScene()
    {
        if (openStoryOnFirstRun)
        {
            SceneManager.LoadScene(StoryProgress.HasSeen ? menuSceneName : storySceneName);
            return;
        }

        SceneManager.LoadScene(nextSceneIndex);
    }
}
