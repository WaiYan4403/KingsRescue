using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingsRescue.UI
{
    /// <summary>
    /// One weapon or passive cell on the results card: an icon and the level reached.
    /// </summary>
    public class ResultItemSlot : MonoBehaviour
    {
        [SerializeField] private GameObject content;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text levelLabel;

        public void Show(Sprite sprite, int level)
        {
            if (content != null)
            {
                content.SetActive(true);
            }

            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.preserveAspect = true;
            }

            if (levelLabel != null)
            {
                levelLabel.text = Mathf.Max(1, level).ToString();
            }
        }

        public void Clear()
        {
            if (content != null)
            {
                content.SetActive(false);
            }

            if (levelLabel != null)
            {
                levelLabel.text = string.Empty;
            }
        }
    }
}
