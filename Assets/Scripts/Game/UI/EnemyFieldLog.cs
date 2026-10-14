using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Core;
using KingsRescue.Enemies;
using KingsRescue.Spawning;

namespace KingsRescue.UI
{
    /// <summary>
    /// On-screen roster of what is on the field and how many enemies are inside the camera view.
    /// Turn <see cref="ShowWindow"/> off and the panel is hidden and nothing is computed.
    /// </summary>
    public class EnemyFieldLog : MonoBehaviour
    {
        [Tooltip("Debug window on or off. While off the roster is not built and the panel is hidden.")]
        [SerializeField] private bool showWindow = true;
        [SerializeField] private TMP_Text label;
        [SerializeField] private float refreshInterval = 0.4f;
        [SerializeField] private float fontSize = 28f;
        [SerializeField] private Vector2 panelSize = new Vector2(620f, 380f);

        private GameObject panelRoot;
        private bool windowApplied;
        private float nextRefresh;
        private int onScreenCount;
        private string lastFingerprint;
        private readonly StringBuilder builder = new StringBuilder(512);
        private readonly List<Row> rows = new List<Row>(32);

        private struct Row
        {
            public string name;
            public string action;
            public int count;
            public bool boss;
            public float hp;
        }

        /// <summary>Debug window switch. Safe to flip at runtime.</summary>
        public bool ShowWindow
        {
            get => showWindow;
            set
            {
                showWindow = value;
                ApplyVisibility();
            }
        }

        private void Awake()
        {
            if (label == null)
            {
                BuildOverlay();
            }
            else if (label.gameObject != gameObject)
            {
                // Never deactivate this object, or Update could not switch the window back on.
                panelRoot = label.gameObject;
            }

            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            windowApplied = showWindow;
            if (panelRoot != null && panelRoot.activeSelf != showWindow)
            {
                panelRoot.SetActive(showWindow);
            }
            else if (panelRoot == null && label != null)
            {
                label.enabled = showWindow;
            }

            if (showWindow)
            {
                lastFingerprint = null;
                nextRefresh = 0f;
            }
        }

        private void Update()
        {
            if (showWindow != windowApplied)
            {
                ApplyVisibility();
            }

            if (!showWindow || Time.unscaledTime < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.unscaledTime + Mathf.Max(0.15f, refreshInterval);
            Refresh();
        }

        private void Refresh()
        {
            rows.Clear();
            onScreenCount = 0;
            ArenaBounds arena = ArenaBounds.Instance;
            Rect view = arena != null ? arena.ScreenRect : default;
            List<Enemy> active = Enemy.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Enemy enemy = active[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                if (arena != null && view.Contains(enemy.Position))
                {
                    onScreenCount++;
                }

                string name = enemy.DisplayName;
                string action = enemy.DescribeActivity();
                bool boss = enemy.IsBoss;
                float hp = enemy.Health != null ? enemy.Health.Normalised : 0f;

                int found = -1;
                for (int r = 0; r < rows.Count; r++)
                {
                    if (rows[r].name == name && rows[r].action == action)
                    {
                        found = r;
                        break;
                    }
                }

                if (found >= 0)
                {
                    Row row = rows[found];
                    row.count++;
                    rows[found] = row;
                }
                else
                {
                    rows.Add(new Row
                    {
                        name = name,
                        action = action,
                        count = 1,
                        boss = boss,
                        hp = hp
                    });
                }
            }

            rows.Sort(CompareRows);

            builder.Length = 0;
            GameManager game = GameManager.Instance;
            if (game != null && game.BossFightActive)
            {
                builder.AppendLine("BOSS FIGHT");
            }

            if (!string.IsNullOrEmpty(WaveDirector.LastUnlockAnnouncement)
                && Time.unscaledTime < WaveDirector.LastUnlockAnnounceUntil)
            {
                builder.Append("NEW: ").AppendLine(WaveDirector.LastUnlockAnnouncement);
            }

            builder.Append("On screen: ").Append(onScreenCount).Append("   On field: ").Append(active.Count).AppendLine();
            if (rows.Count == 0)
            {
                builder.Append("Field: empty");
            }
            else
            {
                int shown = 0;
                for (int i = 0; i < rows.Count && shown < 12; i++)
                {
                    Row row = rows[i];
                    builder.Append(row.boss ? "> " : "- ");
                    builder.Append(row.count).Append(" ").Append(row.name);
                    if (row.boss)
                    {
                        builder.Append(" ").Append(Mathf.CeilToInt(row.hp * 100f)).Append("%");
                    }

                    builder.Append("  ").Append(row.action);
                    builder.AppendLine();
                    shown++;
                }

                if (rows.Count > 12)
                {
                    builder.Append("+").Append(rows.Count - 12).Append(" more groups");
                }
            }

            string report = builder.ToString();
            string fingerprint = BuildFingerprint();
            if (fingerprint == lastFingerprint)
            {
                return;
            }

            lastFingerprint = fingerprint;
            if (label != null)
            {
                label.text = report;
            }
        }

        private string BuildFingerprint()
        {
            builder.Length = 0;
            builder.Append(onScreenCount).Append('|');
            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                builder.Append(row.name).Append(':').Append(row.count);
                if (row.boss)
                {
                    builder.Append('/').Append(row.action);
                    builder.Append('/').Append(Mathf.CeilToInt(row.hp * 100f));
                }

                builder.Append(';');
            }

            return builder.ToString();
        }

        private static int CompareRows(Row a, Row b)
        {
            if (a.boss != b.boss)
            {
                return a.boss ? -1 : 1;
            }

            int byCount = b.count.CompareTo(a.count);
            return byCount != 0 ? byCount : string.CompareOrdinal(a.name, b.name);
        }

        private void BuildOverlay()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;
            if (canvas == null)
            {
                // No HUD canvas above this object: own a small overlay so no scene search is needed.
                var host = new GameObject("EnemyFieldLogCanvas");
                host.transform.SetParent(transform, false);
                Canvas own = host.AddComponent<Canvas>();
                own.renderMode = RenderMode.ScreenSpaceOverlay;
                own.sortingOrder = 400;
                parent = host.transform;
            }

            GameObject panel = new GameObject("EnemyFieldLogPanel");
            panelRoot = panel;
            panel.transform.SetParent(parent, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(0f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(14f, 14f);
            panelRect.sizeDelta = panelSize.sqrMagnitude > 1f ? panelSize : new Vector2(620f, 380f);

            Image background = panel.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);
            background.raycastTarget = false;

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(panel.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 8f);
            textRect.offsetMax = new Vector2(-10f, -8f);

            TMP_Text text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize > 8f ? fontSize : 28f;
            text.color = new Color(0.92f, 0.95f, 0.85f, 1f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            text.text = "Field: …";
            label = text;
        }
    }
}
