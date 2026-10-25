using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Core;
using KingsRescue.Enemies;

namespace KingsRescue.UI
{
    /// <summary>
    /// The same overlay bar as the hero, locked above the living boss's head.
    /// </summary>
    public class BossWorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private Vector2 screenPadding = new Vector2(8f, 8f);

        private RectTransform rect;
        private RectTransform canvasRect;
        private Canvas canvas;
        private Camera uiCamera;
        private Camera worldCamera;
        private CanvasGroup visibility;
        private Vector2 lastLocal;
        private float shownFill = -1f;

        private void Awake()
        {
            rect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas = canvas.rootCanvas;
                canvasRect = canvas.transform as RectTransform;
                uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            }

            visibility = GetComponent<CanvasGroup>();
            if (visibility == null)
            {
                visibility = gameObject.AddComponent<CanvasGroup>();
            }

            visibility.interactable = false;
            visibility.blocksRaycasts = false;
            visibility.alpha = 0f;
        }

        /// <summary>Copies the hero bar's frame, fill, and size so both bars match.</summary>
        public void BuildLike(PlayerWorldHealthBar playerBar)
        {
            if (playerBar == null)
            {
                return;
            }

            RectTransform source = playerBar.transform as RectTransform;
            if (source != null && rect != null)
            {
                rect.anchorMin = source.anchorMin;
                rect.anchorMax = source.anchorMax;
                rect.pivot = source.pivot;
                rect.sizeDelta = source.sizeDelta;
                rect.localScale = source.localScale;
            }

            CopyImage(playerBar.GetComponent<Image>(), GetComponent<Image>());

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            for (int i = 0; i < playerBar.transform.childCount; i++)
            {
                Transform child = playerBar.transform.GetChild(i);
                GameObject clone = Instantiate(child.gameObject, transform);
                clone.name = child.name;
            }

            fill = null;
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                images[i].raycastTarget = false;
                if (images[i].type == Image.Type.Filled)
                {
                    fill = images[i];
                }
            }

            if (fill != null)
            {
                fill.fillAmount = 1f;
            }

            shownFill = 1f;
        }

        private void LateUpdate()
        {
            Enemy boss = FindBoss();
            bool show = boss != null && !CoveredByMenu();
            float alpha = show ? 1f : 0f;
            if (visibility != null && visibility.alpha != alpha)
            {
                visibility.alpha = alpha;
            }

            if (!show || rect == null || canvasRect == null)
            {
                shownFill = -1f;
                return;
            }

            ApplyFill(boss.Health != null ? boss.Health.Normalised : 0f);
            Follow(HeadPoint(boss));
        }

        private static Enemy FindBoss()
        {
            for (int i = 0; i < Enemy.Active.Count; i++)
            {
                Enemy enemy = Enemy.Active[i];
                if (enemy != null && enemy.IsBoss && enemy.IsAlive)
                {
                    return enemy;
                }
            }

            return null;
        }

        private static bool CoveredByMenu()
        {
            GameManager game = GameManager.Instance;
            return game == null || game.State != GameState.Playing;
        }

        private void ApplyFill(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (fill == null || Mathf.Abs(amount - shownFill) < 0.001f)
            {
                return;
            }

            shownFill = amount;
            fill.fillAmount = amount;
        }

        private Vector3 HeadPoint(Enemy boss)
        {
            SpriteRenderer renderer = boss.Body;
            if (renderer == null || renderer.sprite == null)
            {
                renderer = LargestBody(boss);
            }

            float height = boss.Definition != null ? boss.Definition.healthBarHeight : 1.15f;
            float x = boss.transform.position.x;
            if (renderer != null && renderer.enabled && renderer.sprite != null)
            {
                x = renderer.bounds.center.x;
            }

            return new Vector3(x, boss.transform.position.y + height, boss.transform.position.z);
        }

        private static SpriteRenderer LargestBody(Enemy boss)
        {
            if (boss.Body != null && boss.Body.enabled && boss.Body.sprite != null)
            {
                return boss.Body;
            }

            SpriteRenderer[] renderers = boss.GetComponentsInChildren<SpriteRenderer>();
            SpriteRenderer best = null;
            float bestHeight = 0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || !renderer.enabled || renderer.sprite == null)
                {
                    continue;
                }

                float height = renderer.bounds.size.y;
                if (height > bestHeight)
                {
                    best = renderer;
                    bestHeight = height;
                }
            }

            return best;
        }

        private void Follow(Vector3 world)
        {
            if (worldCamera == null)
            {
                worldCamera = uiCamera != null ? uiCamera : Camera.main;
            }

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(worldCamera, world);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out Vector2 local))
            {
                return;
            }

            Vector2 half = rect.sizeDelta * 0.5f;
            Vector2 canvasHalf = canvasRect.rect.size * 0.5f;
            local.x = Mathf.Clamp(local.x, -canvasHalf.x + half.x + screenPadding.x, canvasHalf.x - half.x - screenPadding.x);
            local.y = Mathf.Clamp(local.y, -canvasHalf.y + half.y + screenPadding.y, canvasHalf.y - half.y - screenPadding.y);
            if ((local - lastLocal).sqrMagnitude < 0.25f)
            {
                return;
            }

            lastLocal = local;
            rect.anchoredPosition = local;
        }

        private static void CopyImage(Image from, Image to)
        {
            if (from == null || to == null)
            {
                return;
            }

            to.sprite = from.sprite;
            to.type = from.type;
            to.color = from.color;
            to.material = from.material;
            to.preserveAspect = from.preserveAspect;
            to.fillCenter = from.fillCenter;
            to.fillMethod = from.fillMethod;
            to.fillOrigin = from.fillOrigin;
            to.fillAmount = from.fillAmount;
            to.pixelsPerUnitMultiplier = from.pixelsPerUnitMultiplier;
            to.raycastTarget = false;
        }
    }
}
