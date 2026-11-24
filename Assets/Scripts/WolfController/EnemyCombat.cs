// -----------------------------------------------------------------------------
//  EnemyCombat.cs
//  Owns the swing: cooldown, wind-up timing, hit test, damage.
//  EnemyController decides WHEN to ask. This decides WHAT happens.
//
//  Damage goes through EmeraldAI.IDamageable, which the player's EmeraldHealth
//  implements. EmeraldMalbersDamageSync then mirrors it into Malbers MDamageable,
//  so Stats, reactions and death all fire on the player side.
// -----------------------------------------------------------------------------
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using EmeraldAI;

namespace EnemyAI
{
    [AddComponentMenu("Enemy AI/Enemy Combat")]
    public class EnemyCombat : MonoBehaviour
    {
        [System.Serializable]
        public class AttackMove
        {
            public string name = "Attack";
            [Tooltip("Animator trigger fired for this attack.")]
            public string animatorTrigger = "Attack";
            public int damage = 10;
            [Tooltip("Seconds from trigger to the hit test. Match the animation contact frame.")]
            public float hitDelay = 0.4f;
            [Tooltip("Seconds the whole attack occupies before another can start.")]
            public float duration = 1.2f;
            public float range = 2.4f;
            [Tooltip("Half-angle of the hit cone, degrees. 180 = all around.")]
            public float halfAngle = 60f;
            [Range(0f, 1f)] public float weight = 1f;
        }

        [Header("Attacks")]
        public AttackMove[] attacks = new AttackMove[]
        {
            new AttackMove()
        };

        [Header("Timing")]
        public float cooldown = 1.5f;
        [Tooltip("Random extra delay so a pack of enemies does not swing in sync.")]
        public Vector2 extraDelayRange = new Vector2(0f, 0.4f);

        [Header("Hit Test")]
        [Tooltip("Origin of the hit sphere. Leave empty to use this transform + height offset.")]
        public Transform hitOrigin;
        public float hitHeight = 1f;
        public LayerMask targetLayers = ~0;
        [Tooltip("Damage every valid target in the cone, not only the closest.")]
        public bool hitMultipleTargets = false;
        public int ragdollForce = 100;

        [Header("Feedback")]
        public AudioSource audioSource;
        public AudioClip[] swingSounds;
        public GameObject hitVFX;

        [Header("Debug")]
        public bool logHits = false;

        [Header("Events")]
        public UnityEvent OnAttackStart;
        public UnityEvent<int> OnHitLanded;
        public UnityEvent OnAttackMiss;

        float nextAttackTime;
        bool attacking;
        Animator animator;

        public bool IsAttacking { get { return attacking; } }

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
        }

        /// <summary>Called every frame by EnemyController while in Attack state. Safe to spam.</summary>
        public bool TryAttack(Transform target)
        {
            if (attacking || target == null) return false;
            if (Time.time < nextAttackTime) return false;
            if (attacks == null || attacks.Length == 0) return false;

            StartCoroutine(DoAttack(PickAttack(), target));
            return true;
        }

        AttackMove PickAttack()
        {
            if (attacks.Length == 1) return attacks[0];

            float total = 0f;
            for (int i = 0; i < attacks.Length; i++) total += Mathf.Max(0f, attacks[i].weight);
            if (total <= 0f) return attacks[0];

            float roll = Random.value * total;
            for (int i = 0; i < attacks.Length; i++)
            {
                roll -= Mathf.Max(0f, attacks[i].weight);
                if (roll <= 0f) return attacks[i];
            }
            return attacks[attacks.Length - 1];
        }

        IEnumerator DoAttack(AttackMove move, Transform target)
        {
            attacking = true;
            OnAttackStart.Invoke();

            if (animator != null && !string.IsNullOrEmpty(move.animatorTrigger))
                animator.SetTrigger(move.animatorTrigger);

            PlayRandom(swingSounds);

            yield return new WaitForSeconds(move.hitDelay);

            DoHitTest(move);

            float rest = Mathf.Max(0f, move.duration - move.hitDelay);
            if (rest > 0f) yield return new WaitForSeconds(rest);

            attacking = false;
            nextAttackTime = Time.time + cooldown + Random.Range(extraDelayRange.x, extraDelayRange.y);
        }

        void DoHitTest(AttackMove move)
        {
            Vector3 origin = hitOrigin != null
                ? hitOrigin.position
                : transform.position + Vector3.up * hitHeight;

            var hits = Physics.OverlapSphere(origin, move.range, targetLayers);
            bool landed = false;
            IDamageable closest = null;
            float closestDist = float.MaxValue;
            Vector3 closestPoint = origin;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;

                var dmg = hits[i].GetComponentInParent<IDamageable>();
                if (dmg == null) continue;

                Vector3 toTarget = hits[i].transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f &&
                    Vector3.Angle(transform.forward, toTarget) > move.halfAngle) continue;

                if (hitMultipleTargets)
                {
                    Apply(dmg, move, hits[i].ClosestPoint(origin));
                    landed = true;
                }
                else
                {
                    float d = toTarget.sqrMagnitude;
                    if (d < closestDist)
                    {
                        closestDist = d;
                        closest = dmg;
                        closestPoint = hits[i].ClosestPoint(origin);
                    }
                }
            }

            if (!hitMultipleTargets && closest != null)
            {
                Apply(closest, move, closestPoint);
                landed = true;
            }

            if (!landed) OnAttackMiss.Invoke();
        }

        void Apply(IDamageable damageable, AttackMove move, Vector3 point)
        {
            damageable.Damage(move.damage, transform, ragdollForce, false);

            // damageable.Damage(move.damage, transform);

            if (hitVFX != null) Instantiate(hitVFX, point, Quaternion.identity);
            if (logHits) Debug.Log($"[EnemyCombat] {name} hit for {move.damage}");
            OnHitLanded.Invoke(move.damage);
        }

        void PlayRandom(AudioClip[] clips)
        {
            if (audioSource == null || clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        /// <summary>Optional: call from an Animation Event instead of using hitDelay.</summary>
        public void AnimationHitEvent()
        {
            if (attacks != null && attacks.Length > 0) DoHitTest(attacks[0]);
        }

        void OnDrawGizmosSelected()
        {
            if (attacks == null || attacks.Length == 0) return;
            Vector3 origin = hitOrigin != null ? hitOrigin.position : transform.position + Vector3.up * hitHeight;
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(origin, attacks[0].range);
        }
    }
}
