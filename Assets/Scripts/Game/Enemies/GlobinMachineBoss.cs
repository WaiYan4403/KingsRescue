using UnityEngine;
using KingsRescue.Audio;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Goblin Machine. Stays at the top of the screen and cycles three attacks:
    /// a sideways cannon strafe, bouncing saws, and a spinning bullet ring.
    /// </summary>
    public class GlobinMachineBoss : MonoBehaviour, IPoolable
    {
        private enum Phase
        {
            Intro,
            Idle,
            Strafe,
            Saws,
            Spiral
        }

        private static readonly int IdleState = Animator.StringToHash("GlobinMachine_Idle");
        private static readonly int WalkState = Animator.StringToHash("GlobinMachine_Walk");
        private static readonly int Attack1State = Animator.StringToHash("GlobinMachine_Attack1");
        private static readonly int Attack2State = Animator.StringToHash("GlobinMachine_Attack2");

        private const float HalfHealth = 0.5f;
        private const float SawSwingLength = 0.27f;

        [SerializeField] private float perchPadding = 0.7f;
        [SerializeField] private float strafeSpeed = 3.4f;
        [SerializeField] private float strafeDuration = 4.2f;
        [Tooltip("Seconds between cannon waves. Each wave is the big bullet plus the two small ones that follow.")]
        [SerializeField] private float shotInterval = 1.6f;
        [SerializeField] private float sawLifetime = 12f;
        [SerializeField] private float sawSpeed = 4.6f;
        [SerializeField] private float sawScale = 2.7f;
        [SerializeField] private float spiralDuration = 6.5f;
        [SerializeField] private float spiralInterval = 0.3f;
        [SerializeField] private float spiralTurnSpeed = 150f;
        [Tooltip("How far above the screen center the spin attack stands.")]
        [SerializeField] private float spiralLift = 2.2f;

        [Header("Muzzles")]
        [SerializeField] private Transform mainBulletParent;
        [SerializeField] private Transform leftSmallBulletParent;
        [SerializeField] private Transform rightSmallBulletParent;
        [SerializeField] private Transform sawWheelParent;

        [Header("Projectiles")]
        [SerializeField] private GameObject mainBulletPrefab;
        [SerializeField] private GameObject smallBulletPrefab;
        [SerializeField] private GameObject sawWheelPrefab;

        private enum ShotMuzzle
        {
            Main,
            LeftSmall,
            RightSmall,
            Saw
        }

        private struct LateShot
        {
            public float at;
            public ShotMuzzle muzzle;
            public Vector2 direction;
            public float speed;
            public float damage;
            public float radius;
            public bool alive;
        }

        private EnemyMovement movement;
        private Health health;
        private Animator animator;
        private EnemyDefinition definition;
        private Phase phase;
        private float phaseEnds;
        private float nextShot;
        private float strafeTargetX;
        private float spiralAngle;
        private bool spiralFiring;
        private int sawsToThrow;
        private int sawsThrown;
        private float runMinutes;
        private int nextAttack;
        private int playingState;
        private float perchY;
        private float spiralY;
        private float minX;
        private float maxX;
        private readonly LateShot[] lateShots = new LateShot[8];
        private SpriteRenderer body;

        private void Awake()
        {
            movement = GetComponent<EnemyMovement>();
            health = GetComponent<Health>();
            animator = GetComponent<Animator>();
            body = GetComponent<SpriteRenderer>();
        }

        public string StatusLine
        {
            get
            {
                switch (phase)
                {
                    case Phase.Intro:
                        return "entering";
                    case Phase.Strafe:
                        return "cannon strafe";
                    case Phase.Saws:
                        return "saw wheels";
                    case Phase.Spiral:
                        return "bullet spiral";
                    default:
                        return "waiting";
                }
            }
        }

        public void OnSpawned()
        {
            nextAttack = 0;
            spiralAngle = 0f;
            spiralFiring = false;
            ClearLateShots();
            PlayState(IdleState);
        }

        public void OnDespawned()
        {
            ClearLateShots();
        }

        public void Configure(EnemyDefinition def, float minutes)
        {
            bool isBoss = def != null && def.isBoss;
            enabled = isBoss;
            if (!isBoss)
            {
                return;
            }

            definition = def;
            runMinutes = minutes;
            if (movement != null)
            {
                movement.enabled = false;
            }

            EnemyAttacker attacker = GetComponent<EnemyAttacker>();
            if (attacker != null)
            {
                attacker.enabled = false;
            }

            EncounterBoss generic = GetComponent<EncounterBoss>();
            if (generic != null)
            {
                generic.enabled = false;
            }

            phase = Phase.Intro;
            phaseEnds = Time.time + 2.4f;
            PlayState(WalkState);
        }

        private void Update()
        {
            if (definition == null)
            {
                return;
            }

            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying)
            {
                return;
            }

            if (movement != null && movement.IsFrozen)
            {
                float paused = Time.deltaTime;
                phaseEnds += paused;
                nextShot += paused;
                DelayLateShots(paused);
                return;
            }

            RefreshLane();
            TickLateShots();

            switch (phase)
            {
                case Phase.Intro:
                    TickIntro();
                    break;
                case Phase.Idle:
                    HoldPerch(2.2f);
                    if (Time.time >= phaseEnds)
                    {
                        BeginNextAttack();
                    }

                    break;
                case Phase.Strafe:
                    TickStrafe();
                    break;
                case Phase.Saws:
                    HoldPerch(2.6f);
                    if (Time.time < phaseEnds)
                    {
                        break;
                    }

                    if (sawsThrown < sawsToThrow)
                    {
                        ThrowNextSaw();
                    }
                    else
                    {
                        BeginNextAttack();
                    }

                    break;
                case Phase.Spiral:
                    MoveToward(LaneCenter(), spiralY, 6.5f);
                    if (!AtSpiralMark())
                    {
                        spiralFiring = false;
                        PlayState(WalkState);
                        break;
                    }

                    if (!spiralFiring)
                    {
                        spiralFiring = true;
                        phaseEnds = Time.time + spiralDuration;
                        nextShot = Time.time;
                        PlayState(Attack2State, true);
                    }

                    TickSpiral();
                    break;
            }
        }

        private void TickIntro()
        {
            Vector3 position = transform.position;
            position.x = Mathf.MoveTowards(position.x, LaneCenter(), strafeSpeed * Time.deltaTime);
            position.y = Mathf.MoveTowards(position.y, perchY, 3.6f * Time.deltaTime);
            transform.position = position;
            if (Mathf.Abs(position.y - perchY) > 0.08f && Time.time < phaseEnds)
            {
                return;
            }

            position.y = perchY;
            transform.position = position;
            BeginIdle();
        }

        private void BeginIdle()
        {
            phase = Phase.Idle;
            phaseEnds = Time.time + 0.7f;
            PlayState(IdleState);
        }

        private void BeginNextAttack()
        {
            int attack = nextAttack;
            nextAttack = (nextAttack + 1) % 3;
            if (attack == 0)
            {
                BeginStrafe();
            }
            else if (attack == 1)
            {
                BeginSaws();
            }
            else
            {
                BeginSpiral();
            }
        }

        private void BeginStrafe()
        {
            phase = Phase.Strafe;
            phaseEnds = Time.time + strafeDuration;
            nextShot = Time.time + 0.15f;
            strafeTargetX = Random.Range(minX, maxX);
            PlayState(WalkState);
        }

        private void TickStrafe()
        {
            Vector3 position = transform.position;
            position.x = Mathf.MoveTowards(position.x, strafeTargetX, strafeSpeed * Time.deltaTime);
            position.y = Mathf.MoveTowards(position.y, perchY, strafeSpeed * Time.deltaTime);
            transform.position = position;
            if (Mathf.Abs(position.x - strafeTargetX) < 0.2f)
            {
                strafeTargetX = Random.Range(minX, maxX);
            }

            if (Time.time >= nextShot)
            {
                nextShot = Time.time + shotInterval;
                FireTriple();
            }

            if (Time.time >= phaseEnds)
            {
                BeginIdle();
            }
        }

        private void BeginSaws()
        {
            phase = Phase.Saws;
            sawsToThrow = BelowHalf() ? 2 : 1;
            sawsThrown = 0;
            ThrowNextSaw();
        }

        private void ThrowNextSaw()
        {
            Vector2 direction = sawsThrown == 0
                ? new Vector2(0.45f, -0.9f)
                : new Vector2(0.95f, -0.28f);
            ThrowSaw(direction);
            sawsThrown++;
            phaseEnds = Time.time + SawSwingLength;
            PlayState(Attack1State, true);
        }

        private void BeginSpiral()
        {
            phase = Phase.Spiral;
            spiralFiring = false;
            PlayState(WalkState, true);
        }

        private void TickSpiral()
        {
            spiralAngle += spiralTurnSpeed * Time.deltaTime;
            if (Time.time >= nextShot)
            {
                nextShot = Time.time + spiralInterval;
                FireRing(spiralAngle, true);
                if (BelowHalf())
                {
                    FireRing(-spiralAngle + 30f, false);
                }
            }

            if (Time.time >= phaseEnds)
            {
                BeginIdle();
            }
        }

        private void FireTriple()
        {
            SoundEffects.Play(SoundId.EnemyAttack);
            Vector2 origin = MuzzlePoint(ShotMuzzle.Main);
            Vector2 forward = AimDown(origin);
            float damage = ShotDamage();
            const float shotSpeed = 7.2f;
            SpawnShot(mainBulletPrefab, origin, forward, shotSpeed, damage, 0.72f, false, CrossScreenLife(shotSpeed));
            QueueLate(ShotMuzzle.LeftSmall, forward, shotSpeed, damage * 0.4f, 0.32f, 0.18f);
            QueueLate(ShotMuzzle.RightSmall, forward, shotSpeed, damage * 0.4f, 0.32f, 0.18f);
        }

        private void ThrowSaw(Vector2 direction)
        {
            SoundEffects.Play(SoundId.EnemyAttack);
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
            float radius = 0.55f * Mathf.Max(1.2f, sawScale);
            SpawnShot(sawWheelPrefab, MuzzlePoint(ShotMuzzle.Saw), dir, sawSpeed, ShotDamage() * 0.9f, radius, true, Mathf.Max(8f, sawLifetime));
        }

        private void FireRing(float baseAngle, bool big)
        {
            const int count = 5;
            float damage = ShotDamage() * (big ? 0.85f : 0.45f);
            float radius = big ? 0.55f : 0.26f;
            float speed = big ? 5.4f : 7.2f;
            GameObject prefab = big ? mainBulletPrefab : smallBulletPrefab;
            float life = CrossScreenLife(speed);

            for (int i = 0; i < count; i++)
            {
                float angle = (baseAngle + i * (360f / count)) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                ShotMuzzle muzzle = big
                    ? ShotMuzzle.Main
                    : (i % 2 == 0 ? ShotMuzzle.LeftSmall : ShotMuzzle.RightSmall);
                SpawnShot(prefab, MuzzlePoint(muzzle), dir, speed, damage, radius, false, life);
            }
        }

        private void QueueLate(ShotMuzzle muzzle, Vector2 direction, float speed, float damage, float radius, float delay)
        {
            for (int i = 0; i < lateShots.Length; i++)
            {
                if (lateShots[i].alive)
                {
                    continue;
                }

                lateShots[i] = new LateShot
                {
                    at = Time.time + delay,
                    muzzle = muzzle,
                    direction = direction,
                    speed = speed,
                    damage = damage,
                    radius = radius,
                    alive = true
                };
                return;
            }
        }

        private void TickLateShots()
        {
            for (int i = 0; i < lateShots.Length; i++)
            {
                if (!lateShots[i].alive || Time.time < lateShots[i].at)
                {
                    continue;
                }

                LateShot shot = lateShots[i];
                lateShots[i].alive = false;
                SpawnShot(smallBulletPrefab, MuzzlePoint(shot.muzzle), shot.direction, shot.speed, shot.damage, shot.radius, false, CrossScreenLife(shot.speed));
            }
        }

        private void DelayLateShots(float paused)
        {
            for (int i = 0; i < lateShots.Length; i++)
            {
                if (lateShots[i].alive)
                {
                    lateShots[i].at += paused;
                }
            }
        }

        private void ClearLateShots()
        {
            for (int i = 0; i < lateShots.Length; i++)
            {
                lateShots[i].alive = false;
            }
        }

        private void SpawnShot(GameObject prefab, Vector2 origin, Vector2 direction, float speed, float damage, float radius, bool saw, float life)
        {
            if (prefab == null && definition != null)
            {
                prefab = definition.projectilePrefab;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (prefab == null || pool == null)
            {
                return;
            }

            HostileProjectile shot = pool.Spawn(prefab, origin, Quaternion.identity)?.GetComponent<HostileProjectile>();
            if (shot == null)
            {
                return;
            }

            shot.Launch(direction, speed, damage, life, radius, 1f, 0f, false, gameObject, 0f);
            if (saw)
            {
                shot.SetScreenBounce(420f, true);
            }
        }

        /// <summary>Seconds a straight shot needs to cross the whole camera, plus a little past the far edge.</summary>
        private float CrossScreenLife(float speed)
        {
            float span = 28f;
            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds != null)
            {
                Rect screen = bounds.ScreenRect;
                span = Mathf.Sqrt(screen.width * screen.width + screen.height * screen.height) + 4f;
            }

            return span / Mathf.Max(1f, speed) + 0.4f;
        }

        private Vector2 AimDown(Vector2 origin)
        {
            Transform player = PlayerController.VisibleTransform;
            Vector2 forward = Vector2.down;
            if (player != null)
            {
                Vector2 toPlayer = (Vector2)player.position - origin;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    forward = toPlayer.normalized;
                }
            }

            if (forward.y > -0.35f)
            {
                forward.y = -0.35f;
                forward.Normalize();
            }

            return forward;
        }

        private float ShotDamage()
        {
            if (definition == null)
            {
                return 12f;
            }

            return definition.ScaledAttackDamage(runMinutes);
        }

        private Vector2 MuzzlePoint(ShotMuzzle muzzle)
        {
            Transform point;
            switch (muzzle)
            {
                case ShotMuzzle.LeftSmall:
                    point = leftSmallBulletParent;
                    break;
                case ShotMuzzle.RightSmall:
                    point = rightSmallBulletParent;
                    break;
                case ShotMuzzle.Saw:
                    point = sawWheelParent;
                    break;
                default:
                    point = mainBulletParent;
                    break;
            }

            return point != null ? (Vector2)point.position : (Vector2)transform.position;
        }

        private bool BelowHalf()
        {
            return health != null && health.Normalised <= HalfHealth;
        }

        private bool AtSpiralMark()
        {
            Vector2 position = transform.position;
            return Mathf.Abs(position.x - LaneCenter()) < 0.12f && Mathf.Abs(position.y - spiralY) < 0.12f;
        }

        private void HoldPerch(float speed)
        {
            MoveToward(LaneCenter(), perchY, speed);
        }

        private void MoveToward(float x, float y, float speed)
        {
            Vector3 position = transform.position;
            float step = speed * Time.deltaTime;
            position.x = Mathf.MoveTowards(position.x, x, step);
            position.y = Mathf.MoveTowards(position.y, y, step);
            transform.position = position;
        }

        private float LaneCenter()
        {
            return (minX + maxX) * 0.5f;
        }

        private void RefreshLane()
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds == null)
            {
                perchY = transform.position.y;
                spiralY = perchY - 4f;
                minX = -4f;
                maxX = 4f;
                return;
            }

            Rect screen = bounds.ScreenRect;
            float halfBody = body != null ? Mathf.Max(1.6f, body.bounds.extents.y) : 3.6f;
            perchY = screen.yMax - halfBody - Mathf.Max(0.3f, perchPadding);
            spiralY = screen.center.y + spiralLift;
            minX = screen.xMin + 1.15f;
            maxX = screen.xMax - 1.15f;
            if (maxX < minX)
            {
                float mid = screen.center.x;
                minX = mid - 1f;
                maxX = mid + 1f;
            }
        }

        private void PlayState(int state, bool restart = false)
        {
            if (animator == null)
            {
                return;
            }

            if (!restart && playingState == state)
            {
                return;
            }

            playingState = state;
            animator.Play(state, 0, 0f);
        }
    }
}
