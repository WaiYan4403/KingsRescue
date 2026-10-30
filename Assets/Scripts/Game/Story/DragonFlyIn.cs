using DG.Tweening;
using UnityEngine;

namespace KingsRescue.Story
{
    /// <summary>
    /// Slides the dragon in from off-screen, then keeps a small hover so it still reads as flying.
    /// Starts when this object (or its parent panel) becomes active.
    /// </summary>
    public class DragonFlyIn : MonoBehaviour
    {
        [SerializeField] private Vector2 fromOffset = new Vector2(1100f, 160f);
        [SerializeField] private float flySeconds = 1.15f;
        [SerializeField] private float bobPixels = 26f;
        [SerializeField] private float bobSeconds = 1.05f;
        [SerializeField] private float tiltDegrees = -8f;

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

            motion?.Kill();
            rect.anchoredPosition = rest + fromOffset;
            rect.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);

            float fly = Mathf.Max(0.2f, flySeconds);
            float bob = Mathf.Max(0.3f, bobSeconds);
            motion = DOTween.Sequence().SetLink(gameObject);
            motion.Append(rect.DOAnchorPos(rest, fly).SetEase(Ease.OutCubic));
            motion.Join(rect.DOLocalRotate(Vector3.zero, fly).SetEase(Ease.OutSine));
            motion.Append(rect.DOAnchorPosY(rest.y + bobPixels, bob).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
            motion.Join(rect.DOLocalRotate(new Vector3(0f, 0f, 3.5f), bob).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
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
