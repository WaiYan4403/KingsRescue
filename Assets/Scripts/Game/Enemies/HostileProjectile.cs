using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Hostile shot aimed at the hero. Optional homing and a slow on hit (spider webs).
    /// Webs play flying, then hit, then destroy before returning to the pool.
    /// </summary>
    public class HostileProjectile : MonoBehaviour, IPoolable
    {
        private enum VisualPhase
        {
            Flying,
            Hit,
            Destroy
        }

        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int DestoryHash = Animator.StringToHash("Destory");
        private static readonly int IdleState = Animator.StringToHash("Web_Idle");
        private static readonly int NetState = Animator.StringToHash("Web_Net");
        private static readonly int DestoryState = Animator.StringToHash("Web_Destory");

        [SerializeField] private float hitRadius = 0.28f;
        [SerializeField] private bool rotateToDirection = true;
        [SerializeField] private bool rotateZ = false;
        [SerializeField] private float playerRadius = 0.35f;

        private Vector2 velocity;
        private float damage;
        private float expireAt;
        private float maxTravel;
        private float traveled;
        private float slowFactor;
        private float slowDuration;
        private float phaseDeadline;
        private bool homing;
        private float homingTurn = 4.5f;
        private bool bounceOffScreen;
        private bool surviveHit;
        private float spinSpeed;
        private float nextHitTime;
        private Vector3 baseScale;
        private Color baseColor = Color.white;
        private bool visualsCached;
        private SpriteRenderer sprite;
        private bool staged;
        private VisualPhase phase;
        private GameObject source;
        private HitboxMarker hitbox;
        private Animator animator;

        /// <summary>Every hostile shot currently in flight.</summary>
        private static readonly System.Collections.Generic.List<HostileProjectile> Active = new System.Collections.Generic.List<HostileProjectile>(64);

        private void Awake()
        {
            animator = GetComponent<Animator>();
            CacheVisuals();
            staged = HasTrigger(HitHash) && HasTrigger(DestoryHash);
        }

        public void OnSpawned()
        {
            if (!Active.Contains(this))
            {
                Active.Add(this);
            }

            phase = VisualPhase.Flying;
        }

        public void OnDespawned()
        {
            Active.Remove(this);
            HitboxDisplay.Release(hitbox);
            hitbox = null;
            phase = VisualPhase.Flying;
            ResetMotion();
            if (staged && animator != null)
            {
                animator.ResetTrigger(HitHash);
                animator.ResetTrigger(DestoryHash);
            }
        }

        public static void DespawnAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                HostileProjectile shot = Active[i];
                if (shot != null)
                {
                    shot.Recycle();
                }
            }

            Active.Clear();
        }

        public void Launch(Vector2 direction, float speed, float damageAmount, float lifetime, float radius, float slow, float slowTime, bool homes, GameObject owner, float maxDistance = 0f)
        {
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
            velocity = dir * Mathf.Max(0.5f, speed);
            damage = damageAmount;
            hitRadius = Mathf.Max(0.08f, radius);
            expireAt = Time.time + Mathf.Max(0.05f, lifetime);
            maxTravel = maxDistance;
            traveled = 0f;
            slowFactor = slow;
            slowDuration = slowTime;
            homing = homes;
            source = owner;
            phase = VisualPhase.Flying;
            ResetMotion();
            BeginFlying();
            FaceVelocity();
            HitboxDisplay.SyncCircle(ref hitbox, transform.position, hitRadius);
        }

        /// <summary>Scales the shot art. Hit radius stays whatever Launch was given.</summary>
        public void SetScale(float scale)
        {
            CacheVisuals();
            transform.localScale = baseScale * Mathf.Max(0.2f, scale);
        }

        public void Tint(Color color)
        {
            CacheVisuals();
            if (sprite != null)
            {
                sprite.color = color;
            }
        }

        /// <summary>
        /// Keeps flying after a hit and reflects off the camera edges.
        /// Spin is degrees per second, used for saws.
        /// </summary>
        public void SetScreenBounce(float spinDegreesPerSecond, bool keepGoingAfterHit)
        {
            bounceOffScreen = true;
            surviveHit = keepGoingAfterHit;
            spinSpeed = spinDegreesPerSecond;
            maxTravel = 0f;
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying)
            {
                return;
            }

            if (phase == VisualPhase.Hit)
            {
                if (StateFinished(NetState) || Time.time >= phaseDeadline)
                {
                    BeginDestroy();
                }

                return;
            }

            if (phase == VisualPhase.Destroy)
            {
                if (StateFinished(DestoryState) || Time.time >= phaseDeadline)
                {
                    Recycle();
                }

                return;
            }

            float dt = Time.deltaTime;
            if (homing)
            {
                SteerTowardPlayer(dt);
            }

            float step = velocity.magnitude * dt;
            Vector2 position = (Vector2)transform.position + velocity * dt;
            if (bounceOffScreen)
            {
                position = BounceInsideScreen(position);
            }

            transform.position = position;
            traveled += step;
            if (Mathf.Abs(spinSpeed) > 0.01f && rotateZ)
            {
                transform.Rotate(0f, 0f, spinSpeed * dt);
            }
            else
            {
                FaceVelocity();
            }

            HitboxDisplay.SyncCircle(ref hitbox, position, hitRadius);

            if ((maxTravel > 0f && traveled >= maxTravel) || Time.time >= expireAt)
            {
                EndFlight();
                return;
            }

            if (!bounceOffScreen)
            {
                ArenaBounds bounds = ArenaBounds.Instance;
                if (bounds != null && bounds.IsPastDespawn(position))
                {
                    EndFlight();
                    return;
                }
            }

            TryHitPlayer(position);
        }

        private Vector2 BounceInsideScreen(Vector2 position)
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds == null)
            {
                return position;
            }

            Rect screen = bounds.ScreenRect;
            float inset = Mathf.Max(0.2f, hitRadius);
            if (position.x < screen.xMin + inset)
            {
                position.x = screen.xMin + inset;
                velocity.x = Mathf.Abs(velocity.x);
            }
            else if (position.x > screen.xMax - inset)
            {
                position.x = screen.xMax - inset;
                velocity.x = -Mathf.Abs(velocity.x);
            }

            if (position.y < screen.yMin + inset)
            {
                position.y = screen.yMin + inset;
                velocity.y = Mathf.Abs(velocity.y);
            }
            else if (position.y > screen.yMax - inset)
            {
                position.y = screen.yMax - inset;
                velocity.y = -Mathf.Abs(velocity.y);
            }

            return position;
        }

        private void CacheVisuals()
        {
            if (visualsCached)
            {
                return;
            }

            baseScale = transform.localScale;
            sprite = GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                baseColor = sprite.color;
            }

            visualsCached = true;
        }

        private void ResetMotion()
        {
            bounceOffScreen = false;
            surviveHit = false;
            spinSpeed = 0f;
            nextHitTime = 0f;
            CacheVisuals();
            transform.localScale = baseScale;
            transform.rotation = Quaternion.identity;
            if (sprite != null)
            {
                sprite.color = baseColor;
            }
        }

        private void SteerTowardPlayer(float dt)
        {
            Transform player = PlayerController.VisibleTransform;
            if (player == null)
            {
                return;
            }

            Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
            if (toPlayer.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float speed = velocity.magnitude;
            Vector2 desired = toPlayer.normalized * speed;
            velocity = Vector2.Lerp(velocity, desired, 1f - Mathf.Exp(-homingTurn * dt));
        }

        private void TryHitPlayer(Vector2 position)
        {
            PlayerController player = PlayerController.Instance;
            if (player == null || player.Health == null || !player.Health.IsAlive)
            {
                return;
            }

            float reach = hitRadius + playerRadius;
            if (((Vector2)player.transform.position - position).sqrMagnitude > reach * reach)
            {
                return;
            }

            bool overlapping = true;
            if (surviveHit && Time.time < nextHitTime)
            {
                overlapping = false;
            }

            if (overlapping && !player.Health.IsInvulnerable)
            {
                Vector2 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.down;
                player.Health.TakeDamage(new DamageInfo(damage, player.transform.position, direction, 0f, source));
                nextHitTime = Time.time + 0.45f;
            }

            if (!overlapping)
            {
                return;
            }

            PlayerStatus status = player.Status;
            if (status != null && slowDuration > 0.01f && slowFactor < 0.99f)
            {
                status.ApplySlow(slowFactor, slowDuration);
            }

            if (surviveHit)
            {
                return;
            }

            BeginHit();
        }

        private void BeginFlying()
        {
            if (!staged || animator == null)
            {
                return;
            }

            animator.ResetTrigger(HitHash);
            animator.ResetTrigger(DestoryHash);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 1f;
            animator.Play(IdleState, 0, 0f);
            animator.Update(0f);
        }

        private void BeginHit()
        {
            velocity = Vector2.zero;
            transform.rotation = Quaternion.identity;
            HitboxDisplay.Release(hitbox);
            hitbox = null;

            if (!staged || animator == null)
            {
                Recycle();
                return;
            }

            phase = VisualPhase.Hit;
            phaseDeadline = Time.time + 0.5f;
            animator.ResetTrigger(DestoryHash);
            animator.Play(NetState, 0, 0f);
            animator.Update(0f);
        }

        private void EndFlight()
        {
            if (!staged || animator == null)
            {
                Recycle();
                return;
            }

            BeginDestroy();
        }

        private void BeginDestroy()
        {
            if (phase == VisualPhase.Destroy)
            {
                return;
            }

            velocity = Vector2.zero;
            transform.rotation = Quaternion.identity;
            HitboxDisplay.Release(hitbox);
            hitbox = null;

            if (!staged || animator == null)
            {
                Recycle();
                return;
            }

            phase = VisualPhase.Destroy;
            phaseDeadline = Time.time + 0.28f;
            animator.ResetTrigger(HitHash);
            animator.ResetTrigger(DestoryHash);
            animator.Play(DestoryState, 0, 0f);
            animator.Update(0f);
        }

        private bool StateFinished(int stateHash)
        {
            if (animator == null || animator.IsInTransition(0))
            {
                return false;
            }

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            return info.shortNameHash == stateHash && info.normalizedTime >= 1f;
        }

        private bool HasTrigger(int hash)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].nameHash == hash)
                {
                    return true;
                }
            }

            return false;
        }

        private void FaceVelocity()
        {
            if (!rotateToDirection || velocity.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Recycle()
        {
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
