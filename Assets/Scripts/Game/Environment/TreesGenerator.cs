using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.World;

public class TreesGenerator : MonoBehaviour, IPoolable
{
    [Header("Sprite shuffle (Tree prefab)")]
    [SerializeField] Sprite[] treeLeftSprites;
    [SerializeField] Sprite[] treeRightSprites;
    [SerializeField] SpriteRenderer treeLeftObj;
    [SerializeField] SpriteRenderer treeLeftShadowObj;
    [SerializeField] SpriteRenderer treeRightObj;
    [SerializeField] SpriteRenderer treeRightShadowObj;

    [Header("Spawn along a background tile")]
    [SerializeField] GameObject treePrefab;
    [SerializeField] Transform treesRoot;

    [Tooltip("Leave 0 to use the tree sprite height. Smaller values overlap rows.")]
    [SerializeField] float spacingOverride;

    [SerializeField, Range(0f, 0.5f)] float overlap = 0.08f;

    private readonly List<GameObject> spawned = new List<GameObject>();

    private void Awake()
    {
        if (treesRoot == null)
        {
            treesRoot = transform;
        }
    }

    private void Start()
    {
        RandomizeTrees();
    }

    public void OnSpawned()
    {
        RandomizeTrees();
    }

    public void OnDespawned()
    {
    }

    public void RandomizeTrees()
    {
        AssignRandom(treeLeftSprites, treeLeftObj, treeLeftShadowObj);
        AssignRandom(treeRightSprites, treeRightObj, treeRightShadowObj);
    }

    public void Generate(float chunkHeight)
    {
        ClearSpawned();
        if (treePrefab == null)
        {
            RandomizeTrees();
            return;
        }

        float spacing = ResolveSpacing();
        if (spacing <= 0.01f)
        {
            return;
        }

        float half = chunkHeight * 0.5f;
        int count = Mathf.Max(1, Mathf.CeilToInt(chunkHeight / spacing));
        float startY = -half + spacing * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float y = startY + i * spacing;
            Vector3 world = treesRoot.TransformPoint(new Vector3(0f, y, 0f));
            GameObject instance = BackgroundSegment.SpawnChild(treePrefab, world, treesRoot);
            if (instance != null)
            {
                spawned.Add(instance);
            }
        }
    }

    public void ClearSpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            BackgroundSegment.Release(spawned[i]);
        }

        spawned.Clear();
    }

    private float ResolveSpacing()
    {
        if (spacingOverride > 0f)
        {
            return spacingOverride;
        }

        float height = 3f;
        if (treePrefab != null)
        {
            SpriteRenderer renderer = treePrefab.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
            {
                height = renderer.sprite.rect.height / renderer.sprite.pixelsPerUnit;
                height *= Mathf.Abs(renderer.transform.localScale.y);
            }
        }

        return height * (1f - overlap);
    }

    private static void AssignRandom(Sprite[] sprites, SpriteRenderer target, SpriteRenderer shadow)
    {
        if (sprites == null || sprites.Length == 0 || target == null)
        {
            return;
        }

        target.sprite = sprites[Random.Range(0, sprites.Length)];
        if (shadow != null)
        {
            shadow.sprite = target.sprite;
        }
    }
}
