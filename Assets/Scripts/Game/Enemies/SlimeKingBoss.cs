using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// First boss. Alternates a vanish-slam onto the hero and a rotating water beam.
    /// </summary>
    public class SlimeKingBoss : MonoBehaviour, IPoolable
    {
        private enum Phase
        {
            Intro,
            Idle,
            JumpAir,
            LaserWindup,
            Laser
        }

        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer landingMarker;
        [SerializeField] private Transform laserPivot;
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

        [Header("Slam")]
        [SerializeField] private float slamRadius = 1.5f;
        [SerializeField] private float slamDamage = 18f;

        [Header("Laser")]
        [SerializeField] private float laserLength = 7.5f;
        [SerializeField] private float laserWidth = 0.55f;
        [SerializeField] private float laserRotateSpeed = 52f;
        [SerializeField] private float laserDamage = 9f;
        [SerializeField] private float laserTick = 0.28f;

        private EnemyMovement movement;
        private ContactDamage contact;
        private Health health;
        private Phase phase;
        private float phaseEnds;
        private Vector2 slamPoint;
        private bool slamLocked;
        private bool nextIsJump = true;
        private float nextLaserTick;
        private float rotateSign = 1f;
        private bool musicOn;
        private float introFromY;
        private float introToY;
        private float introStarted;

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponent<SpriteRenderer>();
            }

            movement = GetComponent<EnemyMovement>();
            contact = GetComponent<ContactDamage>();
            health = GetComponent<Health>();
        }

        public void OnSpawned()
        {
            musicOn = false;
            nextIsJump = true;
            HideMarker();
            SetLaserVisible(false);
        }

        public void OnDespawned()
        {
            RestoreGameplayMusic();
            if (health != null)
            {
                health.Untargetable = false;
            }

            HideMarker();
            SetLaserVisible(false);
        }

        public void Configure(EnemyDefinition def, float runMinutes)
        {
            bool isKing = def != null && def.isBoss;
            enabled = isKing;
            if (!isKing)
            {
                return;
            }

            slamDamage = def.ScaledAttackDamage(runMinutes);
            laserDamage = Mathf.Max(6f, def.ScaledAttackDamage(runMinutes) * 0.55f);

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
                        if (nextIsJump)
                        {
                            BeginJump();
                        }
                        else
                        {
                            BeginLaserWindup();
                        }

                        nextIsJump = !nextIsJump;
                    }

                    break;
                case Phase.JumpAir:
                    TickJump();
                    break;
                case Phase.LaserWindup:
                    ClampToPlay();
                    PulseLaser(0.35f);
                    if (Time.time >= phaseEnds)
                    {
                        BeginLaser();
                    }

                    break;
                case Phase.Laser:
                    TickLaser();
                    break;
            }
        }

        private void LateUpdate()
        {
            if (phase == Phase.JumpAir)
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
            phaseEnds = Time.time + laserWindup;
            SetMove(false);
            SetContact(true);
            AimLaserAtPlayer();
            rotateSign = Random.value < 0.5f ? -1f : 1f;
            LayoutLaser();
            SetLaserVisible(true);
            PulseLaser(0.35f);
        }

        private void BeginLaser()
        {
            phase = Phase.Laser;
            phaseEnds = Time.time + laserDuration;
            nextLaserTick = 0f;
            PulseLaser(0.92f);
        }

        private void TickLaser()
        {
            if (laserPivot != null)
            {
                laserPivot.Rotate(0f, 0f, rotateSign * laserRotateSpeed * Time.deltaTime);
            }

            PulseLaser(0.75f + 0.2f * Mathf.Abs(Mathf.Sin(Time.time * 14f)));
            TryLaserHit();

            if (Time.time >= phaseEnds)
            {
                SetLaserVisible(false);
                BeginIdle();
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
            Vector2 beamDir = laserPivot != null ? (Vector2)laserPivot.up : Vector2.up;
            Vector2 end = origin + beamDir * laserLength;
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

        private void AimLaserAtPlayer()
        {
            if (laserPivot == null)
            {
                return;
            }

            Transform player = PlayerController.PlayerTransform;
            Vector2 dir = player != null
                ? (Vector2)player.position - (Vector2)transform.position
                : Vector2.down;
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.down;
            }

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            laserPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void LayoutLaser()
        {
            if (laserBeam == null)
            {
                return;
            }

            float native = laserBeam.sprite != null ? Mathf.Max(0.01f, laserBeam.sprite.bounds.size.y) : 1f;
            float nativeX = laserBeam.sprite != null ? Mathf.Max(0.01f, laserBeam.sprite.bounds.size.x) : 1f;
            Vector2 parent = LossyAbs();
            laserBeam.transform.localPosition = new Vector3(0f, (laserLength * 0.5f) / parent.y, 0f);
            laserBeam.transform.localRotation = Quaternion.identity;
            laserBeam.transform.localScale = new Vector3(
                (laserWidth / nativeX) / parent.x,
                (laserLength / native) / parent.y,
                1f);
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
            }
            else if (laserBeam != null)
            {
                laserBeam.enabled = visible;
            }
        }

        private void PulseLaser(float alpha)
        {
            if (laserBeam == null)
            {
                return;
            }

            Color c = laserBeam.color;
            c.a = alpha;
            laserBeam.color = c;
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
