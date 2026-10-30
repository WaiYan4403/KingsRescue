using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;

public class GameMusicController : Singleton<GameMusicController>
{
    [SerializeField] private StudioEventEmitter musicEmitter;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this || !isActiveAndEnabled)
        {
            return;
        }
    }

    protected override void OnDestroy()
    {
        StopMusic();
        base.OnDestroy();
    }

    /// <summary>
    /// Cuts the gameplay theme immediately. The emitter does not stop itself when the scene unloads,
    /// so the loop would keep playing under the menu theme.
    /// </summary>
    public void StopMusic()
    {
        if (musicEmitter == null)
        {
            return;
        }

        musicEmitter.AllowFadeout = false;
        musicEmitter.Stop();
    }

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