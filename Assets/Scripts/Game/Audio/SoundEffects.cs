using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KingsRescue.Data;

namespace KingsRescue.Audio
{
    /// <summary>
    /// Plays sound effects through Unity <see cref="AudioSource"/>s.
    /// FMOD keeps the music on its own bus. The Sound setting is
    /// <see cref="AudioListener.volume"/>, so these sources follow that slider
    /// and do not change the soundtrack.
    /// </summary>
    public static class SoundEffects
    {
        const int PoolSize = 8;

        static SoundBank bank;
        static bool bankSearched;
        static AudioSource[] pool;
        static int cursor;
        static readonly float[] nextAllowed = new float[178];
        static AudioSource loopVoice;
        static SoundId loopingId = SoundId.None;
        static readonly Dictionary<int, int> loopCounts = new Dictionary<int, int>();
        static readonly HashSet<int> boundButtons = new HashSet<int>();
        static readonly HashSet<int> boundSliders = new HashSet<int>();
        static readonly Dictionary<int, SoundId> buttonSounds = new Dictionary<int, SoundId>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ListenForScenes()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            boundButtons.Clear();
            boundSliders.Clear();
            BindButtonsInScene();
            BindSlidersInScene();
        }

        /// <summary>
        /// Buttons wired in code, such as reset and buy upgrade, use this instead of the generic click.
        /// Call it from Awake so it is in place before the scene bind runs.
        /// </summary>
        public static void UseButtonSound(Button button, SoundId id)
        {
            if (button == null || id == SoundId.None)
            {
                return;
            }

            buttonSounds[button.GetInstanceID()] = id;
        }

        public static void Use(SoundBank assigned)
        {
            if (assigned == null)
            {
                return;
            }

            bank = assigned;
            bankSearched = true;
        }

        public static void Play(SoundId id)
        {
            if (id == SoundId.None)
            {
                return;
            }

            SoundBank source = Bank;
            if (source == null || !source.TryGet(id, out SoundSlot slot))
            {
                return;
            }

            AudioClip clip = PickClip(slot);
            if (clip == null)
            {
                return;
            }

            int index = (int)id;
            if (index >= 0 && index < nextAllowed.Length)
            {
                if (Time.unscaledTime < nextAllowed[index])
                {
                    return;
                }

                nextAllowed[index] = Time.unscaledTime + Mathf.Max(0f, slot.minInterval);
            }

            if (clip.loadState == AudioDataLoadState.Unloaded)
            {
                clip.LoadAudioData();
            }

            AudioSource voice = NextVoice();
            float min = Mathf.Min(slot.pitchMin, slot.pitchMax);
            float max = Mathf.Max(slot.pitchMin, slot.pitchMax);
            voice.pitch = Mathf.Approximately(min, max) ? min : Random.Range(min, max);
            voice.PlayOneShot(clip, Mathf.Clamp01(slot.volume));
        }

        static int typewriterStep;

        static AudioClip PickClip(SoundSlot slot)
        {
            int extra = slot.variants != null ? slot.variants.Length : 0;
            if (extra == 0)
            {
                return slot.clip;
            }

            bool hasMain = slot.clip != null;
            int count = extra + (hasMain ? 1 : 0);
            for (int n = 0; n < count; n++)
            {
                int pick = (typewriterStep + n) % count;
                AudioClip clip = hasMain
                    ? (pick == 0 ? slot.clip : slot.variants[pick - 1])
                    : slot.variants[pick];
                if (clip == null)
                {
                    continue;
                }

                typewriterStep = (pick + 1) % count;
                return clip;
            }

            return slot.clip;
        }

        /// <summary>
        /// Plays a clip on its own source and repeats it until every matching <see cref="StopLoop"/> has run.
        /// Two starts need two stops, so overlapping saws keep one loop until the last one is gone.
        /// An empty clip stays silent. This source is not the one-shot pool, so a later effect cannot cut it off.
        /// </summary>
        public static void PlayLoop(SoundId id)
        {
            if (id == SoundId.None)
            {
                return;
            }

            SoundBank source = Bank;
            if (source == null || !source.TryGet(id, out SoundSlot slot) || slot.clip == null)
            {
                StopLoop(id);
                return;
            }

            int key = (int)id;
            loopCounts.TryGetValue(key, out int count);
            loopCounts[key] = count + 1;

            AudioSource voice = LoopVoice();
            if (count > 0 && loopingId == id && voice.isPlaying && voice.clip == slot.clip)
            {
                return;
            }

            float min = Mathf.Min(slot.pitchMin, slot.pitchMax);
            float max = Mathf.Max(slot.pitchMin, slot.pitchMax);
            voice.pitch = Mathf.Approximately(min, max) ? min : Random.Range(min, max);
            voice.volume = Mathf.Clamp01(slot.volume);
            voice.clip = slot.clip;
            voice.loop = true;
            voice.Play();
            loopingId = id;
        }

        public static void StopLoop(SoundId id)
        {
            int key = (int)id;
            if (loopCounts.TryGetValue(key, out int count) && count > 0)
            {
                count--;
                if (count > 0)
                {
                    loopCounts[key] = count;
                    return;
                }

                loopCounts.Remove(key);
            }

            if (loopVoice == null || loopingId != id)
            {
                return;
            }

            loopVoice.Stop();
            loopVoice.clip = null;
            loopVoice.loop = false;
            loopingId = SoundId.None;
        }

        public static void PlayEnemy(EnemyDefinition definition, EnemyCue cue)
        {
            EnemyVoice voice = definition != null ? definition.voice : EnemyVoice.Generic;
            Play(EnemySounds.Resolve(voice, cue));
        }

        public static void PlayWeapon(WeaponDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            Play(WeaponSounds.Resolve(definition.attackSound));
        }

        static SoundBank Bank
        {
            get
            {
                if (!bankSearched)
                {
                    bankSearched = true;
                    bank = Resources.Load<SoundBank>("SoundBank");
                }

                return bank;
            }
        }

        static AudioSource NextVoice()
        {
            if (pool == null)
            {
                GameObject host = new GameObject("Sound Effects");
                Object.DontDestroyOnLoad(host);
                if (Object.FindAnyObjectByType<AudioListener>() == null)
                {
                    host.AddComponent<AudioListener>();
                }

                pool = new AudioSource[PoolSize];
                for (int i = 0; i < pool.Length; i++)
                {
                    AudioSource voice = host.AddComponent<AudioSource>();
                    voice.playOnAwake = false;
                    voice.spatialBlend = 0f;
                    voice.loop = false;
                    pool[i] = voice;
                }
            }

            AudioSource next = pool[cursor];
            cursor = (cursor + 1) % pool.Length;
            return next;
        }

        static AudioSource LoopVoice()
        {
            if (loopVoice == null)
            {
                GameObject host = new GameObject("Sound Effects Loop");
                Object.DontDestroyOnLoad(host);
                loopVoice = host.AddComponent<AudioSource>();
                loopVoice.playOnAwake = false;
                loopVoice.spatialBlend = 0f;
                loopVoice.loop = false;
            }

            return loopVoice;
        }

        static void BindButtonsInScene()
        {
            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
            for (int i = 0; i < buttons.Length; i++)
            {
                BindButton(buttons[i]);
            }
        }

        static void BindSlidersInScene()
        {
            Slider[] sliders = Object.FindObjectsByType<Slider>(FindObjectsInactive.Include);
            for (int i = 0; i < sliders.Length; i++)
            {
                BindSlider(sliders[i]);
            }
        }

        /// <summary>Buttons created after the scene loads, such as level-up cards.</summary>
        public static void BindButton(Button button)
        {
            if (button == null || !boundButtons.Add(button.GetInstanceID()))
            {
                return;
            }

            if (MethodPlaysItsOwnSound(button))
            {
                return;
            }

            SoundId id = SoundForButton(button);
            button.onClick.AddListener(() => Play(id));
        }

        static void BindSlider(Slider slider)
        {
            if (slider == null || !slider.interactable || !boundSliders.Add(slider.GetInstanceID()))
            {
                return;
            }

            if (slider.GetComponent<Poker.UI.ToggleSwitch.ToggleSwitch>() != null)
            {
                return;
            }

            slider.onValueChanged.AddListener(PlaySlider);
        }

        static void PlaySlider(float value)
        {
            Play(SoundId.UiSlider);
        }

        static SoundId SoundForButton(Button button)
        {
            if (buttonSounds.TryGetValue(button.GetInstanceID(), out SoundId chosen))
            {
                return chosen;
            }

            int count = button.onClick.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                switch (button.onClick.GetPersistentMethodName(i))
                {
                    case "SelectNextCharacter":
                    case "SelectPreviousCharacter":
                        return SoundId.UiBrowse;
                    case "BuySelectedCharacter":
                    case "BuySelectedUpgrade":
                        return SoundId.UiBuy;
                    case "ResetSelectedUpgrades":
                        return SoundId.UiReset;
                }
            }

            if (button.gameObject.name.IndexOf("back", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SoundId.UiBack;
            }

            return SoundId.UiClick;
        }

        static bool MethodPlaysItsOwnSound(Button button)
        {
            int count = button.onClick.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                switch (button.onClick.GetPersistentMethodName(i))
                {
                    case "GoBack":
                    case "HideSettings":
                    case "CloseQuitConfirm":
                        return true;
                }
            }

            return false;
        }
    }
}
