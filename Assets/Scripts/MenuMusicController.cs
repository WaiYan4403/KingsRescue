using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using FMOD.Studio;
using FMODUnity;

public class MenuMusicController : Singleton<MenuMusicController>
{
    const string MenuParameter = "Menu";

    [SerializeField] private StudioEventEmitter musicEmitter;

    string requestedLabel = "Main";
    int requestId;
    Coroutine playRoutine;

    private void Start()
    {
        if (Instance != this)
        {
            return;
        }

        // The emitter's own start can run before this, or the other way around. Either way the
        // event has to be started here: setting the Menu label on an instance that does not
        // exist yet is dropped, which is what happens on the first launch from the logo.
        SetMainMusic();
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
        requestId++;
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (musicEmitter != null && musicEmitter.EventInstance.isValid())
        {
            musicEmitter.AllowFadeout = false;
            musicEmitter.Stop();
        }
    }

    protected override void OnDestroy()
    {
        if (Instance == this)
        {
            StopMusic();
        }

        base.OnDestroy();
    }

    public void SetMainMusic()
    {
        PlayLabel("Main");
    }

    public void SetCharacterMusic()
    {
        PlayLabel("Characters");
    }

    public void SetOptionsMusic()
    {
        PlayLabel("Options");
    }

    void PlayLabel(string label)
    {
        requestedLabel = label;
        if (musicEmitter == null || Instance != this)
        {
            return;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(PlayWhenReady(++requestId));
    }

    IEnumerator PlayWhenReady(int id)
    {
        const int attempts = 90;
        for (int i = 0; i < attempts && id == requestId; i++)
        {
            bool started = false;
            try
            {
                started = TryStart(requestedLabel);
            }
            catch (EventNotFoundException)
            {
                // The logo scene can reach the menu before the music bank is ready.
            }

            if (started)
            {
                playRoutine = null;
                yield break;
            }

            yield return null;
        }

        if (id == requestId)
        {
            playRoutine = null;
        }
    }

    bool TryStart(string label)
    {
        EventDescription description = musicEmitter.EventDescription;
        if (!description.isValid())
        {
            musicEmitter.Play();
            description = musicEmitter.EventDescription;
        }

        if (!description.isValid())
        {
            return false;
        }

        description.loadSampleData();
        description.getSampleLoadingState(out LOADING_STATE loading);
        if (loading == LOADING_STATE.LOADING)
        {
            return false;
        }

        EventInstance instance = musicEmitter.EventInstance;
        if (!IsAudible(instance))
        {
            musicEmitter.Play();
            instance = musicEmitter.EventInstance;
        }

        if (!instance.isValid())
        {
            return false;
        }

        instance.setParameterByNameWithLabel(MenuParameter, label);
        return IsAudible(instance);
    }

    static bool IsAudible(EventInstance instance)
    {
        if (!instance.isValid())
        {
            return false;
        }

        instance.getPlaybackState(out PLAYBACK_STATE state);
        return state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING || state == PLAYBACK_STATE.SUSTAINING;
    }
}