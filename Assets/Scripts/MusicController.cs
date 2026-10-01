using UnityEngine;
using FMODUnity;

public class MusicController : MonoBehaviour
{
    [Header("FMOD Event Emitters")]
    [SerializeField] private StudioEventEmitter gameEmitter;
    [SerializeField] private StudioEventEmitter menuEmitter;

    // =========================
    // MENU
    // =========================

    public void SetMenuMusic()
    {
        // Stop Game event
        gameEmitter.Stop();

        // Start Menu event if it isn't already playing
        menuEmitter.Play();

        // Set Menu parameter
        menuEmitter.EventInstance.setParameterByNameWithLabel(
            "Menu",
            "Main"
        );

        Debug.Log("Music → Menu / Main");
    }

    public void SetCharactersMusic()
    {
        gameEmitter.Stop();

        menuEmitter.Play();

        menuEmitter.EventInstance.setParameterByNameWithLabel(
            "Menu",
            "Characters"
        );

        Debug.Log("Music → Menu / Characters");
    }

    public void SetOptionsMusic()
    {
        gameEmitter.Stop();

        menuEmitter.Play();

        menuEmitter.EventInstance.setParameterByNameWithLabel(
            "Menu",
            "Options"
        );

        Debug.Log("Music → Menu / Options");
    }


    // =========================
    // GAME
    // =========================

    public void SetGameplayMusic()
    {
        // Stop Menu event
        menuEmitter.Stop();

        // Start Game event
        gameEmitter.Play();

        // Set Game parameter
        gameEmitter.EventInstance.setParameterByNameWithLabel(
            "Game",
            "Play"
        );

        Debug.Log("Music → Game / Play");
    }

    public void SetBossMusic()
    {
        // Stop Menu event
        menuEmitter.Stop();

        // Start Game event
        gameEmitter.Play();

        // Set Game parameter
        gameEmitter.EventInstance.setParameterByNameWithLabel(
            "Game",
            "Boss"
        );

        Debug.Log("Music → Game / Boss");
    }
    public void SetGameOverMusic()
    {
        // Stop Menu event
        menuEmitter.Stop();

        // Start Game event
        gameEmitter.Play();

        // Set Game parameter
        gameEmitter.EventInstance.setParameterByNameWithLabel(
            "Game",
            "Over"
        );

        Debug.Log("Music → Game / GameOver");
    }
}