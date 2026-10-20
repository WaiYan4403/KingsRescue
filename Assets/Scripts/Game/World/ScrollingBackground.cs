using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.World
{
    /// <summary>
    /// Spawns stacked sprite background prefabs (ground, road, trees, props) and
    /// recycles the bottom one to the top so the road never runs out.
    /// </summary>
    public class ScrollingBackground : MonoBehaviour
    {
        [SerializeField] private BackgroundSegment segmentPrefab;

        [Tooltip("How many tiles are kept alive. 3 covers a typical portrait view plus one ahead.")]
        [SerializeField] private int segmentCount = 3;

        [Tooltip("1 = matches the world scroll exactly. Use smaller values for distant parallax layers.")]
        [SerializeField, Range(0f, 2f)] private float parallaxFactor = 1f;

        [Tooltip("Optional scene preview of the same prefab. Hidden at runtime so only spawned tiles scroll.")]
        [SerializeField] private GameObject scenePreview;

        private readonly List<BackgroundSegment> segments = new List<BackgroundSegment>();
        private Transform root;

        private void Start()
        {
            StartCoroutine(BuildWhenPoolsReady());
        }

        private IEnumerator BuildWhenPoolsReady()
        {
            if (segmentPrefab == null)
            {
                Debug.LogError("ScrollingBackground needs the Background prefab assigned.", this);
                enabled = false;
                yield break;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            while (pool != null && !pool.IsReady)
            {
                yield return null;
            }

            if (scenePreview != null)
            {
                scenePreview.SetActive(false);
            }

            BackgroundSegment.ResetChestSchedule();
            BackgroundSegment.SetOpeningRoad(true);

            root = new GameObject("Backgrounds").transform;
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            int count = Mathf.Max(2, segmentCount);
            float height = segmentPrefab.Height;
            Vector3 origin = new Vector3(0f, 0f, segmentPrefab.transform.position.z);
            int middle = count / 2;

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = origin;
                pos.y = origin.y + (i - middle) * height;
                BackgroundSegment segment = SpawnSegment(pos);
                if (segment == null)
                {
                    BackgroundSegment.SetOpeningRoad(false);
                    enabled = false;
                    yield break;
                }

                segments.Add(segment);
            }

            BackgroundSegment.SetOpeningRoad(false);
        }

        private void Update()
        {
            float speed = ScrollDirector.Speed * parallaxFactor;
            if (speed == 0f || segments.Count == 0)
            {
                return;
            }

            float delta = speed * Time.deltaTime;
            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].Scroll(delta);
            }

            RecycleIfNeeded();
        }

        private BackgroundSegment SpawnSegment(Vector3 position)
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            return pool != null ? pool.Spawn(segmentPrefab, position, Quaternion.identity, root) : null;
        }

        private void RecycleIfNeeded()
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds == null)
            {
                return;
            }

            BackgroundSegment lowest = segments[0];
            BackgroundSegment highest = segments[0];
            for (int i = 1; i < segments.Count; i++)
            {
                if (segments[i].transform.position.y < lowest.transform.position.y)
                {
                    lowest = segments[i];
                }

                if (segments[i].transform.position.y > highest.transform.position.y)
                {
                    highest = segments[i];
                }
            }

            if (lowest.TopY > bounds.ScreenDespawnY)
            {
                return;
            }

            Vector3 pos = lowest.transform.position;
            pos.y = highest.transform.position.y + lowest.Height;
            lowest.transform.position = pos;
            lowest.Prepare();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null)
                {
                    segments[i].ClearSpawned();
                }
            }
        }
    }
}
