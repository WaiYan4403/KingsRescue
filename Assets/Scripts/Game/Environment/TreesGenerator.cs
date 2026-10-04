using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;

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

    private readonly List<TreesGenerator> rows = new List<TreesGenerator>();
    private float cachedSpacing;

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
        if (treePrefab == null)
        {
            RandomizeTrees();
            return;
        }

        if (rows.Count == 0)
        {
            Fill(chunkHeight);
            return;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            rows[i]?.RandomizeTrees();
        }
    }

    public void ClearSpawned()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null)
            {
                Destroy(rows[i].gameObject);
            }
        }

        rows.Clear();
    }

    private void Fill(float chunkHeight)
    {
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
            GameObject instance = Instantiate(treePrefab, treesRoot);
            instance.name = treePrefab.name;
            instance.transform.localPosition = new Vector3(0f, startY + i * spacing, 0f);
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = treePrefab.transform.localScale;

            TreesGenerator generator = instance.GetComponent<TreesGenerator>();
            if (generator != null)
            {
                generator.RandomizeTrees();
                rows.Add(generator);
            }
        }
    }

    private float ResolveSpacing()
    {
        if (cachedSpacing > 0f)
        {
            return cachedSpacing;
        }

        if (spacingOverride > 0f)
        {
            cachedSpacing = spacingOverride;
            return cachedSpacing;
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

        cachedSpacing = height * (1f - overlap);
        return cachedSpacing;
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
