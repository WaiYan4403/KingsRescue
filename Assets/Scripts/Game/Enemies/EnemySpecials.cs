using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Extra behaviours from <see cref="EnemyTrait"/>: lunges, summons, stealth, heals.
    /// Disabled when the definition has no traits.
    /// </summary>
    public class EnemySpecials : MonoBehaviour, IPoolable
    {
        private EnemyDefinition definition;
        private float runMinutes;
        private float nextSpecial;
        private float stealthUntil;
        private bool stealthed;
        private SpriteRenderer body;
        private Color baseTint = Color.white;
        private EnemyMovement movement;
        private ContactDamage contact;
        private Health health;
        private HitFlash flash;
        private string lastAction;
        private float lastActionUntil;

        private void Awake()
        {
            body = GetComponent<SpriteRenderer>();
            movement = GetComponent<EnemyMovement>();
            contact = GetComponent<ContactDamage>();
            health = GetComponent<Health>();
            flash = GetComponent<HitFlash>();
        }

        private void OnEnable()
        {
            if (contact != null)
            {
                contact.HitPlayer += OnContactHit;
            }

            if (health != null)
            {
                health.Damaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (contact != null)
            {
                contact.HitPlayer -= OnContactHit;
            }

            if (health != null)
            {
                health.Damaged -= OnDamaged;
            }
        }

        public void OnSpawned()
        {
            stealthed = false;
            stealthUntil = 0f;
            lastAction = null;
        }

        public string StatusLine
        {
            get
            {
                if (stealthed)
                {
                    return "cloaked";
                }

                if (!string.IsNullOrEmpty(lastAction) && Time.time < lastActionUntil)
                {
                    return lastAction;
                }

                return null;
            }
        }

        public void OnDespawned()
        {
            definition = null;
        }

        public void Configure(EnemyDefinition def, float minutes)
        {
            definition = def;
            runMinutes = minutes;
            bool active = def != null && def.traits != EnemyTrait.None;
            enabled = active;
            if (!active)
            {
                if (health != null)
                {
                    health.BlocksFrontal = false;
                }

                return;
            }

            nextSpecial = Time.time + Mathf.Max(0.4f, SpecialInterval() * 0.45f);
            if (health != null)
            {
                health.BlocksFrontal = Has(EnemyTrait.FrontBlock);
            }

            if (Has(EnemyTrait.Mimic))
            {
                movement?.SetMimicSleeping(true);
            }

            if (Has(EnemyTrait.Stealth))
            {
                EnterStealth(1.8f);
            }
        }

        public void ApplyVisual(Color tint)
        {
            baseTint = tint;
            if (body != null)
            {
                body.color = tint;
            }

            flash?.SetBaseColor(tint);
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
                if (Time.time >= nextSpecial)
                {
                    nextSpecial = Time.time + 0.2f;
                }

                return;
            }

            if (health != null && Has(EnemyTrait.FrontBlock))
            {
                health.FacingLeft = movement != null && movement.FacingLeft;
            }

            if (Has(EnemyTrait.Mimic) && TryWakeMimic())
            {
                movement?.WakeMimic();
            }

            if (stealthed && Time.time >= stealthUntil)
            {
                ExitStealth();
            }

            if (Time.time < nextSpecial)
            {
                return;
            }

            nextSpecial = Time.time + SpecialInterval();
            Transform player = PlayerController.VisibleTransform;
            if (player == null)
            {
                return;
            }

            Vector2 origin = transform.position;
            Vector2 toPlayer = (Vector2)player.position - origin;

            if (Has(EnemyTrait.Lunge) || Has(EnemyTrait.LineCharge))
            {
                TryLunge(toPlayer);
                Note(Has(EnemyTrait.LineCharge) ? "line charge" : "lunge");
            }

            if (Has(EnemyTrait.Teleport))
            {
                TryTeleport(player.position);
                Note("teleport");
            }

            if (Has(EnemyTrait.Summon))
            {
                SpawnAllies(origin);
                Note("summoning");
            }

            if (Has(EnemyTrait.HealAllies))
            {
                HealNearby();
                Note("healing allies");
            }

            if (Has(EnemyTrait.BuffAllies))
            {
                HasteNearby();
                Note("buffing allies");
            }

            if (Has(EnemyTrait.DropHazard) || Has(EnemyTrait.FireTrail))
            {
                bool fire = Has(EnemyTrait.FireTrail);
                DropZone(origin, fire);
                Note(fire ? "leaving fire" : "dropping poison");
            }

            if (Has(EnemyTrait.Stealth) && !stealthed)
            {
                EnterStealth(1.4f);
            }
        }

        private bool TryWakeMimic()
        {
            Transform player = PlayerController.VisibleTransform;
            if (player == null)
            {
                return false;
            }

            return ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude < 2.6f * 2.6f;
        }

        private void TryLunge(Vector2 toPlayer)
        {
            if (toPlayer.sqrMagnitude < 0.04f || movement == null)
            {
                return;
            }

            float speed = Has(EnemyTrait.LineCharge) ? 7.2f : 6.1f;
            float time = Has(EnemyTrait.LineCharge) ? 0.55f : 0.28f;
            movement.Dash(toPlayer.normalized * speed, time);
        }

        private void TryTeleport(Vector2 player)
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(1.4f, 2.6f);
            Vector2 dest = player + offset;
            if (bounds != null)
            {
                dest = bounds.ClampToPlayArea(dest);
            }

            transform.position = dest;
        }

        private void SpawnAllies(Vector2 origin)
        {
            EnemyDefinition[] table = definition.summonAllies;
            EnemyDefinition fallback = definition.summonAlly;
            int count = definition.summonCount > 0 ? definition.summonCount : 2;
            for (int i = 0; i < count; i++)
            {
                EnemyDefinition pick = fallback;
                if (table != null && table.Length > 0)
                {
                    pick = table[Random.Range(0, table.Length)];
                    if (pick == null)
                    {
                        pick = fallback;
                    }
                }

                if (pick == null || pick.prefab == null)
                {
                    continue;
                }

                float angle = i / (float)count * Mathf.PI * 2f;
                Vector2 pos = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.85f;
                Enemy.SpawnConfigured(pick, pos, runMinutes);
            }
        }

        private void HealNearby()
        {
            float radius = definition.specialRadius > 0.2f ? definition.specialRadius : 2.4f;
            float radiusSq = radius * radius;
            Vector2 origin = transform.position;
            for (int i = 0; i < Enemy.Active.Count; i++)
            {
                Enemy other = Enemy.Active[i];
                if (other == null || !other.IsAlive || other.Health == null)
                {
                    continue;
                }

                if ((other.Position - origin).sqrMagnitude > radiusSq)
                {
                    continue;
                }

                other.Health.Heal(Mathf.Max(2f, other.Health.Max * 0.12f));
            }
        }

        private void HasteNearby()
        {
            float radius = definition.specialRadius > 0.2f ? definition.specialRadius : 2.6f;
            float radiusSq = radius * radius;
            Vector2 origin = transform.position;
            for (int i = 0; i < Enemy.Active.Count; i++)
            {
                Enemy other = Enemy.Active[i];
                if (other == null || !other.IsAlive)
                {
                    continue;
                }

                if ((other.Position - origin).sqrMagnitude > radiusSq)
                {
                    continue;
                }

                other.GetComponent<EnemyMovement>()?.ApplyHaste(1.45f, 2.4f);
            }
        }

        private void DropZone(Vector2 origin, bool fire)
        {
            GameObject prefab = definition.hazardPrefab;
            if (prefab == null)
            {
                return;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            GroundHazard zone = pool.Spawn(prefab, origin, Quaternion.identity)?.GetComponent<GroundHazard>();
            if (zone == null)
            {
                return;
            }

            float radius = definition.specialRadius > 0.2f ? definition.specialRadius * 0.7f : 0.85f;
            if (fire)
            {
                zone.Arm(radius, 3.2f, 2.4f, 1f, 0f, 0f);
            }
            else
            {
                zone.Arm(radius, 1.6f, 3.1f, 0.7f, 0.8f, 4f);
            }
        }

        private void EnterStealth(float duration)
        {
            stealthed = true;
            stealthUntil = Time.time + duration;
            if (health != null)
            {
                health.Untargetable = true;
            }

            SetAlpha(0.22f);
        }

        private void ExitStealth()
        {
            stealthed = false;
            if (health != null)
            {
                health.Untargetable = false;
            }

            SetAlpha(baseTint.a > 0.01f ? baseTint.a : 1f);
        }

        private void SetAlpha(float alpha)
        {
            if (body == null)
            {
                return;
            }

            Color c = body.color;
            c.a = alpha;
            body.color = c;
        }

        private void OnContactHit()
        {
            if (definition == null)
            {
                return;
            }

            PlayerController player = PlayerController.Instance;
            PlayerStatus status = player != null ? player.Status : null;
            if (status != null && Has(EnemyTrait.IceSlow))
            {
                float duration = definition.slowDuration > 0.2f ? definition.slowDuration : 1.4f;
                status.ApplySlow(definition.slowFactor > 0.1f ? definition.slowFactor : 0.55f, duration);
            }

            if (status != null && Has(EnemyTrait.Poison))
            {
                status.ApplyPoison(4.5f, 2.4f);
            }

            if (Has(EnemyTrait.Lifesteal) && health != null)
            {
                health.Heal(Mathf.Max(1.5f, health.Max * 0.08f));
            }
        }

        private void OnDamaged(Health source, DamageInfo info)
        {
            if (Has(EnemyTrait.Mimic))
            {
                movement?.WakeMimic();
            }

            if (Has(EnemyTrait.Stealth) && stealthed)
            {
                ExitStealth();
            }
        }

        private void Note(string action)
        {
            lastAction = action;
            lastActionUntil = Time.time + 1.1f;
        }

        private bool Has(EnemyTrait trait)
        {
            return definition != null && (definition.traits & trait) != 0;
        }

        private float SpecialInterval()
        {
            return definition.specialInterval > 0.2f ? definition.specialInterval : 3.6f;
        }
    }
}
