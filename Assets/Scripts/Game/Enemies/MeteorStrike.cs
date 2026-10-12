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

        private Vector2 impact;
        private Vector2 start;
        private float damage;
        private float radius;
        private float warning;
        private float spawnedAt;
        private bool falling;
        private bool exploded;
        private EnemyMovement movement;
        private ContactDamage contact;
        private EnemyAttacker attacker;
        private OffscreenDespawner despawner;

        private void Awake()
        {
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
            start = new Vector2(impact.x, top);
            transform.position = start;
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
                fireball.enabled = true;
            }
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
                    falling = true;
                }

                return;
            }

            float fallTime = Mathf.Max(0.18f, warning * 0.45f);
            float t = Mathf.Clamp01((elapsed - warning) / fallTime);
            transform.position = Vector2.Lerp(start, impact, t * t);

            if (t < 1f)
            {
                return;
            }

            Explode();
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
