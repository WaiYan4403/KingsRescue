using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;

public class MenuMusicController : Singleton<MenuMusicController>
{
    [SerializeField] private StudioEventEmitter musicEmitter;

    private void Update()
    {
        // Press G to test Gameplay music
        if (Keyboard.current.mKey.wasPressedThisFrame)
        {
            SetMainMusic();
        }

        // Press B to test Boss music
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            SetCharacterMusic();
        }

        // Press O to test Game Over music
        if (Keyboard.current.oKey.wasPressedThisFrame)
        {
            SetOptionsMusic();
        }
    }

    public void StopMusic()
    {
        musicEmitter.EventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        Debug.Log("Music stopped");
    }

    public void SetMainMusic()
    {
        musicEmitter.EventInstance.setParameterByNameWithLabel(
            "Menu",
            "Main"
        );

        Debug.Log("Music changed to Main");
    }

    public void SetCharacterMusic()
    {
        musicEmitter.EventInstance.setParameterByNameWithLabel(
            "Menu",
            "Characters"
        );

        Debug.Log("Music changed to Characters");
    }
    public void SetOptionsMusic()
    {
        musicEmitter.EventInstance.setParameterByNameWithLabel(
            "Menu",
            "Options"
        );

        Debug.Log("Music changed to Options");
    }
}