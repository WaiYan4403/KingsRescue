using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Vampire Survivors-style sky drop: a marker on the ground, then a fireball
    /// falls onto that point and blasts the player if they stay in the circle.
    /// </summary>
    public class MeteorStrike : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer fireball;
        [SerializeField] private SpriteRenderer indicator;
        [SerializeField] private float playerRadius = 0.35f;

        private static readonly int FallState = Animator.StringToHash("Meteor_Falling");

        private Vector2 impact;
        private Vector2 parked;
        private float damage;
        private float radius;
        private float warning;
        private float spawnedAt;
        private float fallDeadline;
        private bool falling;
        private bool exploded;
        private bool usesAnimation;
        private bool sawFall;
        private Animator animator;
        private EnemyMovement movement;
        private ContactDamage contact;
        private EnemyAttacker attacker;
        private OffscreenDespawner despawner;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            movement = GetComponent<EnemyMovement>();
            contact = GetComponent<ContactDamage>();
            attacker = GetComponent<EnemyAttacker>();
            despawner = GetComponent<OffscreenDespawner>();
        }

        public void OnSpawned()
        {
            exploded = false;
            falling = false;
        }

        public void OnDespawned()
        {
            exploded = true;
        }

        public void Configure(EnemyDefinition def, float runMinutes)
        {
            if (def == null || def.attackStyle != EnemyAttackStyle.Meteor)
            {
                enabled = false;
                return;
            }

            enabled = true;
            exploded = false;
            falling = false;
            damage = def.ScaledAttackDamage(runMinutes);
            radius = Mathf.Max(0.4f, def.meteorRadius);
            warning = Mathf.Max(0.25f, def.meteorWarning);
            spawnedAt = Time.time;

            if (movement != null)
            {
                movement.enabled = false;
            }

            if (contact != null)
            {
                contact.enabled = false;
            }

            if (attacker != null)
            {
                attacker.enabled = false;
            }

            if (despawner != null)
            {
                despawner.enabled = false;
            }

            PickImpact();
            PlaceVisuals(true);
        }

        /// <summary>
        /// Mage casts aim the blast at the player instead of a random field point.
        /// </summary>
        public void Retarget(Vector2 impactPoint)
        {
            SetImpact(impactPoint);
            spawnedAt = Time.time;
            falling = false;
            exploded = false;
            PlaceVisuals(true);
        }

        private void PickImpact()
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            Rect play = bounds != null ? bounds.PlayRect : new Rect(-3f, -6f, 6f, 12f);
            Vector2 here = transform.position;
            float x = Mathf.Clamp(here.x, play.xMin, play.xMax);
            float y = play.yMin + play.height * Random.Range(0.25f, 0.8f);
            SetImpact(new Vector2(x, y));
        }

        private void SetImpact(Vector2 impactPoint)
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            Rect play = bounds != null ? bounds.PlayRect : new Rect(-3f, -6f, 6f, 12f);
            impact = new Vector2(
                Mathf.Clamp(impactPoint.x, play.xMin, play.xMax),
                Mathf.Clamp(impactPoint.y, play.yMin, play.yMax));
            float top = bounds != null ? bounds.TopSpawnY : impact.y + 8f;
            parked = new Vector2(impact.x, top);
            transform.position = parked;
        }

        private void PlaceVisuals(bool warningPhase)
        {
            if (indicator != null)
            {
                indicator.enabled = true;
                indicator.transform.position = new Vector3(impact.x, impact.y, 0f);
                float diameter = radius * 2f;
                float native = indicator.sprite != null ? indicator.sprite.bounds.size.x : 1f;
                float scale = native > 0.0001f ? diameter / native : diameter;
                indicator.transform.localScale = new Vector3(scale, scale, 1f);
                Color c = indicator.color;
                c.a = warningPhase ? 0.85f : 0.4f;
                indicator.color = c;
            }

            if (fireball != null)
            {
                fireball.enabled = !warningPhase;
            }

            if (warningPhase)
            {
                RewindFall();
            }
        }

        private void RewindFall()
        {
            sawFall = false;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            animator.speed = 0f;
            animator.Play(FallState, 0, 0f);
            animator.Update(0f);
        }

        private void Update()
        {
            if (exploded)
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
                spawnedAt += Time.deltaTime;
                if (falling)
                {
                    fallDeadline += Time.deltaTime;
                    if (animator != null)
                    {
                        animator.speed = 0f;
                    }
                }

                // Movement is disabled on a strike, so the body has to take the road scroll itself.
                transform.position += (Vector3)(ScrollDirector.Velocity * Time.deltaTime);
                impact += ScrollDirector.Velocity * Time.deltaTime;
                return;
            }

            if (animator != null && falling)
            {
                animator.speed = 1f;
            }

            if (indicator != null)
            {
                indicator.transform.position = new Vector3(impact.x, impact.y, 0f);
            }

            float elapsed = Time.time - spawnedAt;
            if (!falling)
            {
                PulseIndicator(elapsed);
                if (elapsed >= warning)
                {
                    BeginFall();
                }

                return;
            }

            if (FallFinished() || Time.time >= fallDeadline)
            {
                Explode();
            }
        }

        /// <summary>
        /// The sheet falls inside the sprite, with the pivot on the blast point.
        /// Damage lands when that clip finishes, which is the impact frame.
        /// </summary>
        private void BeginFall()
        {
            falling = true;
            usesAnimation = false;
            sawFall = false;
            transform.position = impact;
            PlaceVisuals(false);

            float clip = 0.7f;
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = 1f;
                animator.Play(FallState, 0, 0f);
                animator.Update(0f);
                usesAnimation = true;
                float length = animator.GetCurrentAnimatorStateInfo(0).length;
                if (length > 0.05f)
                {
                    clip = length;
                }
            }

            fallDeadline = Time.time + clip + 0.05f;
        }

        private bool FallFinished()
        {
            if (!usesAnimation || animator == null)
            {
                return false;
            }

            if (animator.IsInTransition(0))
            {
                return false;
            }

            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash != FallState)
            {
                return false;
            }

            if (info.normalizedTime < 0.85f)
            {
                sawFall = true;
                return false;
            }

            return sawFall && info.normalizedTime >= 1f;
        }

        private void PulseIndicator(float elapsed)
        {
            if (indicator == null)
            {
                return;
            }

            float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(elapsed * 10f));
            Color c = indicator.color;
            c.a = pulse;
            indicator.color = c;
        }

        private void Explode()
        {
            exploded = true;
            transform.position = impact;
            HitboxDisplay.Flash(impact, radius, 0.35f);

            PlayerController player = PlayerController.Instance;
            if (player != null && player.Health != null && player.Health.IsAlive)
            {
                float reach = radius + playerRadius;
                Vector2 delta = (Vector2)player.transform.position - impact;
                if (delta.sqrMagnitude <= reach * reach)
                {
                    Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.down;
                    player.Health.TakeDamage(new DamageInfo(damage, player.transform.position, direction, 0f, gameObject));
                }
            }

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
