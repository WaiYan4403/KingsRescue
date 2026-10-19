using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Data-driven arena boss. Attack picks come from <see cref="BossKit"/> on the definition.
    /// </summary>
    public class EncounterBoss : MonoBehaviour, IPoolable
    {
        private enum Phase
        {
            Intro,
            Idle,
            Attack
        }

        [SerializeField] private SpriteRenderer landingMarker;
        [SerializeField] private SpriteRenderer body;

        private EnemyDefinition definition;
        private EnemyMovement movement;
        private ContactDamage contact;
        private Health health;
        private Phase phase;
        private float phaseEnds;
        private float introFromY;
        private float introToY;
        private float introStarted;
        private float runMinutes;
        private bool enraged;
        private Vector2 chargeDir;
        private int attackPick;
        private bool musicOn;

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

        public string StatusLine
        {
            get
            {
                string rage = enraged ? " (enraged)" : "";
                switch (phase)
                {
                    case Phase.Intro:
                        return "entering" + rage;
                    case Phase.Idle:
                        return "waiting" + rage;
                    default:
                        return AttackName(attackPick) + rage;
                }
            }
        }

        public void OnSpawned()
        {
            enraged = false;
            musicOn = false;
            HideMarker();
        }

        public void OnDespawned()
        {
            RestoreMusic();
            GameManager.Instance?.EndBossFight();
            HideMarker();
            if (health != null)
            {
                health.Untargetable = false;
            }
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

            phase = Phase.Intro;
            introStarted = Time.time;
            introFromY = transform.position.y;
            ArenaBounds bounds = ArenaBounds.Instance;
            introToY = bounds != null ? bounds.WorldRect.yMax - 1.7f : introFromY - 2f;
            phaseEnds = Time.time + 1.2f;
            StartBossMusic();
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
                phaseEnds += Time.deltaTime;
                introStarted += Time.deltaTime;
                return;
            }

            if (!enraged && Has(BossKit.Enrage) && health != null && health.Normalised <= 0.32f)
            {
                enraged = true;
                movement?.ApplyHaste(1.35f, 30f);
            }

            switch (phase)
            {
                case Phase.Intro:
                    TickIntro();
                    break;
                case Phase.Idle:
                    if (Time.time >= phaseEnds)
                    {
                        BeginAttack();
                    }

                    break;
                case Phase.Attack:
                    TickAttack();
                    break;
            }
        }

        private void TickIntro()
        {
            float t = Mathf.Clamp01((Time.time - introStarted) / 1.2f);
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(introFromY, introToY, t);
            transform.position = p;
            if (t >= 1f)
            {
                BeginIdle(0.6f);
            }
        }

        private void BeginIdle(float wait)
        {
            phase = Phase.Idle;
            float haste = enraged ? 0.7f : 1f;
            phaseEnds = Time.time + Mathf.Max(0.35f, wait * haste);
            HideMarker();
            if (contact != null)
            {
                contact.enabled = true;
            }
        }

        private void BeginAttack()
        {
            attackPick = PickAttack();
            phase = Phase.Attack;
            phaseEnds = Time.time + Windup(attackPick);
            if (attackPick == 0)
            {
                SlamTelegraph();
            }
            else if (attackPick == 3)
            {
                Transform player = PlayerController.PlayerTransform;
                chargeDir = player != null
                    ? ((Vector2)player.position - (Vector2)transform.position).normalized
                    : Vector2.down;
            }
        }

        private void TickAttack()
        {
            if (attackPick == 3 && Time.time >= phaseEnds - 0.45f)
            {
                transform.position = (Vector2)transform.position + chargeDir * (9.5f * Time.deltaTime);
            }

            if (Time.time < phaseEnds)
            {
                return;
            }

            FireAttack(attackPick);
            BeginIdle(enraged ? 0.85f : 1.25f);
        }

        private void FireAttack(int pick)
        {
            Vector2 origin = transform.position;
            Transform player = PlayerController.PlayerTransform;
            Vector2 target = player != null ? (Vector2)player.position : origin + Vector2.down;

            switch (pick)
            {
                case 0:
                    Slam(origin);
                    break;
                case 1:
                    Volley(origin, target);
                    break;
                case 2:
                    Summon(origin);
                    break;
                case 3:
                    break;
                case 4:
                    Teleport(target);
                    break;
                case 5:
                    Howl(origin);
                    break;
                case 6:
                    Breath(origin, target);
                    break;
                case 7:
                    Vanish(target);
                    break;
                default:
                    DropZones(target);
                    break;
            }

            HideMarker();
        }

        private static string AttackName(int pick)
        {
            switch (pick)
            {
                case 0: return "slam";
                case 1: return "volley";
                case 2: return "summoning";
                case 3: return "charging";
                case 4: return "teleport";
                case 5: return "howl";
                case 6: return "breath";
                case 7: return "vanishing";
                default: return "dropping zones";
            }
        }

        private int PickAttack()
        {
            int[] options = new int[10];
            int n = 0;
            if (Has(BossKit.Slam)) options[n++] = 0;
            if (Has(BossKit.Volley)) options[n++] = 1;
            if (Has(BossKit.Summon)) options[n++] = 2;
            if (Has(BossKit.Charge)) options[n++] = 3;
            if (Has(BossKit.Teleport)) options[n++] = 4;
            if (Has(BossKit.Howl)) options[n++] = 5;
            if (Has(BossKit.Breath)) options[n++] = 6;
            if (Has(BossKit.Vanish)) options[n++] = 7;
            if (Has(BossKit.Zones)) options[n++] = 8;
            if (n <= 0)
            {
                return 0;
            }

            return options[Random.Range(0, n)];
        }

        private float Windup(int pick)
        {
            switch (pick)
            {
                case 0: return 0.55f;
                case 3: return 0.7f;
                case 6: return 0.4f;
                case 7: return 0.35f;
                default: return 0.28f;
            }
        }

        private void SlamTelegraph()
        {
            if (landingMarker == null)
            {
                return;
            }

            Transform player = PlayerController.PlayerTransform;
            Vector2 point = player != null ? (Vector2)player.position : (Vector2)transform.position;
            landingMarker.transform.position = point;
            float size = (definition.specialRadius > 0.4f ? definition.specialRadius : 1.7f) * 2f;
            landingMarker.transform.localScale = new Vector3(size, size, 1f);
            landingMarker.enabled = true;
        }

        private void Slam(Vector2 origin)
        {
            Vector2 point = landingMarker != null && landingMarker.enabled
                ? (Vector2)landingMarker.transform.position
                : origin;
            float radius = definition.specialRadius > 0.4f ? definition.specialRadius : 1.7f;
            HurtPlayerInRadius(point, radius, definition.ScaledAttackDamage(runMinutes) * 1.15f);
            transform.position = point;
        }

        private void Volley(Vector2 origin, Vector2 target)
        {
            GameObject prefab = definition.projectilePrefab;
            if (prefab == null)
            {
                return;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            Vector2 forward = (target - origin).sqrMagnitude > 0.01f ? (target - origin).normalized : Vector2.down;
            int count = 5;
            float spread = 46f;
            float speed = definition.projectileSpeed > 0.1f ? definition.projectileSpeed : 7f;
            float damage = definition.ScaledAttackDamage(runMinutes);
            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                float offset = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
                float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg + offset;
                Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                HostileProjectile shot = pool.Spawn(prefab, origin, Quaternion.identity)?.GetComponent<HostileProjectile>();
                shot?.Launch(dir, speed, damage, 3.2f, definition.projectileRadius > 0.05f ? definition.projectileRadius : 0.3f, 1f, 0f, definition.projectileHoming, gameObject, 10f);
            }
        }

        private void Summon(Vector2 origin)
        {
            EnemyDefinition[] table = definition.summonAllies;
            EnemyDefinition fallback = definition.summonAlly;
            int count = definition.summonCount > 0 ? definition.summonCount : 3;
            for (int i = 0; i < count; i++)
            {
                EnemyDefinition pick = fallback;
                if (table != null && table.Length > 0)
                {
                    pick = table[Random.Range(0, table.Length)] ?? fallback;
                }

                if (pick == null)
                {
                    continue;
                }

                float angle = i / (float)count * Mathf.PI * 2f + 0.2f;
                Vector2 pos = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.2f;
                Enemy.SpawnConfigured(pick, pos, runMinutes);
            }
        }

        private void Teleport(Vector2 player)
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            Vector2 dest = player + Random.insideUnitCircle.normalized * 2.1f;
            if (bounds != null)
            {
                dest = bounds.ClampToPlayArea(dest);
            }

            transform.position = dest;
        }

        private void Howl(Vector2 origin)
        {
            float radius = 4.5f;
            float radiusSq = radius * radius;
            for (int i = 0; i < Enemy.Active.Count; i++)
            {
                Enemy other = Enemy.Active[i];
                if (other == null || !other.IsAlive || (other.Position - origin).sqrMagnitude > radiusSq)
                {
                    continue;
                }

                other.GetComponent<EnemyMovement>()?.ApplyHaste(1.5f, 3.2f);
            }
        }

        private void Breath(Vector2 origin, Vector2 target)
        {
            Vector2 dir = (target - origin).sqrMagnitude > 0.01f ? (target - origin).normalized : Vector2.down;
            HurtCone(origin, dir, 4.2f, 0.7f, definition.ScaledAttackDamage(runMinutes) * 0.9f);
        }

        private void Vanish(Vector2 player)
        {
            if (health != null)
            {
                health.Untargetable = true;
            }

            if (body != null)
            {
                Color c = body.color;
                c.a = 0.15f;
                body.color = c;
            }

            Teleport(player);
            if (health != null)
            {
                health.Untargetable = false;
            }

            if (body != null)
            {
                Color c = body.color;
                c.a = 1f;
                body.color = c;
            }
        }

        private void DropZones(Vector2 target)
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

            int count = 3;
            for (int i = 0; i < count; i++)
            {
                Vector2 pos = target + Random.insideUnitCircle * 1.4f;
                ArenaBounds bounds = ArenaBounds.Instance;
                if (bounds != null)
                {
                    pos = bounds.ClampToPlayArea(pos);
                }

                GroundHazard zone = pool.Spawn(prefab, pos, Quaternion.identity)?.GetComponent<GroundHazard>();
                zone?.Arm(1.05f, 3.4f, 3.2f, 0.65f, 0.9f, Has(BossKit.Zones) ? 3.5f : 0f);
            }
        }

        private void HurtPlayerInRadius(Vector2 point, float radius, float damage)
        {
            HitboxDisplay.Flash(point, radius, 0.35f);
            PlayerController player = PlayerController.Instance;
            if (player == null || player.Health == null || !player.Health.IsAlive)
            {
                return;
            }

            float reach = radius + 0.35f;
            if (((Vector2)player.transform.position - point).sqrMagnitude > reach * reach)
            {
                return;
            }

            Vector2 dir = ((Vector2)player.transform.position - point).normalized;
            player.Health.TakeDamage(new DamageInfo(damage, player.transform.position, dir, 0f, gameObject));
        }

        private void HurtCone(Vector2 origin, Vector2 forward, float length, float halfWidth, float damage)
        {
            HitboxDisplay.FlashCapsule(origin, forward, length, halfWidth * 2f, 0.35f);
            PlayerController player = PlayerController.Instance;
            if (player == null || player.Health == null)
            {
                return;
            }

            Vector2 toPlayer = (Vector2)player.transform.position - origin;
            float along = Vector2.Dot(toPlayer, forward);
            if (along < 0f || along > length)
            {
                return;
            }

            float side = Vector2.Dot(toPlayer, new Vector2(-forward.y, forward.x));
            if (Mathf.Abs(side) > halfWidth + 0.35f)
            {
                return;
            }

            player.Health.TakeDamage(new DamageInfo(damage, player.transform.position, forward, 0f, gameObject));
        }

        private bool Has(BossKit kit)
        {
            return definition != null && (definition.bossKit & kit) != 0;
        }

        private void HideMarker()
        {
            if (landingMarker != null)
            {
                landingMarker.enabled = false;
            }
        }

        private void StartBossMusic()
        {
            if (musicOn)
            {
                return;
            }

            musicOn = true;
            if (GameMusicController.Instance != null)
            {
                GameMusicController.Instance.SetBossMusic();
            }
        }

        private void RestoreMusic()
        {
            if (!musicOn)
            {
                return;
            }

            musicOn = false;
            if (GameMusicController.Instance != null)
            {
                GameMusicController.Instance.SetGameplayMusic();
            }
        }
    }
}
