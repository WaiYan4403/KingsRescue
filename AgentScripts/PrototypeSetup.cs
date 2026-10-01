using System;
using System.Collections.Generic;
using System.IO;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Enemies;
using KingsRescue.InputControl;
using KingsRescue.Pickups;
using KingsRescue.Player;
using KingsRescue.Progression;
using KingsRescue.Spawning;
using KingsRescue.UI;
using KingsRescue.World;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class PrototypeSetup
{
    const string ScenePath = "Assets/Scenes/Gameplay.unity";
    const string ArtDir = "Assets/Art/Proto";

    public static string Run()
    {
        var log = new List<string>();

        EnsureFolders();
        EnsureGrassWrap();
        var art = BuildArt();

        var pickups = BuildPickupPrefabs(art);
        var combat = BuildCombatPrefabs(art);
        var enemies = BuildEnemyPrefabs(art, pickups);
        var props = BuildPropPrefabs(art, pickups);

        var data = BuildData(enemies, combat);
        WireWeaponLoadoutOnPlayerPrefab(BuildPlayerPrefab(art, data.Sword), data.Sword);

        BuildGameplayScene(art, pickups, enemies, props, combat, data);
        AddSceneToBuild();

        AssetDatabase.SaveAssets();
        log.Add("Gameplay scene, prefabs, and data authored.");
        log.Add("Player sprite packs were missing; used generated 16 PPU placeholders.");
        return string.Join("\n", log);
    }

    static void EnsureFolders()
    {
        string[] folders =
        {
            "Assets/Art", ArtDir,
            "Assets/Prefabs", "Assets/Prefabs/Enemies", "Assets/Prefabs/Pickups",
            "Assets/Prefabs/Weapons", "Assets/Prefabs/Props", "Assets/Prefabs/FX",
            "Assets/Data", "Assets/Data/Enemies", "Assets/Data/Waves",
            "Assets/Data/Weapons", "Assets/Data/Passives", "Assets/Data/Upgrades",
            "Assets/Materials", "Assets/Scenes"
        };
        foreach (string folder in folders)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }
    }

    static void EnsureGrassWrap()
    {
        const string path = "Assets/Sprites/GrassTile.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = 16;
        importer.SaveAndReimport();
    }

    class Art
    {
        public Sprite White;
        public Sprite Circle;
        public Sprite Ring;
        public Sprite Knight;
        public Sprite Orc;
        public Sprite Goblin;
        public Sprite Skelly;
        public Sprite Arrow;
        public Sprite Blade;
        public Sprite Slash;
        public Sprite Burst;
        public Sprite Gem;
        public Sprite Coin;
        public Sprite Heart;
        public Sprite Pot;
        public Sprite Chest;
        public Sprite Tree;
        public Sprite Rock;
        public Texture2D Grass;
        public Material Ground;
    }

    static Art BuildArt()
    {
        var art = new Art
        {
            White = CircleSprite("White", Color.white, 16, filled: true),
            Circle = CircleSprite("Circle", Color.white, 32, filled: true),
            Ring = CircleSprite("Ring", new Color(1f, 1f, 1f, 0.9f), 64, filled: false),
            Knight = RectSprite("Knight", new Color(0.85f, 0.82f, 0.55f), 16, 24),
            Orc = RectSprite("Orc", new Color(0.35f, 0.62f, 0.28f), 16, 18),
            Goblin = RectSprite("Goblin", new Color(0.45f, 0.75f, 0.22f), 12, 14),
            Skelly = RectSprite("Skelly", new Color(0.9f, 0.9f, 0.82f), 14, 22),
            Arrow = RectSprite("Arrow", new Color(1f, 0.92f, 0.35f), 6, 14),
            Blade = RectSprite("Blade", new Color(0.85f, 0.2f, 0.2f), 8, 20),
            Slash = CircleSprite("Slash", new Color(1f, 1f, 1f, 0.55f), 32, filled: false),
            Burst = CircleSprite("Burst", new Color(0.6f, 0.85f, 1f, 0.45f), 32, filled: true),
            Gem = CircleSprite("Gem", new Color(0.3f, 0.9f, 1f), 12, filled: true),
            Coin = CircleSprite("Coin", new Color(1f, 0.82f, 0.2f), 12, filled: true),
            Heart = CircleSprite("Heart", new Color(0.95f, 0.25f, 0.3f), 12, filled: true),
            Pot = RectSprite("Pot", new Color(0.55f, 0.32f, 0.18f), 12, 14),
            Chest = RectSprite("Chest", new Color(0.78f, 0.52f, 0.16f), 16, 12),
            Tree = RectSprite("Tree", new Color(0.15f, 0.38f, 0.18f), 14, 22),
            Rock = CircleSprite("Rock", new Color(0.45f, 0.45f, 0.42f), 16, filled: true),
            Grass = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/GrassTile.png")
        };

        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Texture")
                     ?? Shader.Find("Sprites/Default");
        art.Ground = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/GroundScroll.mat");
        if (art.Ground == null)
        {
            art.Ground = new Material(shader);
            AssetDatabase.CreateAsset(art.Ground, "Assets/Materials/GroundScroll.mat");
        }

        art.Ground.shader = shader;
        if (art.Grass != null)
        {
            if (art.Ground.HasProperty("_BaseMap"))
            {
                art.Ground.SetTexture("_BaseMap", art.Grass);
            }

            if (art.Ground.HasProperty("_MainTex"))
            {
                art.Ground.SetTexture("_MainTex", art.Grass);
            }

            art.Ground.mainTextureScale = new Vector2(6f, 10f);
        }

        EditorUtility.SetDirty(art.Ground);
        return art;
    }

    static Sprite CircleSprite(string name, Color color, int size, bool filled)
    {
        string path = $"{ArtDir}/{name}.png";
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        float r = size * 0.5f - 0.5f;
        float inner = r * 0.62f;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - r;
                float dy = y - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                bool on = filled ? d <= r : d <= r && d >= inner;
                pixels[y * size + x] = on ? color : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        ConfigureSpriteImporter(path);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Sprite RectSprite(string name, Color color, int w, int h)
    {
        string path = $"{ArtDir}/{name}.png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        ConfigureSpriteImporter(path);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void ConfigureSpriteImporter(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }

    class PickupPrefabs
    {
        public GameObject Gem;
        public GameObject Coin;
        public GameObject Health;
    }

    static PickupPrefabs BuildPickupPrefabs(Art art)
    {
        var gem = Body("Pickup_XpGem", art.Gem, 6);
        gem.AddComponent<OffscreenDespawner>();
        var xp = gem.AddComponent<XpGem>();
        Set(xp, "value", 1);
        Set(xp, "spriteRenderer", gem.GetComponent<SpriteRenderer>());
        var tiers = So(xp).FindProperty("tiers");
        tiers.arraySize = 3;
        SetTier(tiers, 0, 1, new Color(0.3f, 0.9f, 1f), 1f);
        SetTier(tiers, 1, 5, new Color(0.35f, 1f, 0.45f), 1.25f);
        SetTier(tiers, 2, 20, new Color(0.95f, 0.4f, 1f), 1.6f);
        So(xp).ApplyModifiedPropertiesWithoutUndo();
        SetPickupDefaults(gem);

        var coin = Body("Pickup_Coin", art.Coin, 6);
        coin.AddComponent<OffscreenDespawner>();
        var coinPickup = coin.AddComponent<CoinPickup>();
        Set(coinPickup, "value", 1);
        SetPickupDefaults(coin);

        var health = Body("Pickup_Health", art.Heart, 6);
        health.AddComponent<OffscreenDespawner>();
        var hp = health.AddComponent<HealthPickup>();
        Set(hp, "flatHeal", 15f);
        Set(hp, "percentOfMaxHeal", 0.1f);
        SetPickupDefaults(health);

        return new PickupPrefabs
        {
            Gem = SavePrefab(gem, "Assets/Prefabs/Pickups/Pickup_XpGem.prefab"),
            Coin = SavePrefab(coin, "Assets/Prefabs/Pickups/Pickup_Coin.prefab"),
            Health = SavePrefab(health, "Assets/Prefabs/Pickups/Pickup_Health.prefab")
        };
    }

    static void SetPickupDefaults(GameObject go)
    {
        var pickup = go.GetComponent<Pickup>();
        Set(pickup, "scrollInfluence", 1f);
        Set(pickup, "attractAcceleration", 40f);
        Set(pickup, "maxAttractSpeed", 20f);
        Set(pickup, "collectRadius", 0.4f);
    }

    static void SetTier(SerializedProperty tiers, int index, int min, Color color, float scale)
    {
        var el = tiers.GetArrayElementAtIndex(index);
        el.FindPropertyRelative("minValue").intValue = min;
        el.FindPropertyRelative("color").colorValue = color;
        el.FindPropertyRelative("scale").floatValue = scale;
    }

    class CombatPrefabs
    {
        public GameObject Arrow;
        public GameObject Blade;
        public GameObject Slash;
        public GameObject Aura;
    }

    static CombatPrefabs BuildCombatPrefabs(Art art)
    {
        var arrow = Body("Projectile_Arrow", art.Arrow, 8);
        var projectile = arrow.AddComponent<Projectile>();
        Set(projectile, "hitRadius", 0.3f);
        Set(projectile, "rotateToDirection", true);

        var blade = Body("OrbitBlade_Sword", art.Blade, 8);
        var orbit = blade.AddComponent<OrbitBlade>();
        Set(orbit, "hitRadius", 0.45f);
        Set(orbit, "spinSprite", true);

        var slash = Body("FX_SwordSwing", art.Slash, 5);
        slash.transform.localScale = Vector3.one;
        var slashDespawn = slash.AddComponent<AutoDespawn>();
        Set(slashDespawn, "lifetime", 0.25f);

        var aura = Body("FX_AuraPulse", art.Burst, 5);
        aura.transform.localScale = Vector3.one;
        var auraDespawn = aura.AddComponent<AutoDespawn>();
        Set(auraDespawn, "lifetime", 0.25f);

        return new CombatPrefabs
        {
            Arrow = SavePrefab(arrow, "Assets/Prefabs/Weapons/Projectile_Arrow.prefab"),
            Blade = SavePrefab(blade, "Assets/Prefabs/Weapons/OrbitBlade_Sword.prefab"),
            Slash = SavePrefab(slash, "Assets/Prefabs/FX/FX_SwordSwing.prefab"),
            Aura = SavePrefab(aura, "Assets/Prefabs/FX/FX_AuraPulse.prefab")
        };
    }

    class EnemyPrefabs
    {
        public GameObject Orc;
        public GameObject Goblin;
        public GameObject Skelly;
    }

    static EnemyPrefabs BuildEnemyPrefabs(Art art, PickupPrefabs pickups)
    {
        return new EnemyPrefabs
        {
            Orc = EnemyPrefab("Enemy_Orc", art.Orc, 0.5f, pickups),
            Goblin = EnemyPrefab("Enemy_Goblin", art.Goblin, 0.4f, pickups),
            Skelly = EnemyPrefab("Enemy_Skelly", art.Skelly, 0.55f, pickups)
        };
    }

    static GameObject EnemyPrefab(string name, Sprite sprite, float radius, PickupPrefabs pickups)
    {
        var go = Body(name, sprite, 4);
        var health = go.AddComponent<Health>();
        Set(health, "maxHealth", 10f);
        Set(health, "hitRadius", radius);
        go.AddComponent<Enemy>();
        var movement = go.AddComponent<EnemyMovement>();
        Set(movement, "chaseSlack", 1f);
        Set(movement, "speedJitter", 0.12f);
        Set(movement, "spriteRenderer", go.GetComponent<SpriteRenderer>());
        var contact = go.AddComponent<ContactDamage>();
        Set(contact, "playerRadius", 0.35f);
        var dropper = go.AddComponent<EnemyDropper>();
        Set(dropper, "xpGemPrefab", pickups.Gem);
        Set(dropper, "coinPrefab", pickups.Coin);
        Set(dropper, "healthPrefab", pickups.Health);
        go.AddComponent<OffscreenDespawner>();
        go.AddComponent<HitFlash>();
        return SavePrefab(go, $"Assets/Prefabs/Enemies/{name}.prefab");
    }

    class PropPrefabs
    {
        public GameObject Pot;
        public GameObject Chest;
        public GameObject Tree;
        public GameObject Rock;
    }

    static PropPrefabs BuildPropPrefabs(Art art, PickupPrefabs pickups)
    {
        return new PropPrefabs
        {
            Pot = Breakable("Prop_Pot", art.Pot, 1f, 2, 5, pickups),
            Chest = Breakable("Prop_Chest", art.Chest, 6f, 6, 12, pickups),
            Tree = Deco("Prop_Tree", art.Tree),
            Rock = Deco("Prop_Rock", art.Rock)
        };
    }

    static GameObject Breakable(string name, Sprite sprite, float hp, int minCoins, int maxCoins, PickupPrefabs pickups)
    {
        var go = Body(name, sprite, 2);
        var health = go.AddComponent<Health>();
        Set(health, "maxHealth", hp);
        Set(health, "hitRadius", 0.4f);
        var prop = go.AddComponent<DestructibleProp>();
        Set(prop, "coinPrefab", pickups.Coin);
        Set(prop, "minCoins", minCoins);
        Set(prop, "maxCoins", maxCoins);
        Set(prop, "coinValue", 1);
        Set(prop, "healthPrefab", pickups.Health);
        Set(prop, "healthDropChance", 0.08f);
        var scroll = go.AddComponent<Scrollable>();
        Set(scroll, "scrollInfluence", 1f);
        go.AddComponent<OffscreenDespawner>();
        go.AddComponent<HitFlash>();
        return SavePrefab(go, $"Assets/Prefabs/Props/{name}.prefab");
    }

    static GameObject Deco(string name, Sprite sprite)
    {
        var go = Body(name, sprite, 1);
        var scroll = go.AddComponent<Scrollable>();
        Set(scroll, "scrollInfluence", 1f);
        go.AddComponent<OffscreenDespawner>();
        return SavePrefab(go, $"Assets/Prefabs/Props/{name}.prefab");
    }

    class DataAssets
    {
        public EnemyDefinition Orc;
        public EnemyDefinition Goblin;
        public EnemyDefinition Skelly;
        public WaveDefinition Wave1;
        public WaveDefinition Wave2;
        public WaveDefinition Wave3;
        public WaveDefinition Endless;
        public WeaponDefinition Sword;
        public WeaponDefinition Bow;
        public WeaponDefinition Orbit;
        public WeaponDefinition Aura;
        public List<UpgradeDefinition> Pool = new List<UpgradeDefinition>();
        public UpgradeDefinition Coins;
        public List<PassiveDefinition> Passives = new List<PassiveDefinition>();
    }

    static DataAssets BuildData(EnemyPrefabs enemies, CombatPrefabs combat)
    {
        var data = new DataAssets
        {
            Orc = EnemyDef("Enemy_Orc", "Orc", enemies.Orc, 14, 9, 0.5f, 2.4f, 0.8f, 1, 0.05f),
            Goblin = EnemyDef("Enemy_Goblin", "Goblin", enemies.Goblin, 8, 6, 0.4f, 3.6f, 0.5f, 1, 0.04f),
            Skelly = EnemyDef("Enemy_Skelly", "Skelly", enemies.Skelly, 26, 14, 0.55f, 1.8f, 1f, 3, 0.12f)
        };

        data.Wave1 = Wave("Wave_01", "Wave 1", 0, 60, 1.4f, 1, SpawnPattern.TopRandom,
            new[] { Entry(data.Orc, 100) }, Array.Empty<WaveDefinition.Burst>());
        data.Wave2 = Wave("Wave_02", "Wave 2", 45, 75, 1f, 2, SpawnPattern.TopRandom,
            new[] { Entry(data.Orc, 60), Entry(data.Goblin, 40) },
            new[] { Burst(30, data.Goblin, 8, SpawnPattern.TopLine) });
        data.Wave3 = Wave("Wave_03", "Wave 3", 110, 90, 0.8f, 2, SpawnPattern.TopRandom,
            new[] { Entry(data.Orc, 40), Entry(data.Goblin, 40), Entry(data.Skelly, 20) },
            new[] { Burst(45, data.Skelly, 12, SpawnPattern.SurroundPlayer) });
        data.Endless = Wave("Wave_Endless", "Endless", 0, 9999, 0.6f, 3, SpawnPattern.TopRandom,
            new[] { Entry(data.Orc, 40), Entry(data.Goblin, 40), Entry(data.Skelly, 20) },
            Array.Empty<WaveDefinition.Burst>());

        data.Sword = Weapon("Weapon_Sword", "Sword", "A swinging blade that hunts the nearest enemy.",
            WeaponFireMode.MeleeSwing, null, null, combat.Slash, true, new[]
            {
                L(12, 0.85f, 1, 0, 2.2f, 12, 0.25f, 0, 150, 14, 0.45f, 0.7f, ""),
                L(16, 0.85f, 1, 0, 2.2f, 12, 0.25f, 0, 170, 14, 0.45f, 0.7f, "Wider, heavier swing"),
                L(16, 0.85f, 2, 0, 2.2f, 12, 0.25f, 0, 170, 14, 0.45f, 0.7f, "Two-hit combo"),
                L(22, 0.85f, 2, 0, 2.8f, 12, 0.25f, 0, 180, 14, 0.45f, 0.7f, "Longer reach"),
                L(30, 0.85f, 3, 0, 2.8f, 12, 0.25f, 0, 360, 14, 0.45f, 0.7f, "Full spin. Hits everything around you")
            });
        data.Bow = Weapon("Weapon_Bow", "Bow", "Fires arrows up the screen.",
            WeaponFireMode.ForwardShot, combat.Arrow, null, null, true, new[]
            {
                L(9, 0.7f, 1, 0, 1f, 14, 1.8f, 0, 150, 14, 0.45f, 0.7f, ""),
                L(9, 0.7f, 2, 0, 1f, 14, 1.8f, 0, 150, 14, 0.45f, 0.7f, "+1 arrow"),
                L(9, 0.7f, 2, 1, 1f, 14, 1.8f, 0, 150, 16, 0.45f, 0.7f, "Pierce 1"),
                L(14, 0.7f, 3, 1, 1f, 14, 1.8f, 0, 150, 22, 0.45f, 0.7f, "Three-arrow volley"),
                L(14, 0.5f, 3, 3, 1f, 16, 1.8f, 0, 150, 22, 0.45f, 0.7f, "Faster, more pierce")
            });
        data.Orbit = Weapon("Weapon_Orbit", "Orbit Blade", "A blade that circles the hero.",
            WeaponFireMode.Orbit, null, combat.Blade, null, true, new[]
            {
                L(8, 1f, 1, 0, 1.9f, 12, 1f, 0, 150, 14, 0.45f, 0.7f, ""),
                L(8, 1f, 2, 0, 1.9f, 12, 1f, 0, 150, 14, 0.45f, 0.7f, "+1 blade"),
                L(12, 1f, 2, 0, 1.9f, 12, 1f, 0, 150, 14, 0.45f, 0.95f, "Faster orbit"),
                L(12, 1f, 3, 0, 2.4f, 12, 1f, 0, 150, 14, 0.45f, 0.95f, "+1 blade, more reach"),
                L(18, 1f, 4, 0, 2.4f, 12, 1f, 0, 150, 14, 0.4f, 1.1f, "Four blades")
            });
        data.Aura = Weapon("Weapon_Aura", "Aura", "A pulsing damage field.",
            WeaponFireMode.Aura, null, null, combat.Aura, false, new[]
            {
                L(6, 1.2f, 1, 0, 2.3f, 12, 0.25f, 0, 360, 14, 0.45f, 0.7f, ""),
                L(9, 1.2f, 1, 0, 2.3f, 12, 0.25f, 0, 360, 14, 0.45f, 0.7f, "More damage"),
                L(9, 1.2f, 1, 0, 3f, 12, 0.25f, 0, 360, 14, 0.45f, 0.7f, "Larger aura"),
                L(14, 0.9f, 1, 0, 3f, 12, 0.25f, 0, 360, 14, 0.45f, 0.7f, "Faster pulse"),
                L(20, 0.9f, 1, 0, 3.8f, 12, 0.25f, 0, 360, 14, 0.45f, 0.7f, "Huge aura")
            });

        data.Passives.Add(Passive("Passive_Boots", "Boots", StatType.MoveSpeed, ModifierMode.Percent, 0.12f, 5));
        data.Passives.Add(Passive("Passive_Gauntlet", "Gauntlet", StatType.Damage, ModifierMode.Percent, 0.15f, 5));
        data.Passives.Add(Passive("Passive_Hourglass", "Hourglass", StatType.AttackSpeed, ModifierMode.Percent, 0.12f, 5));
        data.Passives.Add(Passive("Passive_Lodestone", "Lodestone", StatType.MagnetRadius, ModifierMode.Percent, 0.35f, 3));
        data.Passives.Add(PassivePlate());
        data.Passives.Add(Passive("Passive_Tome", "Tome", StatType.XpGain, ModifierMode.Percent, 0.15f, 3));

        data.Pool.Add(UpgradeWeapon("Upgrade_Sword_Level", UpgradeKind.WeaponLevel, data.Sword));
        data.Pool.Add(UpgradeWeapon("Upgrade_Bow_New", UpgradeKind.NewWeapon, data.Bow));
        data.Pool.Add(UpgradeWeapon("Upgrade_Bow_Level", UpgradeKind.WeaponLevel, data.Bow));
        data.Pool.Add(UpgradeWeapon("Upgrade_Orbit_New", UpgradeKind.NewWeapon, data.Orbit));
        data.Pool.Add(UpgradeWeapon("Upgrade_Orbit_Level", UpgradeKind.WeaponLevel, data.Orbit));
        data.Pool.Add(UpgradeWeapon("Upgrade_Aura_New", UpgradeKind.NewWeapon, data.Aura));
        data.Pool.Add(UpgradeWeapon("Upgrade_Aura_Level", UpgradeKind.WeaponLevel, data.Aura));
        foreach (var passive in data.Passives)
        {
            data.Pool.Add(UpgradePassive(passive));
        }

        data.Coins = ScriptableObject.CreateInstance<UpgradeDefinition>();
        data.Coins.kind = UpgradeKind.CoinBonus;
        data.Coins.weight = 1f;
        data.Coins.coinAmount = 25;
        data.Coins.titleOverride = "25 Coins";
        AssetDatabase.CreateAsset(data.Coins, "Assets/Data/Upgrades/Upgrade_Coins.asset");

        return data;
    }

    static EnemyDefinition EnemyDef(string file, string name, GameObject prefab, float hp, float dmg, float radius, float speed, float scroll, int xp, float coinChance)
    {
        var def = ScriptableObject.CreateInstance<EnemyDefinition>();
        def.displayName = name;
        def.prefab = prefab;
        def.maxHealth = hp;
        def.contactDamage = dmg;
        def.contactRadius = radius;
        def.moveSpeed = speed;
        def.scrollInfluence = scroll;
        def.followChance = 1f;
        def.giveUpWhenPassedChance = 0.45f;
        def.separationRadius = radius * 1.7f;
        def.separationStrength = 3.25f;
        def.xpValue = xp;
        def.coinDropChance = coinChance;
        AssetDatabase.CreateAsset(def, $"Assets/Data/Enemies/{file}.asset");
        return def;
    }

    static WaveDefinition.EnemyEntry Entry(EnemyDefinition enemy, float weight)
    {
        return new WaveDefinition.EnemyEntry { enemy = enemy, weight = weight };
    }

    static WaveDefinition.Burst Burst(float at, EnemyDefinition enemy, int count, SpawnPattern pattern)
    {
        return new WaveDefinition.Burst { atTime = at, enemy = enemy, count = count, pattern = pattern };
    }

    static WaveDefinition Wave(string file, string label, float start, float duration, float interval, int perTick, SpawnPattern pattern, WaveDefinition.EnemyEntry[] enemies, WaveDefinition.Burst[] bursts)
    {
        var wave = ScriptableObject.CreateInstance<WaveDefinition>();
        wave.label = label;
        wave.startTime = start;
        wave.duration = duration;
        wave.spawnInterval = interval;
        wave.spawnsPerTick = perTick;
        wave.pattern = pattern;
        wave.enemies = enemies;
        wave.bursts = bursts;
        AssetDatabase.CreateAsset(wave, $"Assets/Data/Waves/{file}.asset");
        return wave;
    }

    static WeaponLevel L(float damage, float cooldown, int count, int pierce, float area, float projSpeed, float lifetime, float knockback, float arc, float spread, float rehit, float orbit, string text)
    {
        return new WeaponLevel
        {
            damage = damage,
            cooldown = cooldown,
            count = count,
            pierce = pierce,
            area = area,
            projectileSpeed = projSpeed,
            lifetime = lifetime,
            knockback = knockback,
            arcDegrees = arc,
            spreadDegrees = spread,
            rehitInterval = rehit,
            orbitSpeed = orbit,
            upgradeText = text
        };
    }

    static WeaponDefinition Weapon(string file, string name, string description, WeaponFireMode mode, GameObject projectile, GameObject orbit, GameObject effect, bool extraProjectiles, WeaponLevel[] levels)
    {
        var def = ScriptableObject.CreateInstance<WeaponDefinition>();
        def.displayName = name;
        def.description = description;
        def.fireMode = mode;
        def.projectilePrefab = projectile;
        def.orbitPrefab = orbit;
        def.effectPrefab = effect;
        def.usesGlobalProjectileCount = extraProjectiles;
        def.comboDelay = 0.12f;
        def.levels = levels;
        AssetDatabase.CreateAsset(def, $"Assets/Data/Weapons/{file}.asset");
        return def;
    }

    static PassiveDefinition Passive(string file, string name, StatType stat, ModifierMode mode, float value, int levels)
    {
        var def = ScriptableObject.CreateInstance<PassiveDefinition>();
        def.displayName = name;
        def.description = $"{name} upgrades.";
        def.levels = new PassiveDefinition.PassiveLevel[levels];
        for (int i = 0; i < levels; i++)
        {
            def.levels[i] = new PassiveDefinition.PassiveLevel
            {
                modifiers = new[] { new StatModifier(stat, mode, value) }
            };
        }

        AssetDatabase.CreateAsset(def, $"Assets/Data/Passives/{file}.asset");
        return def;
    }

    static PassiveDefinition PassivePlate()
    {
        var def = ScriptableObject.CreateInstance<PassiveDefinition>();
        def.displayName = "Plate";
        def.description = "Max HP and armor.";
        def.levels = new PassiveDefinition.PassiveLevel[4];
        for (int i = 0; i < 4; i++)
        {
            def.levels[i] = new PassiveDefinition.PassiveLevel
            {
                modifiers = new[]
                {
                    new StatModifier(StatType.MaxHp, ModifierMode.Flat, 30f),
                    new StatModifier(StatType.Armor, ModifierMode.Flat, 1f)
                }
            };
        }

        AssetDatabase.CreateAsset(def, "Assets/Data/Passives/Passive_Plate.asset");
        return def;
    }

    static UpgradeDefinition UpgradeWeapon(string file, UpgradeKind kind, WeaponDefinition weapon)
    {
        var def = ScriptableObject.CreateInstance<UpgradeDefinition>();
        def.kind = kind;
        def.weight = 100f;
        def.weapon = weapon;
        AssetDatabase.CreateAsset(def, $"Assets/Data/Upgrades/{file}.asset");
        return def;
    }

    static UpgradeDefinition UpgradePassive(PassiveDefinition passive)
    {
        var def = ScriptableObject.CreateInstance<UpgradeDefinition>();
        def.kind = UpgradeKind.Passive;
        def.weight = 80f;
        def.passive = passive;
        AssetDatabase.CreateAsset(def, $"Assets/Data/Upgrades/Upgrade_{passive.displayName}.asset");
        return def;
    }

    static GameObject BuildPlayerPrefab(Art art, WeaponDefinition sword)
    {
        var go = Body("Player", art.Knight, 10);
        go.transform.position = new Vector3(0f, -5f, 0f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        var stats = go.AddComponent<PlayerStats>();
        var overrides = So(stats).FindProperty("baseOverrides");
        overrides.arraySize = 3;
        SetOverride(overrides, 0, StatType.MoveSpeed, 6f);
        SetOverride(overrides, 1, StatType.MaxHp, 120f);
        SetOverride(overrides, 2, StatType.MagnetRadius, 2.5f);
        So(stats).ApplyModifiedPropertiesWithoutUndo();
        go.AddComponent<PlayerController>();
        var health = go.AddComponent<PlayerHealth>();
        Set(health, "invulnerabilityDuration", 0.5f);
        Set(health, "flashTarget", go.GetComponent<SpriteRenderer>());
        var magnet = go.AddComponent<PickupMagnet>();
        Set(magnet, "scanInterval", 0.08f);
        var loadout = go.AddComponent<WeaponLoadout>();
        Set(loadout, "startingWeapon", sword);
        Set(loadout, "maxWeapons", 6);
        Set(loadout, "maxPassives", 6);
        return SavePrefab(go, "Assets/Prefabs/Player.prefab");
    }

    static void WireWeaponLoadoutOnPlayerPrefab(GameObject playerPrefab, WeaponDefinition sword)
    {
        Set(playerPrefab.GetComponent<WeaponLoadout>(), "startingWeapon", sword);
        EditorUtility.SetDirty(playerPrefab);
    }

    static void SetOverride(SerializedProperty list, int index, StatType stat, float value)
    {
        var el = list.GetArrayElementAtIndex(index);
        el.FindPropertyRelative("stat").enumValueIndex = (int)stat;
        el.FindPropertyRelative("value").floatValue = value;
    }

    static void BuildGameplayScene(Art art, PickupPrefabs pickups, EnemyPrefabs enemies, PropPrefabs props, CombatPrefabs combat, DataAssets data)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        cameraGo.transform.position = new Vector3(0f, 0f, -10f);
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 10f;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 50f;
        camera.backgroundColor = new Color(0.07f, 0.09f, 0.07f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        cameraGo.AddComponent<AudioListener>();
        var ppc = cameraGo.AddComponent<PixelPerfectCamera>();
        SetIfExists(ppc, "m_AssetsPPU", 16);
        SetIfExists(ppc, "assetsPPU", 16);
        SetIfExists(ppc, "m_RefResolutionX", 180);
        SetIfExists(ppc, "refResolutionX", 180);
        SetIfExists(ppc, "m_RefResolutionY", 320);
        SetIfExists(ppc, "refResolutionY", 320);
        SetIfExists(ppc, "m_PixelSnapping", true);
        SetIfExists(ppc, "pixelSnapping", true);

        var lightGo = new GameObject("Global Light 2D");
        var light = lightGo.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;

        var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
        background.name = "Background";
        background.transform.position = new Vector3(0f, 0f, 10f);
        background.transform.localScale = new Vector3(12f, 21f, 1f);
        UnityEngine.Object.DestroyImmediate(background.GetComponent<MeshCollider>());
        background.GetComponent<MeshRenderer>().sharedMaterial = art.Ground;
        var scrolling = background.AddComponent<ScrollingBackground>();
        Set(scrolling, "parallaxFactor", 1f);
        Set(scrolling, "tilesPerWorldUnit", 0.5f);
        Set(scrolling, "textureProperty", "_BaseMap");

        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));
        player.transform.position = new Vector3(0f, -5f, 0f);

        var systems = new GameObject("Systems");
        var bounds = systems.AddComponent<ArenaBounds>();
        Set(bounds, "targetCamera", camera);
        Set(bounds, "paddingLeft", 1f);
        Set(bounds, "paddingRight", 1f);
        Set(bounds, "paddingTop", 2f);
        Set(bounds, "paddingBottom", 1.5f);
        Set(bounds, "spawnMargin", 2.5f);
        Set(bounds, "despawnMargin", 3f);

        var scroll = systems.AddComponent<ScrollDirector>();
        Set(scroll, "baseSpeed", 3f);
        Set(scroll, "speedGainPerMinute", 0.9f);
        Set(scroll, "maxSpeed", 7f);

        var pool = systems.AddComponent<PoolManager>();
        var prewarm = So(pool).FindProperty("prewarm");
        prewarm.arraySize = 8;
        Prewarm(prewarm, 0, enemies.Orc, 80);
        Prewarm(prewarm, 1, enemies.Goblin, 80);
        Prewarm(prewarm, 2, enemies.Skelly, 80);
        Prewarm(prewarm, 3, pickups.Gem, 60);
        Prewarm(prewarm, 4, pickups.Coin, 40);
        Prewarm(prewarm, 5, combat.Arrow, 60);
        Prewarm(prewarm, 6, props.Pot, 20);
        Prewarm(prewarm, 7, props.Chest, 12);
        So(pool).ApplyModifiedPropertiesWithoutUndo();

        var game = systems.AddComponent<GameManager>();
        Set(game, "playerHealth", player.GetComponent<PlayerHealth>());
        Set(game, "menuSceneName", "SampleScene");

        var input = systems.AddComponent<PlayerInputReader>();
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        Set(input, "actionAsset", actions);

        var xp = systems.AddComponent<ExperienceSystem>();
        Set(xp, "stats", player.GetComponent<PlayerStats>());
        Set(xp, "baseXp", 5f);
        Set(xp, "growth", 2.4f);
        Set(xp, "exponent", 1.35f);

        var upgrades = systems.AddComponent<UpgradeService>();
        Set(upgrades, "choiceCount", 4);
        Set(upgrades, "fallback", data.Coins);
        Set(upgrades, "loadout", player.GetComponent<WeaponLoadout>());
        Set(upgrades, "stats", player.GetComponent<PlayerStats>());
        var poolProp = So(upgrades).FindProperty("pool");
        poolProp.arraySize = data.Pool.Count;
        for (int i = 0; i < data.Pool.Count; i++)
        {
            poolProp.GetArrayElementAtIndex(i).objectReferenceValue = data.Pool[i];
        }

        So(upgrades).ApplyModifiedPropertiesWithoutUndo();

        var waves = systems.AddComponent<WaveDirector>();
        Set(waves, "maxAliveEnemies", 140);
        Set(waves, "horizontalInset", 0.6f);
        Set(waves, "endlessWave", data.Endless);
        var waveList = So(waves).FindProperty("waves");
        waveList.arraySize = 3;
        waveList.GetArrayElementAtIndex(0).objectReferenceValue = data.Wave1;
        waveList.GetArrayElementAtIndex(1).objectReferenceValue = data.Wave2;
        waveList.GetArrayElementAtIndex(2).objectReferenceValue = data.Wave3;
        So(waves).ApplyModifiedPropertiesWithoutUndo();

        var propsSpawner = systems.AddComponent<PropSpawner>();
        var breakables = So(propsSpawner).FindProperty("breakables");
        breakables.arraySize = 2;
        SetPropEntry(breakables, 0, props.Pot, 70);
        SetPropEntry(breakables, 1, props.Chest, 30);
        So(propsSpawner).FindProperty("breakableInterval").vector2Value = new Vector2(2.5f, 5f);
        var decorations = So(propsSpawner).FindProperty("decorations");
        decorations.arraySize = 2;
        SetPropEntry(decorations, 0, props.Tree, 60);
        SetPropEntry(decorations, 1, props.Rock, 40);
        So(propsSpawner).FindProperty("decorationInterval").vector2Value = new Vector2(0.6f, 1.6f);
        So(propsSpawner).ApplyModifiedPropertiesWithoutUndo();

        var canvas = BuildCanvas(art, player, input);
        BuildEventSystem();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static void Prewarm(SerializedProperty list, int index, GameObject prefab, int count)
    {
        var el = list.GetArrayElementAtIndex(index);
        el.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        el.FindPropertyRelative("count").intValue = count;
    }

    static void SetPropEntry(SerializedProperty list, int index, GameObject prefab, float weight)
    {
        var el = list.GetArrayElementAtIndex(index);
        el.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        el.FindPropertyRelative("weight").floatValue = weight;
    }

    static GameObject BuildCanvas(Art art, GameObject player, PlayerInputReader input)
    {
        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(360f, 640f);
        scaler.matchWidthOrHeight = 0.5f;

        var hud = Panel("HUD", canvasGo.transform, new Vector2(0f, 0.78f), Vector2.one, Color.clear);
        var healthBg = Bar("HealthBar", hud, new Vector2(16, -18), new Vector2(220, 16), new Color(0.2f, 0.08f, 0.08f));
        var healthFill = Fill(healthBg, art.White, new Color(0.85f, 0.2f, 0.22f));
        var xpBg = Bar("XpBar", hud, new Vector2(16, -38), new Vector2(220, 10), new Color(0.08f, 0.12f, 0.2f));
        var xpFill = Fill(xpBg, art.White, new Color(0.35f, 0.75f, 1f));
        var healthLabel = Label("HealthLabel", hud, new Vector2(16, -58), "120 / 120", 12, TextAlignmentOptions.MidlineLeft);
        var levelLabel = Label("LevelLabel", hud, new Vector2(250, -18), "Lv 1", 14, TextAlignmentOptions.MidlineRight);
        var timerLabel = Label("TimerLabel", hud, new Vector2(250, -38), "00:00", 14, TextAlignmentOptions.MidlineRight);
        var coinLabel = Label("CoinLabel", hud, new Vector2(16, -76), "0", 12, TextAlignmentOptions.MidlineLeft);
        var killLabel = Label("KillLabel", hud, new Vector2(120, -76), "0", 12, TextAlignmentOptions.MidlineLeft);
        var hudView = hud.gameObject.AddComponent<HudView>();
        Set(hudView, "healthFill", healthFill);
        Set(hudView, "xpFill", xpFill);
        Set(hudView, "healthLabel", healthLabel);
        Set(hudView, "levelLabel", levelLabel);
        Set(hudView, "timerLabel", timerLabel);
        Set(hudView, "coinLabel", coinLabel);
        Set(hudView, "killLabel", killLabel);
        Set(hudView, "playerHealth", player.GetComponent<PlayerHealth>());
        healthBg.gameObject.SetActive(false);

        var joystickZone = Panel("Joystick", canvasGo.transform, new Vector2(0f, 0f), new Vector2(1f, 0.55f), new Color(1f, 1f, 1f, 0f));
        joystickZone.GetComponent<Image>().raycastTarget = true;
        var background = ImageGo("Background", joystickZone, art.Ring, new Vector2(0f, 80f), new Vector2(150f, 150f));
        var handle = ImageGo("Handle", background, art.Circle, Vector2.zero, new Vector2(70f, 70f));
        var joystick = joystickZone.gameObject.AddComponent<VirtualJoystick>();
        Set(joystick, "background", background);
        Set(joystick, "handle", handle);
        Set(joystick, "handleRange", 70f);
        Set(joystick, "deadZone", 0.15f);
        Set(joystick, "floatingOrigin", true);
        Set(joystick, "hideWhenIdle", true);
        Set(joystick, "idleAlpha", 0.25f);
        Set(input, "joystick", joystick);

        BuildPlayerWorldHealthBar(canvasGo.transform, player, art.White);

        var levelUp = Panel("LevelUpPanel", canvasGo.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.72f));
        levelUp.GetComponent<Image>().raycastTarget = true;
        var levelTitle = Label("LevelTitle", levelUp, new Vector2(0, 250), "Level 2", 22, TextAlignmentOptions.Center);
        Stretch(levelTitle.rectTransform, new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.92f));
        var cards = new UpgradeCardView[4];
        float[] y = { 140, 20, -100, -220 };
        for (int i = 0; i < 4; i++)
        {
            cards[i] = UpgradeCard(levelUp, new Vector2(0, y[i]), art.White);
        }

        var levelPanel = levelUp.gameObject.AddComponent<LevelUpPanel>();
        Set(levelPanel, "root", levelUp.gameObject);
        Set(levelPanel, "levelLabel", levelTitle);
        var cardProp = So(levelPanel).FindProperty("cards");
        cardProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            cardProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        }

        So(levelPanel).ApplyModifiedPropertiesWithoutUndo();

        var gameOver = Panel("GameOverPanel", canvasGo.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.78f));
        Label("Title", gameOver, new Vector2(0, 180), "Game Over", 28, TextAlignmentOptions.Center);
        var time = Label("TimeLabel", gameOver, new Vector2(0, 90), "00:00", 18, TextAlignmentOptions.Center);
        var level = Label("LevelValue", gameOver, new Vector2(0, 50), "1", 18, TextAlignmentOptions.Center);
        var kills = Label("KillsLabel", gameOver, new Vector2(0, 10), "0", 18, TextAlignmentOptions.Center);
        var coins = Label("CoinsLabel", gameOver, new Vector2(0, -30), "0", 18, TextAlignmentOptions.Center);
        var restart = ButtonGo("Restart", gameOver, new Vector2(0, -110), "Restart");
        var menu = ButtonGo("Menu", gameOver, new Vector2(0, -170), "Menu");
        var over = gameOver.gameObject.AddComponent<GameOverPanel>();
        Set(over, "root", gameOver.gameObject);
        Set(over, "timeLabel", time);
        Set(over, "levelLabel", level);
        Set(over, "killsLabel", kills);
        Set(over, "coinsLabel", coins);
        Set(over, "restartButton", restart);
        Set(over, "menuButton", menu);

        return canvasGo;
    }

    static void BuildPlayerWorldHealthBar(Transform canvas, GameObject player, Sprite white)
    {
        var root = ImageGo("PlayerHealthBar", canvas, white, Vector2.zero, new Vector2(132f, 22f));
        root.GetComponent<Image>().color = Color.black;
        root.GetComponent<Image>().raycastTarget = false;

        var bg = ImageGo("Background", root, white, Vector2.zero, new Vector2(126f, 16f));
        bg.GetComponent<Image>().color = new Color(0.08f, 0.02f, 0.02f, 1f);
        bg.GetComponent<Image>().raycastTarget = false;

        var fillRt = ImageGo("Fill", bg, white, Vector2.zero, new Vector2(126f, 16f));
        var fill = fillRt.GetComponent<Image>();
        fill.color = new Color(0.22f, 0.86f, 0.32f, 1f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 1f;
        fill.raycastTarget = false;

        var bar = root.gameObject.AddComponent<PlayerWorldHealthBar>();
        Set(bar, "health", player.GetComponent<PlayerHealth>());
        Set(bar, "followRenderer", player.GetComponent<SpriteRenderer>());
        Set(bar, "fill", fill);
    }

    static UpgradeCardView UpgradeCard(RectTransform parent, Vector2 pos, Sprite white)
    {
        var rt = ImageGo("Card", parent, white, pos, new Vector2(300, 100));
        rt.GetComponent<Image>().color = new Color(0.16f, 0.16f, 0.2f, 0.95f);
        rt.GetComponent<Image>().raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        var icon = ImageGo("Icon", rt, white, new Vector2(-110, 0), new Vector2(48, 48));
        var title = Label("Title", rt, new Vector2(20, 24), "Upgrade", 16, TextAlignmentOptions.MidlineLeft);
        Stretch(title.rectTransform, new Vector2(0.28f, 0.55f), new Vector2(0.95f, 0.92f));
        var desc = Label("Description", rt, new Vector2(20, -10), "Description", 12, TextAlignmentOptions.TopLeft);
        Stretch(desc.rectTransform, new Vector2(0.28f, 0.08f), new Vector2(0.95f, 0.58f));
        var banner = Label("Banner", rt, new Vector2(120, 32), "NEW", 12, TextAlignmentOptions.MidlineRight);
        var view = rt.gameObject.AddComponent<UpgradeCardView>();
        Set(view, "button", button);
        Set(view, "iconImage", icon.GetComponent<Image>());
        Set(view, "titleLabel", title);
        Set(view, "descriptionLabel", desc);
        Set(view, "bannerLabel", banner);
        return view;
    }

    static void BuildEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    static void AddSceneToBuild()
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == ScenePath))
        {
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static GameObject Body(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return go;
    }

    static GameObject SavePrefab(GameObject go, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }

    static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt, min, max);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = color.a > 0.01f;
        return rt;
    }

    static RectTransform Bar(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return rt;
    }

    static Image Fill(RectTransform parent, Sprite sprite, Color color)
    {
        var go = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt, Vector2.zero, Vector2.one);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillAmount = 0f;
        image.raycastTarget = false;
        return image;
    }

    static TMP_Text Label(string name, Transform parent, Vector2 pos, string text, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(280f, 32f);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    static RectTransform ImageGo(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.raycastTarget = false;
        return rt;
    }

    static Button ButtonGo(string name, Transform parent, Vector2 pos, string text)
    {
        var rt = ImageGo(name, parent, null, pos, new Vector2(200f, 44f));
        rt.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.28f);
        rt.GetComponent<Image>().raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        Label("Label", rt, Vector2.zero, text, 16, TextAlignmentOptions.Center);
        return button;
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static SerializedObject So(UnityEngine.Object obj) => new SerializedObject(obj);

    static void Set(UnityEngine.Object obj, string field, UnityEngine.Object value)
    {
        var so = So(obj);
        var prop = so.FindProperty(field);
        if (prop != null)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void Set(UnityEngine.Object obj, string field, float value)
    {
        var so = So(obj);
        var prop = so.FindProperty(field);
        if (prop != null)
        {
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void Set(UnityEngine.Object obj, string field, int value)
    {
        var so = So(obj);
        var prop = so.FindProperty(field);
        if (prop != null)
        {
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void Set(UnityEngine.Object obj, string field, bool value)
    {
        var so = So(obj);
        var prop = so.FindProperty(field);
        if (prop != null)
        {
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void Set(UnityEngine.Object obj, string field, string value)
    {
        var so = So(obj);
        var prop = so.FindProperty(field);
        if (prop != null)
        {
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void SetIfExists(UnityEngine.Object obj, string field, int value)
    {
        var so = So(obj);
        var prop = so.FindProperty(field);
        if (prop != null)
        {
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void SetIfExists(UnityEngine.Object obj, string field, bool value)
    {
        var so = So(obj);
        var prop = so.FindProperty(field);
        if (prop != null)
        {
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
