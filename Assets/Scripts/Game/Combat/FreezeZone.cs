using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Enemies;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Ice crystal stuck to one enemy. The enemy stops and its animation holds, and the crystal hits once.
    /// </summary>
    public class FreezeZone : MonoBehaviour, IPoolable
    {
        private Health target;
        private EnemyMovement movement;
        private float radius = 1.1f;
        private float expireAt;
        private GameObject source;
        private HitboxMarker hitbox;
        private Animator[] heldAnimators = System.Array.Empty<Animator>();
        private float[] heldSpeeds = System.Array.Empty<float>();
        private int heldCount;

        public void OnSpawned()
        {
            KeepUnitScale();
            Animator animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }

            animator.Play(0, 0, 0f);
            animator.Update(0f);
        }

        public void OnDespawned()
        {
            ReleaseEnemyAnimation();
            HitboxDisplay.Release(hitbox);
            hitbox = null;
            target = null;
            movement = null;
            source = null;
        }

        public void Attach(Enemy enemy, float radiusValue, float damage, float duration, GameObject owner)
        {
            target = enemy != null ? enemy.Health : null;
            movement = enemy != null ? enemy.GetComponent<EnemyMovement>() : null;
            radius = Mathf.Max(0.45f, radiusValue);
            float life = Mathf.Max(0.3f, duration);
            expireAt = Time.time + life;
            source = owner;
            KeepUnitScale();
            // Bosses still take the hit. The crystal stays so the weapon cannot strike them again until it expires.
            if (enemy == null || !enemy.IsBoss)
            {
                HoldEnemyAnimation(enemy);
                if (movement != null)
                {
                    movement.ApplyFreeze(life);
                }
            }

            if (damage > 0.01f && target != null && target.IsAlive)
            {
                target.TakeDamage(PlayerHits.Strike(damage, target.Position, Vector2.up, 0f, source));
            }
        }

        private void LateUpdate()
        {
            KeepUnitScale();
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying)
            {
                return;
            }

            if (target == null || !target.IsAlive || Time.time >= expireAt)
            {
                Recycle();
                return;
            }

            HitboxDisplay.SyncCircle(ref hitbox, transform.position, radius * 0.45f);
        }

        private void KeepUnitScale()
        {
            Transform parent = transform.parent;
            if (parent == null)
            {
                transform.localScale = Vector3.one;
                return;
            }

            Vector3 parentScale = parent.lossyScale;
            transform.localScale = new Vector3(
                Inverse(parentScale.x),
                Inverse(parentScale.y),
                Inverse(parentScale.z));
        }

        private static float Inverse(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
        }

        private void HoldEnemyAnimation(Enemy enemy)
        {
            ReleaseEnemyAnimation();
            if (enemy == null)
            {
                return;
            }

            PooledObject enemyPool = enemy.GetComponent<PooledObject>();
            Animator[] found = enemy.GetComponentsInChildren<Animator>(true);
            if (heldAnimators.Length < found.Length)
            {
                heldAnimators = new Animator[found.Length];
                heldSpeeds = new float[found.Length];
            }

            int count = 0;
            for (int i = 0; i < found.Length; i++)
            {
                Animator body = found[i];
                if (body == null || body.transform == transform || body.transform.IsChildOf(transform))
                {
                    continue;
                }

                // Crystals, bolts, and other pooled effects can sit on the enemy.
                // Only the enemy prefab's own animators should hold.
                PooledObject owner = body.GetComponentInParent<PooledObject>();
                if (owner != enemyPool)
                {
                    continue;
                }

                heldAnimators[count] = body;
                heldSpeeds[count] = body.speed;
                body.speed = 0f;
                count++;
            }

            heldCount = count;
        }

        private void ReleaseEnemyAnimation()
        {
            for (int i = 0; i < heldCount; i++)
            {
                if (heldAnimators[i] != null)
                {
                    heldAnimators[i].speed = heldSpeeds[i];
                }

                heldAnimators[i] = null;
            }

            heldCount = 0;
        }

        private void Recycle()
        {
            HitboxDisplay.Release(hitbox);
            hitbox = null;
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
