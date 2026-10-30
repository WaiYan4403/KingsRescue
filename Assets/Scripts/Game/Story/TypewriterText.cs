using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using KingsRescue.Audio;

namespace KingsRescue.Story
{
    /// <summary>
    /// Types the current TMP string when this object is enabled (including when a story
    /// Animator turns the line on).
    /// </summary>
    public class TypewriterText : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private float secondsPerCharacter = 0.03f;
        [SerializeField] private float minDuration = 0.35f;

        Coroutine routine;
        Tween tween;

        void Awake()
        {
            if (label == null)
            {
                label = GetComponent<TMP_Text>();
            }
        }

        void OnEnable()
        {
            if (label == null)
            {
                return;
            }

            label.maxVisibleCharacters = 0;
            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(Type());
        }

        void OnDisable()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            tween?.Kill();
            tween = null;

            if (label != null)
            {
                label.maxVisibleCharacters = int.MaxValue;
            }
        }

        IEnumerator Type()
        {
            yield return null;
            if (!isActiveAndEnabled || label == null)
            {
                yield break;
            }

            label.ForceMeshUpdate();
            int count = label.textInfo.characterCount;
            if (count <= 0)
            {
                label.maxVisibleCharacters = int.MaxValue;
                yield break;
            }

            float duration = Mathf.Max(minDuration, count * secondsPerCharacter);
            int shown = 0;
            label.maxVisibleCharacters = 0;
            tween?.Kill();
            tween = DOTween.To(() => shown, value =>
                {
                    int next = value;
                    if (next > shown)
                    {
                        PlayClicks(shown, next);
                    }

                    shown = next;
                    label.maxVisibleCharacters = next;
                }, count, duration)
                .SetEase(Ease.Linear)
                .SetLink(gameObject);
        }

        void PlayClicks(int from, int to)
        {
            TMP_TextInfo info = label.textInfo;
            int last = Mathf.Min(to, info.characterCount);
            for (int i = from; i < last; i++)
            {
                char character = info.characterInfo[i].character;
                if (char.IsWhiteSpace(character))
                {
                    continue;
                }

                SoundEffects.Play(SoundId.Typewriter);
            }
        }
    }
}
