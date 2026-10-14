using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;

public class GameMusicController : Singleton<GameMusicController>
{
    [SerializeField] private StudioEventEmitter musicEmitter;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Debug keys only exist in the editor and development builds, so release builds skip the per-frame poll.
    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        // Press G to test Gameplay music
        if (keyboard.gKey.wasPressedThisFrame)
        {
            SetGameplayMusic();
        }

        // Press B to test Boss music
        if (keyboard.bKey.wasPressedThisFrame)
        {
            SetBossMusic();
        }

        // Press O to test Game Over music
        if (keyboard.oKey.wasPressedThisFrame)
        {
            SetGameOverMusic();
        }
    }
#endif

    public void StopMusic()
    {
        musicEmitter.EventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        Debug.Log("Music stopped");
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