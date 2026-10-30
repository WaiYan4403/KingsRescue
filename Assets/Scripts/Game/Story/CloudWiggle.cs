using DG.Tweening;
using UnityEngine;

namespace KingsRescue.Story
{
    /// <summary>
    /// Slow drift and tilt so a cloud reads as moving air. Rest pose is restored when disabled.
    /// </summary>
    public class CloudWiggle : MonoBehaviour
    {
        [SerializeField] private float movePixels = 18f;
        [SerializeField] private float cycleSeconds = 2.4f;
        [SerializeField] private float tiltDegrees = 2.5f;
        [SerializeField] private float phase;

        RectTransform rect;
        Vector2 rest;
        Sequence motion;

        void Awake()
        {
            rect = transform as RectTransform;
            if (rect != null)
            {
                rest = rect.anchoredPosition;
            }
        }

        void OnEnable()
        {
            if (rect == null)
            {
                return;
            }

            rect.anchoredPosition = rest;
            rect.localRotation = Quaternion.identity;
            motion?.Kill();

            float duration = Mathf.Max(0.4f, cycleSeconds);
            motion = DOTween.Sequence()
                .Append(rect.DOAnchorPos(rest + new Vector2(movePixels * 0.35f, movePixels), duration).SetEase(Ease.InOutSine))
                .Join(rect.DOLocalRotate(new Vector3(0f, 0f, tiltDegrees), duration).SetEase(Ease.InOutSine))
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(Mathf.Max(0f, phase))
                .SetLink(gameObject);
        }

        void OnDisable()
        {
            motion?.Kill();
            motion = null;
            if (rect == null)
            {
                return;
            }

            rect.anchoredPosition = rest;
            rect.localRotation = Quaternion.identity;
        }
    }
}
