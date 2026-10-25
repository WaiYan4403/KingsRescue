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
        static readonly float[] nextAllowed = new float[32];
        static readonly HashSet<int> boundButtons = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ListenForScenes()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            boundButtons.Clear();
            BindButtonsInScene();
        }

        public static void Play(SoundId id)
        {
            if (id == SoundId.None)
            {
                return;
            }

            SoundBank source = Bank;
            if (source == null || !source.TryGet(id, out SoundSlot slot) || slot.clip == null)
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

            AudioSource voice = NextVoice();
            float min = Mathf.Min(slot.pitchMin, slot.pitchMax);
            float max = Mathf.Max(slot.pitchMin, slot.pitchMax);
            voice.pitch = Mathf.Approximately(min, max) ? min : Random.Range(min, max);
            voice.PlayOneShot(slot.clip, Mathf.Clamp01(slot.volume));
        }

        public static void PlayWeapon(WeaponFireMode mode)
        {
            switch (mode)
            {
                case WeaponFireMode.MeleeSwing:
                    Play(SoundId.PlayerAttack);
                    break;
                case WeaponFireMode.ForwardShot:
                case WeaponFireMode.AimedShot:
                case WeaponFireMode.RandomShot:
                case WeaponFireMode.Boomerang:
                case WeaponFireMode.Orbit:
                    Play(SoundId.PlayerShoot);
                    break;
                case WeaponFireMode.RandomStrike:
                case WeaponFireMode.BoltStrike:
                    Play(SoundId.SpellStrike);
                    break;
                case WeaponFireMode.GroundZone:
                case WeaponFireMode.FreezeField:
                case WeaponFireMode.Aura:
                    Play(SoundId.SpellCast);
                    break;
            }
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

        static void BindButtonsInScene()
        {
            Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
            for (int i = 0; i < buttons.Length; i++)
            {
                BindButton(buttons[i]);
            }
        }

        /// <summary>Buttons created after the scene loads, such as level-up cards.</summary>
        public static void BindButton(Button button)
        {
            if (button == null || !boundButtons.Add(button.GetInstanceID()))
            {
                return;
            }

            button.onClick.AddListener(PlayClick);
        }

        static void PlayClick()
        {
            Play(SoundId.UiClick);
        }
    }
}
