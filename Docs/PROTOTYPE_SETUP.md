# King's Rescue - Vertical Survivors Prototype Setup

Click-by-click wiring for the gameplay scripts in `Assets/Scripts/Game/`.
Everything below is done in the Unity Editor; the C# is already written.

Unity `6000.4.6f1`, 2D URP, Input System only (`activeInputHandler: 1`).

---

## 0. Blocker to clear first

Two scripts that were dropped into `Assets/Scripts/` will not compile, and while
they are broken **nothing** in the project compiles, including this prototype:

- `Assets/Scripts/Object Manage/ObjectPoolManager.cs` needs `DG.Tweening` (DOTween), which is not in this project.
- `Assets/Scripts/Misc/SoftwareCursor.cs` references a missing `DoorHandler` type.

Delete both folders, import DOTween and the missing type, or move them outside
`Assets/`. The prototype does not use either of them - pooling is handled by
`KingsRescue.Core.PoolManager`.

---

## 1. Architecture you need to know before wiring

**Nothing needs colliders.** Combat, pickups and contact damage all run on
squared-distance checks over registries (`Enemy.Active`, `Pickup.Active`,
`HitRegistry.Targets`), not physics. So:

- Enemies, props, projectiles, orbit blades and pickups need **no Collider2D and no Rigidbody2D**.
- The player needs a `Rigidbody2D` only because `PlayerController` uses `MovePosition`. Set it Kinematic.
- There is no layer matrix to configure.

The "body size" of a target is `Health.hitRadius`, and the reach of an attack is
its area value. They are added together, so a big enemy is easier to clip.

**The camera never moves.** `ScrollDirector` publishes one downward speed and
everything reads it. `ArenaBounds` derives the play field from the camera, hard
clamps the player into it, and tells spawners and despawners where the edges are.

---

## 2. Scene skeleton

Create `Assets/Scenes/Gameplay.unity`.

### Main Camera

- Projection: Orthographic, **Size 10**, Position `(0, 0, -10)`, background dark.
- Add **Pixel Perfect Camera**: Assets Pixels Per Unit **16**, Reference Resolution **180 x 320**, Upscale Render Texture off, Pixel Snapping on.
- At 360x640 this gives a play field of roughly **11.25 x 20 world units**.

Set every gameplay sprite's import **Pixels Per Unit to 16** so the scale is
consistent. The Monster Creature Pack is already 16; `Heavy Knight` is currently
100 and must be changed.

### Global Light 2D

Copy the one from `SampleScene` or add a new 2D Global Light, intensity 1.

### `Systems` (empty GameObject at origin)

Add these components, in this order:

1. **ArenaBounds** - Target Camera: Main Camera. Padding Left/Right `1`, Top `2`, Bottom `1.5`. Spawn Margin `2.5`, Despawn Margin `3`.
2. **ScrollDirector** - Base Speed `3`, Speed Gain Per Minute `0.9`, Max Speed `7`.
3. **PoolManager** - leave Prewarm empty for now; fill it in during the tuning pass.
4. **GameManager** - Player Health: the Player object. Music Controller: optional, see section 8. Menu Scene Name: `SampleScene`.
5. **PlayerInputReader** - Joystick: the `Joystick` object from the Canvas (section 6). Action Asset: `Assets/InputSystem_Actions` (optional; without it, WASD/arrows/gamepad still work through the built-in fallback).
6. **ExperienceSystem** - Stats: the Player. Base Xp `5`, Growth `2.4`, Exponent `1.35`.
7. **UpgradeService** - see section 7.
8. **WaveDirector** - see section 5.
9. **PropSpawner** - see section 5.

### `Background`

3D Object > Quad, scaled to about `(12, 21, 1)` at z `10`.
Material: `Universal Render Pipeline/Unlit`, Base Map = a ground tile texture with
Wrap Mode **Repeat**, tiling set so the tiles look right (try `(6, 10)`).
Add **ScrollingBackground**: Parallax Factor `1`, Tiles Per World Unit `0.5`,
Texture Property `_BaseMap`.

---

## 3. Player prefab

Root `Player`, at `(0, -5, 0)`, sorting layer above the background.

| Component | Settings |
| --- | --- |
| `SpriteRenderer` | A Heavy Knight idle frame |
| `Animator` | Optional. Needs a float parameter named `Speed` if used |
| `Rigidbody2D` | Body Type **Kinematic**, Gravity Scale 0, Freeze Rotation Z |
| `PlayerStats` | Base Overrides: `MoveSpeed = 6`, `MaxHp = 120`, `MagnetRadius = 2.5` |
| `PlayerController` | Acceleration `90`, Forward Speed Factor `1`. Animator and Sprite Renderer auto-resolve |
| `PlayerHealth` | Invulnerability Duration `0.5`, Flash Target = the SpriteRenderer |
| `PickupMagnet` | Scan Interval `0.08` |
| `WeaponLoadout` | Starting Weapon = `Weapon_Sword`, Max Weapons `6`, Max Passives `6` |

Weapons create their own child objects at runtime; do not add `WeaponRuntime` by hand.

---

## 4. Combat and world prefabs

Save all of these under `Assets/Prefabs/`.

### Enemy prefabs (`Enemy_Orc`, `Enemy_Goblin`, `Enemy_Skelly`)

From `Assets/Sprites/Monster Creature Pack/`. Each needs:

- `SpriteRenderer` (+ optional `Animator`)
- `Health` - Hit Radius `0.5` (raise for bigger bodies). Max Health is overwritten by the definition
- `Enemy`
- `EnemyMovement` - Chase Slack `1`, Speed Jitter `0.12`
- `ContactDamage` - Player Radius `0.35`. The rest comes from the definition
- `EnemyDropper` - Xp Gem Prefab `Pickup_XpGem`, Coin Prefab `Pickup_Coin`, Health Prefab `Pickup_Health`
- `OffscreenDespawner` - Check Interval `0.2`
- `HitFlash` - optional but makes crowds readable

### `Projectile_Arrow`

From `warriors_rogues_mages/Generic Weapon Attack Effects`.
`SpriteRenderer` + `Projectile` (Hit Radius `0.3`, Rotate To Direction on).

### `OrbitBlade_Sword`

`SpriteRenderer` + `OrbitBlade` (Hit Radius `0.45`, Spin Sprite on).

### `FX_SwordSwing`, `FX_AuraPulse`

A slash or burst sprite plus `AutoDespawn` (Lifetime `0.25`).
`WeaponRuntime` scales these by the weapon's area, so author them at 1 world unit.

### Pickups

- `Pickup_XpGem` - `SpriteRenderer` + `XpGem`. Collect Radius `0.4`, Max Attract Speed `20`. Optional tiers: `{min 1, cyan, 1.0}`, `{min 5, green, 1.25}`, `{min 20, magenta, 1.6}`
- `Pickup_Coin` - `SpriteRenderer` + `CoinPickup`, Value `1`
- `Pickup_Health` - `SpriteRenderer` + `HealthPickup`, Flat Heal `15`, Percent Of Max `0.1`

### Props

`Prop_Pot`, `Prop_Chest` (use the animated chest sheet from Lonesome Forest):

- `SpriteRenderer`
- `Health` - Max Health `1` for pots, `6` for chests. Hit Radius `0.4`
- `DestructibleProp` - Coin Prefab `Pickup_Coin`, Min/Max Coins `2`/`5` for pots and `6`/`12` for chests, Health Prefab `Pickup_Health`, Health Drop Chance `0.08`
- `Scrollable` - Scroll Influence `1`
- `OffscreenDespawner`

Decorative props (trees, rocks) only need `SpriteRenderer` + `Scrollable` + `OffscreenDespawner`.

---

## 5. Data assets

Create these via **Assets > Create > King's Rescue**. Put them in `Assets/Data/`.

### Enemy Definitions

- `Enemy_Orc` - Prefab `Enemy_Orc`, Max Health `14`, Contact Damage `9`, Contact Radius `0.5`, Move Speed `2.4`, Scroll Influence `0.8`, Xp Value `1`, Coin Drop Chance `0.05`
- `Enemy_Goblin` - Max Health `8`, Contact Damage `6`, Move Speed `3.6`, Scroll Influence `0.5`, Xp Value `1`
- `Enemy_Skelly` - Max Health `26`, Contact Damage `14`, Move Speed `1.8`, Scroll Influence `1`, Xp Value `3`, Coin Drop Chance `0.12`

Health and contact damage grow automatically via Health/Damage Growth Per Minute.

### Wave Definitions

- `Wave_01` - Start `0`, Duration `60`, Spawn Interval `1.4`, Spawns Per Tick `1`, Pattern `TopRandom`, enemies: Orc weight 100
- `Wave_02` - Start `45`, Duration `75`, Spawn Interval `1.0`, Spawns Per Tick `2`, enemies: Orc 60, Goblin 40. Burst at `30s`: 8 Goblins, pattern `TopLine`
- `Wave_03` - Start `110`, Duration `90`, Spawn Interval `0.8`, Spawns Per Tick `2`, enemies: Orc 40, Goblin 40, Skelly 20. Burst at `45s`: 12 Skellies, pattern `SurroundPlayer`
- `Wave_Endless` - Spawn Interval `0.6`, Spawns Per Tick `3`, all three enemies. Assign this to the **Endless Wave** slot, not the list. It activates once every authored wave has expired so the run never goes quiet.

On `WaveDirector`: Waves = the three numbered assets, Max Alive Enemies `140`,
Horizontal Inset `0.6`.

On `PropSpawner`: Breakables = `Prop_Pot` weight 70, `Prop_Chest` weight 30,
Breakable Interval `(2.5, 5)`. Decorations = trees and rocks, Interval `(0.6, 1.6)`.

### Weapon Definitions

`Weapon_Sword` (the starting weapon, Fire Mode **MeleeSwing**, Effect Prefab `FX_SwordSwing`):

- Level 1: Damage `12`, Cooldown `0.85`, Count `1`, Area `2.2`, Arc `150`, Lifetime `0.25`
- Level 2: Damage `16`, Arc `170`, text "Wider, heavier swing"
- Level 3: Damage `16`, Count `2`, text "Two-hit combo"
- Level 4: Damage `22`, Area `2.8`, text "Longer reach"
- Level 5: Damage `30`, Count `3`, Arc `360`, text "Full spin. Hits everything around you"

`Weapon_Bow` (Fire Mode **ForwardShot**, Projectile Prefab `Projectile_Arrow`):

- Level 1: Damage `9`, Cooldown `0.7`, Count `1`, Projectile Speed `14`, Lifetime `1.8`, Pierce `0`, Spread `14`
- Level 2: Count `2`
- Level 3: Pierce `1`
- Level 4: Damage `14`, Count `3`, Spread `22`
- Level 5: Pierce `3`, Cooldown `0.5`

`Weapon_Orbit` (Fire Mode **Orbit**, Orbit Prefab `OrbitBlade_Sword`):

- Level 1: Damage `8`, Count `1`, Area `1.9`, Orbit Speed `0.7`, Rehit Interval `0.45`
- Level 2: Count `2`
- Level 3: Damage `12`, Orbit Speed `0.95`
- Level 4: Count `3`, Area `2.4`
- Level 5: Count `4`, Damage `18`

`Weapon_Aura` (Fire Mode **Aura**, Effect Prefab `FX_AuraPulse`):

- Level 1: Damage `6`, Cooldown `1.2`, Area `2.3`
- Level 2: Damage `9`
- Level 3: Area `3.0`
- Level 4: Damage `14`, Cooldown `0.9`
- Level 5: Area `3.8`, Damage `20`

Note: the Extra Projectiles stat adds to Count for every weapon with
**Uses Global Projectile Count** ticked. That means it gives the sword extra
combo swings and the orbit extra blades, which is intentional.

### Passive Definitions

Six is enough for a prototype. Each level holds a list of stat modifiers.

- `Passive_Boots` - `MoveSpeed`, Percent, `+0.12` per level, 5 levels
- `Passive_Gauntlet` - `Damage`, Percent, `+0.15` per level, 5 levels
- `Passive_Hourglass` - `AttackSpeed`, Percent, `+0.12` per level, 5 levels
- `Passive_Lodestone` - `MagnetRadius`, Percent, `+0.35` per level, 3 levels
- `Passive_Plate` - `MaxHp` Flat `+30` and `Armor` Flat `+1` per level, 4 levels
- `Passive_Tome` - `XpGain`, Percent, `+0.15` per level, 3 levels

### Upgrade Definitions

One per card. Weapons need **two** assets each, because "unlock" and "level up"
are different offers with different availability rules:

- `Upgrade_Sword_Level` - Kind `WeaponLevel`, Weapon `Weapon_Sword`, Weight `100`
- `Upgrade_Bow_New` - Kind `NewWeapon`, Weapon `Weapon_Bow`, Weight `100`
- `Upgrade_Bow_Level` - Kind `WeaponLevel`, Weapon `Weapon_Bow`, Weight `100`
- ...and the same pair for `Weapon_Orbit` and `Weapon_Aura`
- One `Kind = Passive` asset per passive, Weight `80`
- `Upgrade_Coins` - Kind `CoinBonus`, Coin Amount `25`. This is the **Fallback**, not a pool entry

The sword has no `NewWeapon` entry because the hero starts with it.

---

## 6. Canvas (portrait)

Canvas: Render Mode **Screen Space - Overlay**, `Canvas Scaler` set to
**Scale With Screen Size**, Reference Resolution `360 x 640`, Match `0.5`.
Make sure an `EventSystem` exists with the **Input System UI Input Module**.

### `HUD` (top of screen)

- `HealthBar/Fill` - Image, Image Type **Filled**, Fill Method Horizontal
- `XpBar/Fill` - same
- TMP labels for health text, level, timer, coins, kills
- Put `HudView` on the `HUD` object and drag all of the above in

### `Joystick` (bottom half)

- Full-width RectTransform covering roughly the bottom 55% of the screen
- An `Image` with alpha `0` and **Raycast Target on** - this is the touch zone
- Child `Background` - the ring sprite, ~150 px, anchored to the zone centre
- Grandchild `Handle` - the knob sprite, ~70 px, centred in Background
- `VirtualJoystick` on the zone object: Background, Handle, Handle Range `70`, Dead Zone `0.15`, Floating Origin **on**, Hide When Idle **on**, Idle Alpha `0.25`
- Drag this object into `PlayerInputReader.joystick` on `Systems`

Floating origin means the stick appears wherever the thumb lands, which is what
makes one-handed play work without looking at the screen.

### `LevelUpPanel`

Full-screen dimmed background, a "Level N" title, and a vertical layout of
**4 cards**. Each card is a `Button` with an icon `Image`, title `TMP_Text`,
description `TMP_Text` and a small banner `TMP_Text`, plus `UpgradeCardView`.

Put `LevelUpPanel` on the panel root and assign Root (itself is fine), the four
cards and the level label. Leave the panel **active** in the scene - the script
hides itself in `Start` after subscribing.

### `GameOverPanel`

Same pattern: run time, level, kills and coins labels, a Restart button and a
Menu button, with `GameOverPanel` on the root. Leave it active in the scene.

---

## 7. Finish the `Systems` wiring

`UpgradeService`: Pool = every `Upgrade_*` asset except the coin one,
Fallback = `Upgrade_Coins`, Choice Count `4`, Loadout and Stats = the Player.

---

## 8. Audio

`GameManager` calls the existing `MusicController` on state changes: gameplay
music once when the run starts, game-over music on death. Resuming from a
level-up does not restart the track.

Either drag the `Music Controller` object from `SampleScene` into this scene
along with its two FMOD emitters and the FMOD Listener, or leave the field empty
and wire audio later.

---

## 9. First playtest checklist

1. Add `Gameplay` to Build Settings.
2. Press Play. You should be able to steer with WASD immediately, even before the joystick is wired.
3. Select `Systems` and look at the Scene view gizmos: cyan is the screen, green is the player-legal rect, yellow is the spawn line, red is the despawn line.
4. Confirm enemies drift in from the top, turn toward you once on screen, and vanish below the red line.
5. Confirm the sword swings on its own toward the nearest enemy.
6. Confirm gems fly in at the magnet radius and the EXP bar fills.
7. Confirm the level-up panel freezes the game and the picked upgrade actually changes something.

---

## 10. Tuning knobs, in the order you will reach for them

- `ScrollDirector.baseSpeed` - the single biggest lever on how the game feels
- `ArenaBounds` padding - how much room the player has to dodge
- `EnemyDefinition.scrollInfluence` - below 1 lets an enemy fight the current and stay on you; at 1 it always drifts off the bottom eventually
- `WaveDefinition.spawnInterval` and `spawnsPerTick` - pressure
- `ExperienceSystem.growth` - how often the level-up screen interrupts
- `WaveDirector.maxAliveEnemies` - drop it if WebGL frame time suffers

---

## 11. WebGL build

Web resolution is already set to `360 x 640` in Player Settings.

- Fill in `PoolManager.prewarm` before building: roughly 80 of each enemy prefab, 60 gems, 40 coins, 60 projectiles, 20 props. Prewarming removes the first-encounter hitch.
- Compression Format **Brotli**, Exception Support **None**, Strip Engine Code on.
- Verify the FMOD banks land in `StreamingAssets`, as they did for `Builds/Test`.
- Test in a mobile browser in portrait, not just the Game view. The joystick only proves itself under a real thumb.

---

## 12. Out of scope for this prototype

Boss fights, meta-progression and the coin shop, saving, character select,
multiple biomes, knockback (the `knockback` field is carried through `DamageInfo`
but nothing consumes it yet), and damage number popups.
