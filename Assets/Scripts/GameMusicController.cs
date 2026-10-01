using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;

public class GameMusicController : MonoBehaviour
{
    [SerializeField] private StudioEventEmitter musicEmitter;

    private void Update()
    {
        // Press G to test Gameplay music
        if (Keyboard.current.gKey.wasPressedThisFrame)
        {
            SetGameplayMusic();
        }

        // Press B to test Boss music
        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            SetBossMusic();
        }

        // Press O to test Game Over music
        if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            SetGameOverMusic();
        }
    }

    public void SetGameplayMusic()
    {
        musicEmitter.EventInstance.setParameterByNameWithLabel(
            "Game",
            "Play"
        );

        Debug.Log("Music changed to Play");
    }

    public void SetBossMusic()
    {
        musicEmitter.EventInstance.setParameterByNameWithLabel(
            "Game",
            "Boss"
        );

        Debug.Log("Music changed to Boss");
    }
    public void SetGameOverMusic()
    {
        musicEmitter.EventInstance.setParameterByNameWithLabel(
            "Game",
            "Over"
        );

        Debug.Log("Music changed to Game Over");
    }
}