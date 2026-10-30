using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace KingsRescue.Story
{
    /// <summary>
    /// Plays each story panel in order, holds it for a set time, then opens the main menu.
    /// Animator clips on these panels loop, so the controller freezes the last frame once
    /// the clip finishes and keeps that pose until the hold time ends.
    /// </summary>
    public class StorySequenceController : MonoBehaviour
    {
        [Serializable]
        public class Beat
        {
            public GameObject root;

            [Tooltip("Seconds this panel stays on screen. 0 uses the Animator clip length.")]
            public float duration;
        }

        [SerializeField] private Beat[] scenes;
        [SerializeField] private string menuSceneName = "MainMenu";
        [SerializeField] private bool skipOnPress;
        [SerializeField] private float skipLockSeconds = 0.35f;
        [SerializeField] private GameObject skipButton;
        [SerializeField] private float skipRevealSeconds = 3f;

        Coroutine play;
        bool left;

        void Start()
        {
            if (skipButton != null)
            {
                skipButton.SetActive(false);
            }

            play = StartCoroutine(Run());
        }

        void OnDisable()
        {
            if (play != null)
            {
                StopCoroutine(play);
            }
        }

        IEnumerator Run()
        {
            if (scenes == null || scenes.Length == 0)
            {
                yield break;
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                if (i == 0)
                {
                    StartCoroutine(RevealSkip());
                }

                ShowOnly(i);

                GameObject root = scenes[i].root;
                Animator animator = root != null ? root.GetComponent<Animator>() : null;
                float clipLength = ClipLength(animator);
                Restart(animator);

                float duration = scenes[i].duration > 0f ? scenes[i].duration : Mathf.Max(clipLength, 0.1f);
                float elapsed = 0f;
                bool held = false;

                while (elapsed < duration)
                {
                    if (left)
                    {
                        yield break;
                    }

                    if (skipOnPress && elapsed >= skipLockSeconds && Pressed())
                    {
                        break;
                    }

                    elapsed += Time.deltaTime;
                    if (!held && animator != null && clipLength > 0.05f && elapsed >= clipLength - 0.02f)
                    {
                        HoldLastFrame(animator);
                        held = true;
                    }

                    yield return null;
                }
            }

            GoToMenu();
        }

        IEnumerator RevealSkip()
        {
            if (skipRevealSeconds > 0f)
            {
                yield return new WaitForSeconds(skipRevealSeconds);
            }

            if (left || skipButton == null)
            {
                yield break;
            }

            skipButton.SetActive(true);
            CanvasGroup group = skipButton.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = skipButton.AddComponent<CanvasGroup>();
            }

            group.alpha = 0f;
            group.DOFade(1f, 0.35f).SetUpdate(true);
        }

        /// <summary>Skip control. Marks the story seen and opens the main menu.</summary>
        public void SkipToMenu()
        {
            GoToMenu();
        }

        void GoToMenu()
        {
            if (left)
            {
                return;
            }

            left = true;
            StoryProgress.MarkSeen();
            if (!string.IsNullOrEmpty(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName);
            }
        }

        void ShowOnly(int index)
        {
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].root != null)
                {
                    scenes[i].root.SetActive(i == index);
                }
            }
        }

        static void Restart(Animator animator)
        {
            if (animator == null)
            {
                return;
            }

            animator.enabled = true;
            animator.speed = 1f;
            animator.Play(0, 0, 0f);
            animator.Update(0f);
        }

        static void HoldLastFrame(Animator animator)
        {
            animator.Play(0, 0, 0.999f);
            animator.Update(0f);
            animator.speed = 0f;
        }

        static float ClipLength(Animator animator)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return 0f;
            }

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            float longest = 0f;
            if (clips == null)
            {
                return 0f;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].length > longest)
                {
                    longest = clips[i].length;
                }
            }

            return longest;
        }

        static bool Pressed()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }

            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                return true;
            }

            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
            {
                return true;
            }

            return false;
        }
    }
}
