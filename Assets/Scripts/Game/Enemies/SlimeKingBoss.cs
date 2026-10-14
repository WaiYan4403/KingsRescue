using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// First boss. Water balls and the laser are used from the top of the road:
    /// a repeating forward volley, then a beam that starts on center and commits left or right.
    /// The vanish-slam is the close-range special.
    /// </summary>
    public class SlimeKingBoss : MonoBehaviour, IPoolable
    {
        private enum Phase
        {
            Intro,
            Idle,
            JumpAir,
            LaserWindup,
            Laser,
            Volley
        }

        private enum Special
        {
            Jump,
            Laser
        }

        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer landingMarker;
        [SerializeField] private Transform laserPivot;
        [SerializeField] private GameObject waterFoams;
        [SerializeField] private SpriteRenderer laserBeam;
        [SerializeField] private float playerRadius = 0.35f;

        [Header("Timing")]
        [SerializeField] private float introDuration = 1.35f;
        [SerializeField] private float idleMin = 1.7f;
        [SerializeField] private float idleMax = 2.4f;
        [SerializeField] private float jumpAirTime = 1.45f;
        [SerializeField] private float jumpLockLead = 0.4f;
        [SerializeField] private float laserWindup = 0.4f;
        [SerializeField] private float laserDuration = 3.1f;
        [SerializeField] private float perchMoveTime = 0.45f;
        [Tooltip("How far below the top of the play area the king stands for volley and laser.")]
        [SerializeField] private float perchTopPadding = 1.8f;

        [Header("Slam")]
        [SerializeField] private float slamRadius = 1.5f;
        [SerializeField] private float slamDamage = 18f;

        [Header("Laser")]
        [SerializeField] private float laserLength = 7.5f;
        [SerializeField] private float laserWidth = 0.55f;
        [SerializeField] private float laserRotateSpeed = 52f;
        [SerializeField] private float laserSweepArc = 140f;
        [SerializeField] private float laserDamage = 9f;
        [SerializeField] private float laserTick = 0.28f;

        [Header("Water Volley")]
        [SerializeField] private GameObject waterBallPrefab;
        [SerializeField] private int volleyCount = 3;
        [SerializeField] private float volleySpread = 18f;
        [SerializeField] private float volleyWindup = 0.35f;
        [SerializeField] private int volleyBursts = 3;
        [Tooltip("Balls per burst. First 4, then 5 in the gaps, then 4 again.")]
        [SerializeField] private int[] volleyPattern = { 4, 5, 4 };
        [SerializeField] private float volleyCooldown = 0.7f;
        [SerializeField] private float volleyRecover = 0.4f;
        [SerializeField] private float volleyDamage = 10f;
        [SerializeField] private float volleySpeed = 7.5f;
        [SerializeField] private float volleyLifetime = 3.2f;
        [SerializeField] private float volleyRadius = 0.32f;
        [SerializeField] private float volleyRange = 8f;

        [Header("Death Split")]
        [SerializeField] private EnemyDefinition splitSlime;
        [SerializeField] private int splitCount = 6;
        [SerializeField] private float splitRadius = 1.1f;

        private EnemyMovement movement;
        private ContactDamage contact;
        private Health health;
        private Animator animator;
        private EnemyDefinition definition;
        private Phase phase;
        private float phaseEnds;
        private Vector2 slamPoint;
        private bool slamLocked;
        private bool nextIsVolley = true;
        private Special nextSpecial = Special.Jump;
        private int volleysFired;
        private bool perched;
        private float nextLaserTick;
        private float laserFromAngle;
        private float laserToAngle;
        private float laserSweepStarted;
        private SpriteRenderer[] laserSections = System.Array.Empty<SpriteRenderer>();
        private Vector2 perchFrom;
        private Vector2 perchTo;
        private float perchStarted;
        private bool musicOn;
        private float introFromY;
        private float introToY;
        private float introStarted;
        private float runMinutes;
        private bool splitSpawned;
        private static readonly int AttackHash = Animator.StringToHash("Attack1");
        private const float DownAngle = -180f;

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponent<SpriteRenderer>();
            }

            movement = GetComponent<EnemyMovement>();
            contact = GetComponent<ContactDamage>();
            health = GetComponent<Health>();
            animator = GetComponent<Animator>();
        }

        public string StatusLine
        {
            get
            {
                switch (phase)
                {
                    case Phase.Intro:
                        return "entering";
                    case Phase.Idle:
                        return "waiting";
                    case Phase.JumpAir:
                        return slamLocked ? "vanish slam" : "jumping";
                    case Phase.LaserWindup:
                        return "laser windup";
                    case Phase.Laser:
                        return "laser sweep";
                    case Phase.Volley:
                        return perched ? "water volley" : "moving to perch";
                    default:
                        return "idle";
                }
            }
        }

        public void OnSpawned()
        {
            musicOn = false;
            nextIsVolley = true;
            nextSpecial = Special.Jump;
            volleysFired = 0;
            perched = false;
            splitSpawned = false;
            HideMarker();
            SetLaserVisible(false);
            SetAttackPose(false);
        }

        public void OnDespawned()
        {
            RestoreGameplayMusic();
            GameManager.Instance?.EndBossFight();
            if (health != null)
            {
                health.Untargetable = false;
            }

            HideMarker();
            SetLaserVisible(false);
            SetAttackPose(false);
        }

        public void Configure(EnemyDefinition def, float runMinutes)
        {
            bool isKing = def != null && def.isBoss;
            enabled = isKing;
            if (!isKing)
            {
                return;
            }

            definition = def;
            this.runMinutes = runMinutes;
            float scaled = def.ScaledAttackDamage(runMinutes);
            slamDamage = scaled;
            laserDamage = Mathf.Max(6f, scaled * 0.55f);
            volleyDamage = Mathf.Max(7f, scaled * 0.65f);
            if (def.projectileSpeed > 0.1f)
            {
                volleySpeed = def.projectileSpeed;
            }

            if (def.projectileLifetime > 0.1f)
            {
                volleyLifetime = def.projectileLifetime;
            }

            if (def.projectileRadius > 0.05f)
            {
                volleyRadius = def.projectileRadius;
            }

            if (def.projectileMaxDistance > 0.1f)
            {
                volleyRange = def.projectileMaxDistance;
            }

            if (waterBallPrefab == null)
            {
                waterBallPrefab = def.projectilePrefab;
            }

            if (health != null)
            {
                health.Untargetable = false;
            }

            EnemyAttacker attacker = GetComponent<EnemyAttacker>();
            if (attacker != null)
            {
                attacker.enabled = false;
            }

            HideMarker();
            SetLaserVisible(false);
            BeginIntro();
            PlayBossMusic();
            GameManager.Instance?.BeginBossFight();
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying)
            {
                return;
            }

            switch (phase)
            {
                case Phase.Intro:
                    TickIntro();
                    break;
                case Phase.Idle:
                    ClampToPlay();
                    if (Time.time >= phaseEnds)
                    {
                        ChooseNextAttack();
                    }

                    break;
                case Phase.JumpAir:
                    TickJump();
                    break;
                case Phase.LaserWindup:
                    TickLaserWindup();
                    break;
                case Phase.Laser:
                    TickLaser();
                    break;
                case Phase.Volley:
                    TickVolley();
                    break;
            }
        }

        private void LateUpdate()
        {
            if (phase == Phase.JumpAir || phase == Phase.Volley || phase == Phase.LaserWindup || phase == Phase.Laser)
            {
                return;
            }

            ClampToPlay();
        }

        private void BeginIntro()
        {
            phase = Phase.Intro;
            introStarted = Time.time;
            phaseEnds = Time.time + introDuration;
            SetMove(false);
            SetContact(false);
            SetVisible(true);

            ArenaBounds bounds = ArenaBounds.Instance;
            introFromY = transform.position.y;
            introToY = bounds != null ? bounds.PlayRect.center.y + 1.2f : transform.position.y - 4f;
        }

        private void TickIntro()
        {
            float t = Mathf.Clamp01((Time.time - introStarted) / Mathf.Max(0.05f, introDuration));
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(introFromY, introToY, t * t);
            transform.position = p;
            if (t >= 1f)
            {
                BeginIdle();
            }
        }

        private void BeginIdle()
        {
            phase = Phase.Idle;
            phaseEnds = Time.time + Random.Range(idleMin, idleMax);
            SetMove(true);
            SetContact(true);
            SetVisible(true);
            if (health != null)
            {
                health.Untargetable = false;
            }

            HideMarker();
            SetLaserVisible(false);
            SetAttackPose(false);
        }

        private void ChooseNextAttack()
        {
            if (nextIsVolley)
            {
                BeginVolley();
            }
            else if (nextSpecial == Special.Jump)
            {
                BeginJump();
                nextSpecial = Special.Laser;
            }
            else
            {
                BeginLaserWindup();
                nextSpecial = Special.Jump;
            }

            nextIsVolley = !nextIsVolley;
        }

        private void BeginVolley()
        {
            phase = Phase.Volley;
            volleysFired = 0;
            perched = false;
            StartPerchMove();
            SetMove(false);
            SetContact(true);
            SetVisible(true);
            SetLaserVisible(false);
            SetAttackPose(true);
        }

        private void TickVolley()
        {
            if (!perched)
            {
                if (!TickPerchMove())
                {
                    return;
                }

                perched = true;
                phaseEnds = Time.time + (volleyWindup > 0.05f ? volleyWindup : 0.35f);
                return;
            }

            if (Time.time < phaseEnds)
            {
                return;
            }

            int bursts = BurstCount();
            if (volleysFired < bursts)
            {
                ThrowWaterFan(BallsForBurst(volleysFired));
                volleysFired++;
                if (volleysFired < bursts)
                {
                    phaseEnds = Time.time + (volleyCooldown > 0.05f ? volleyCooldown : 0.7f);
                    return;
                }

                phaseEnds = Time.time + (volleyRecover > 0.05f ? volleyRecover : 0.4f);
                return;
            }

            SetAttackPose(false);
            BeginIdle();
        }

        private int BurstCount()
        {
            if (volleyPattern != null && volleyPattern.Length > 0)
            {
                return volleyPattern.Length;
            }

            return volleyBursts > 0 ? volleyBursts : 3;
        }

        private int BallsForBurst(int index)
        {
            if (volleyPattern != null && index >= 0 && index < volleyPattern.Length && volleyPattern[index] > 0)
            {
                return volleyPattern[index];
            }

            if (index == 1)
            {
                return 5;
            }

            return 4;
        }

        private void ThrowWaterFan(int count)
        {
            GameObject prefab = waterBallPrefab != null
                ? waterBallPrefab
                : definition != null ? definition.projectilePrefab : null;
            if (prefab == null)
            {
                return;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            Vector2 origin = laserPivot != null ? (Vector2)laserPivot.position : (Vector2)transform.position;
            count = count > 0 ? count : (volleyCount > 0 ? volleyCount : 4);
            float spread = volleySpread > 0.1f ? volleySpread : 40f;
            float speed = volleySpeed > 0.1f ? volleySpeed : 7.5f;
            float life = volleyLifetime > 0.1f ? volleyLifetime : 3.2f;
            float radius = volleyRadius > 0.05f ? volleyRadius : 0.32f;
            float range = volleyRange > 0.1f ? volleyRange : 12f;
            // Straight down the road, then fanned left/right like the Archer volley.
            const float forwardAngle = -90f;

            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                float offset = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
                float angle = (forwardAngle + offset) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                HostileProjectile shot = pool.Spawn(prefab, origin, Quaternion.identity)
                    ?.GetComponent<HostileProjectile>();
                if (shot == null)
                {
                    Debug.LogError("Slime King water ball prefab is missing HostileProjectile.", prefab);
                    return;
                }

                shot.Launch(
                    direction,
                    speed,
                    volleyDamage,
                    life,
                    radius,
                    1f,
                    0f,
                    false,
                    gameObject,
                    range);
            }
        }

        public void SpawnDeathSplit()
        {
            if (splitSpawned)
            {
                return;
            }

            splitSpawned = true;
            if (splitSlime == null || splitSlime.prefab == null)
            {
                return;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            int count = splitCount > 0 ? splitCount : 6;
            float radius = splitRadius > 0.1f ? splitRadius : 1.1f;
            Vector2 origin = transform.position;
            ArenaBounds bounds = ArenaBounds.Instance;

            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f + 0.25f;
                Vector2 spawn = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (bounds != null)
                {
                    spawn = bounds.ClampToPlayArea(spawn);
                }

                GameObject instance = pool.Spawn(splitSlime.prefab, spawn, Quaternion.identity);
                if (instance == null)
                {
                    continue;
                }

                Enemy child = instance.GetComponent<Enemy>();
                if (child == null)
                {
                    continue;
                }

                child.Configure(splitSlime, runMinutes);
                child.SetSpawnHeading(spawn, SpawnPattern.SurroundPlayer);
            }
        }

        private void SetAttackPose(bool attacking)
        {
            if (animator != null)
            {
                animator.SetBool(AttackHash, attacking);
            }
        }

        private void BeginJump()
        {
            phase = Phase.JumpAir;
            phaseEnds = Time.time + jumpAirTime;
            slamLocked = false;
            slamPoint = CurrentPlayerPoint();
            SetMove(false);
            SetContact(false);
            SetVisible(false);
            if (health != null)
            {
                health.Untargetable = true;
            }

            SetLaserVisible(false);
            ShowMarker(slamPoint);
        }

        private void TickJump()
        {
            float remaining = phaseEnds - Time.time;
            if (!slamLocked && remaining > jumpLockLead)
            {
                slamPoint = CurrentPlayerPoint();
            }
            else
            {
                slamLocked = true;
            }

            ShowMarker(slamPoint);
            PulseMarker();

            if (Time.time < phaseEnds)
            {
                return;
            }

            transform.position = slamPoint;
            SetVisible(true);
            if (health != null)
            {
                health.Untargetable = false;
            }

            Slam();
            HideMarker();
            BeginIdle();
        }

        private void Slam()
        {
            PlayerController player = PlayerController.Instance;
            if (player == null || player.Health == null || !player.Health.IsAlive)
            {
                return;
            }

            float reach = slamRadius + playerRadius;
            Vector2 delta = (Vector2)player.transform.position - slamPoint;
            if (delta.sqrMagnitude > reach * reach)
            {
                return;
            }

            Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.down;
            player.Health.TakeDamage(new DamageInfo(slamDamage, player.transform.position, direction, 1.4f, gameObject));
        }

        private void BeginLaserWindup()
        {
            phase = Phase.LaserWindup;
            perched = false;
            StartPerchMove();
            SetMove(false);
            SetContact(true);
            SetVisible(true);
            SetAttackPose(true);
            SetLaserVisible(false);
        }

        private void BeginLaser()
        {
            phase = Phase.Laser;
            laserSweepStarted = Time.time;
            phaseEnds = Time.time + (laserDuration > 0.2f ? laserDuration : 3.1f);
            nextLaserTick = 0f;
            ChooseLaserDirection();
            ApplyLaserAngle(laserFromAngle);
            //PulseLaser(0.92f);
        }

        private void TickLaserWindup()
        {
            if (!perched)
            {
                if (!TickPerchMove())
                {
                    return;
                }

                perched = true;
                laserFromAngle = DownAngle;
                laserToAngle = DownAngle;
                LayoutLaser();
                SetLaserVisible(true);
                ApplyLaserAngle(DownAngle);
                //PulseLaser(0.35f);
                phaseEnds = Time.time + (laserWindup > 0.05f ? laserWindup : 0.65f);
                return;
            }

            ApplyLaserAngle(DownAngle);
            //PulseLaser(0.35f);
            if (Time.time >= phaseEnds)
            {
                BeginLaser();
            }
        }

        private void TickLaser()
        {
            float duration = laserDuration > 0.2f ? laserDuration : 3.1f;
            float t = Mathf.Clamp01((Time.time - laserSweepStarted) / duration);
            ApplyLaserAngle(Mathf.Lerp(laserFromAngle, laserToAngle, t));
            //PulseLaser(0.75f + 0.2f * Mathf.Abs(Mathf.Sin(Time.time * 14f)));
            TryLaserHit();

            if (t >= 1f)
            {
                SetLaserVisible(false);
                SetAttackPose(false);
                BeginIdle();
            }
        }

        private void ChooseLaserDirection()
        {
            // Pivot.up is the beam. -180 is straight down the center of the road.
            // Commit to only one side so the other half of the field stays safe.
            float arc = laserSweepArc > 1f ? laserSweepArc : 70f;
            bool goRight = Random.value < 0.5f;
            laserFromAngle = DownAngle;
            laserToAngle = DownAngle + (goRight ? arc : -arc);
        }

        private void ApplyLaserAngle(float zDegrees)
        {
            if (laserPivot != null)
            {
                laserPivot.rotation = Quaternion.Euler(0f, 0f, zDegrees);
            }
        }

        private void TryLaserHit()
        {
            if (Time.time < nextLaserTick)
            {
                return;
            }

            PlayerController player = PlayerController.Instance;
            if (player == null || player.Health == null || !player.Health.IsAlive || player.Health.IsInvulnerable)
            {
                return;
            }

            Vector2 origin = transform.position;
            Vector2 beamDir = laserPivot != null ? (Vector2)laserPivot.up : Vector2.down;
            Vector2 end = origin + beamDir * ResolvedLaserLength();
            Vector2 closest = ClosestOnSegment(origin, end, player.transform.position);
            float reach = (laserWidth * 0.5f) + playerRadius;
            Vector2 delta = (Vector2)player.transform.position - closest;
            if (delta.sqrMagnitude > reach * reach)
            {
                return;
            }

            Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : -beamDir;
            player.Health.TakeDamage(new DamageInfo(laserDamage, player.transform.position, direction, 0.4f, gameObject));
            nextLaserTick = Time.time + laserTick;
        }

        private void LayoutLaser()
        {
            if (laserBeam == null)
            {
                return;
            }

            Transform root = laserBeam.transform.parent != null ? laserBeam.transform.parent : laserBeam.transform;
            laserSections = root.GetComponentsInChildren<SpriteRenderer>(true);
            Sprite sprite = laserBeam.sprite;
            float height = sprite != null ? Mathf.Max(0.01f, sprite.bounds.size.y) : 1f;
            float ppu = sprite != null ? Mathf.Max(1f, sprite.pixelsPerUnit) : 16f;
            // Exact edges open a seam once the pivot rotates. Two pixels of overlap covers it.
            float advance = Mathf.Max(0.01f, height - (2f / ppu));
            float length = ResolvedLaserLength();

            for (int i = 0; i < laserSections.Length; i++)
            {
                SpriteRenderer section = laserSections[i];
                if (section == null)
                {
                    continue;
                }

                section.enabled = i * advance < length;
                section.transform.localRotation = Quaternion.identity;
                // Pivot +Y is the beam. Each section is one sprite tall, stacked from the mouth outward.
                section.transform.localPosition = new Vector3(0f, (height * 0.5f) + (i * advance), 0f);
            }
        }

        private float ResolvedLaserLength()
        {
            float authored = laserLength > 0.1f ? laserLength : 12f;
            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds == null)
            {
                return authored;
            }

            float toBottom = transform.position.y - bounds.WorldRect.yMin + 1f;
            return Mathf.Max(authored, toBottom);
        }

        private void StartPerchMove()
        {
            perchFrom = transform.position;
            perchTo = TopCenterPerch();
            perchStarted = Time.time;
        }

        private bool TickPerchMove()
        {
            float duration = perchMoveTime > 0.05f ? perchMoveTime : 0.45f;
            float t = Mathf.Clamp01((Time.time - perchStarted) / duration);
            float s = t * t * (3f - 2f * t);
            transform.position = Vector2.Lerp(perchFrom, perchTo, s);
            return t >= 1f;
        }

        private Vector2 TopCenterPerch()
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds == null)
            {
                return transform.position;
            }

            Rect play = bounds.PlayRect;
            float pad = perchTopPadding > 0.01f ? perchTopPadding : 1.8f;
            float y = Mathf.Max(play.center.y, play.yMax - pad);
            return new Vector2(play.center.x, y);
        }

        private void ShowMarker(Vector2 world)
        {
            if (landingMarker == null)
            {
                return;
            }

            landingMarker.enabled = true;
            landingMarker.transform.position = new Vector3(world.x, world.y, 0f);
            float diameter = slamRadius * 2f;
            float native = landingMarker.sprite != null ? landingMarker.sprite.bounds.size.x : 1f;
            float scale = native > 0.0001f ? diameter / native : diameter;
            Vector2 parent = LossyAbs();
            landingMarker.transform.localScale = new Vector3(scale / parent.x, scale / parent.y, 1f);
        }

        private void PulseMarker()
        {
            if (landingMarker == null)
            {
                return;
            }

            Color c = landingMarker.color;
            c.a = 0.55f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 10f));
            landingMarker.color = c;
        }

        private void HideMarker()
        {
            if (landingMarker != null)
            {
                landingMarker.enabled = false;
            }
        }

        private void SetLaserVisible(bool visible)
        {
            if (laserPivot != null)
            {
                laserPivot.gameObject.SetActive(visible);
                waterFoams.SetActive(visible);
            }
            else if (laserBeam != null)
            {
                laserBeam.enabled = visible;
            }
        }

        private void PulseLaser(float alpha)
        {
            if (laserSections == null || laserSections.Length == 0)
            {
                if (laserBeam == null)
                {
                    return;
                }

                Color single = laserBeam.color;
                single.a = alpha;
                laserBeam.color = single;
                return;
            }

            for (int i = 0; i < laserSections.Length; i++)
            {
                SpriteRenderer section = laserSections[i];
                if (section == null)
                {
                    continue;
                }

                Color c = section.color;
                c.a = alpha;
                section.color = c;
            }
        }

        private void SetVisible(bool visible)
        {
            if (body != null)
            {
                body.enabled = visible;
            }
        }

        private void SetMove(bool on)
        {
            if (movement != null)
            {
                movement.enabled = on;
            }
        }

        private void SetContact(bool on)
        {
            if (contact != null)
            {
                contact.enabled = on;
            }
        }

        private void ClampToPlay()
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds == null)
            {
                return;
            }

            transform.position = bounds.ClampToPlayArea(transform.position);
        }

        private Vector2 LossyAbs()
        {
            Vector3 scale = transform.lossyScale;
            return new Vector2(Mathf.Max(0.01f, Mathf.Abs(scale.x)), Mathf.Max(0.01f, Mathf.Abs(scale.y)));
        }

        private static Vector2 CurrentPlayerPoint()
        {
            Transform player = PlayerController.PlayerTransform;
            Vector2 point = player != null ? (Vector2)player.position : Vector2.zero;
            ArenaBounds bounds = ArenaBounds.Instance;
            return bounds != null ? bounds.ClampToPlayArea(point) : point;
        }

        private static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float mag = ab.sqrMagnitude;
            if (mag < 0.0001f)
            {
                return a;
            }

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / mag);
            return a + ab * t;
        }

        private void PlayBossMusic()
        {
            if (GameMusicController.Instance == null)
            {
                return;
            }

            GameMusicController.Instance.SetBossMusic();
            musicOn = true;
        }

        private void RestoreGameplayMusic()
        {
            if (!musicOn)
            {
                return;
            }

            musicOn = false;
            GameManager game = GameManager.Instance;
            if (game != null && game.State == GameState.GameOver)
            {
                return;
            }

            if (GameMusicController.Instance != null)
            {
                GameMusicController.Instance.SetGameplayMusic();
            }
        }

        private void OnDisable()
        {
            RestoreGameplayMusic();
        }
    }
}
