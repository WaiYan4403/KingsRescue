using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;

public class MenuMusicController : Singleton<MenuMusicController>
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

        // Press M to test Main menu music
        if (keyboard.mKey.wasPressedThisFrame)
        {
            SetMainMusic();
        }

        // Press C to test Characters music
        if (keyboard.cKey.wasPressedThisFrame)
        {
            SetCharacterMusic();
        }

        // Press O to test Options music
        if (keyboard.oKey.wasPressedThisFrame)
        {
            SetOptionsMusic();
        }
    }
#endif

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