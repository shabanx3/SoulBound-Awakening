// -----------------------------------------------------------------------------
//  EnemyController.cs
//  Custom brain for an Emerald agent. Handles Idle / Patrol / Chase / Attack /
//  Dead and drives NavMeshAgent + Animator.
//
//  Keeps from Emerald:   Emerald Detection (target finding), Emerald Health.
//  Replaces from Emerald: movement, animation, combat behaviour.
//
//  Pairs with EnemyCombat.cs, which owns the swing, the timing and the damage.
//
//  Emerald's own Movement / Animation / Combat components are disabled on the
//  agent so they do not conflict with this script; Detection and Health stay on.
// -----------------------------------------------------------------------------
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using EmeraldAI;

namespace EnemyAI
{
    [RequireComponent(typeof(NavMeshAgent))]
    [AddComponentMenu("Enemy AI/Enemy Controller")]
    public class EnemyController : MonoBehaviour
    {
        public enum State { Idle, Patrol, Chase, Attack, Dead }
        public enum TargetSource { EmeraldDetection, PhysicsSphere, Manual }

        [Header("Target")]
        public TargetSource targetSource = TargetSource.EmeraldDetection;
        [Tooltip("Used by PhysicsSphere mode, and as fallback if Emerald Detection gives nothing.")]
        public LayerMask playerLayers = ~0;
        public string playerTag = "Player";

        [Header("Ranges")]
        public float detectRange = 15f;
        [Tooltip("Stops chasing past this. Keep bigger than detectRange so it does not flicker.")]
        public float loseRange = 22f;
        public float attackRange = 2.2f;

        [Header("Speeds")]
        public float walkSpeed = 1.5f;
        public float chaseSpeed = 4.5f;
        public float turnSpeed = 8f;

        [Header("Patrol")]
        public Transform[] waypoints;
        public bool randomWaypointOrder = false;
        public Vector2 idleWaitRange = new Vector2(2f, 5f);

        [Header("Animator Parameters")]
        public string speedParam = "Speed";
        public string dieParam = "Die";
        [Tooltip("Damp time on the Speed float. Smooths walk/run blending.")]
        public float speedDamp = 0.15f;

        [Header("Death")]
        public float destroyDelay = 8f;
        public bool disableCollidersOnDeath = true;

        [Header("Debug")]
        public bool drawGizmos = true;
        [SerializeField] State state = State.Idle;
        [SerializeField] Transform target;

        [Header("Events")]
        public UnityEvent OnDeath;
        public UnityEvent OnTargetAcquired;
        public UnityEvent OnTargetLost;

        NavMeshAgent agent;
        Animator animator;
        EmeraldHealth health;
        EnemyCombat combat;

        int waypointIndex;
        float idleTimer;
        bool dead;

        // Cached reflection handle for Emerald's detected-target member.
        // Reflection because Emerald renames this member between versions.
        static MemberInfo emeraldTargetMember;
        static bool emeraldTargetChecked;
        Component detection;

        public State CurrentState { get { return state; } }
        public Transform Target { get { return target; } }
        public bool IsDead { get { return dead; } }

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            combat = GetComponent<EnemyCombat>();
            health = GetComponentInParent<EmeraldHealth>();
            if (health == null) health = GetComponentInChildren<EmeraldHealth>();

            detection = GetComponentInChildren<EmeraldDetection>();

            agent.speed = walkSpeed;
            agent.stoppingDistance = 0f;
            agent.updateRotation = false;   // rotation handled here for tighter turns
        }

        void Update()
        {
            if (dead) return;

            if (health != null && health.CurrentHealth <= 0)
            {
                Die();
                return;
            }

            AcquireTarget();

            switch (state)
            {
                case State.Idle: TickIdle(); break;
                case State.Patrol: TickPatrol(); break;
                case State.Chase: TickChase(); break;
                case State.Attack: TickAttack(); break;
            }

            DriveAnimator();
        }

        // =====================================================================
        //  Target
        // =====================================================================
        void AcquireTarget()
        {
            Transform found = null;

            if (targetSource == TargetSource.Manual)
            {
                found = target;
            }
            else if (targetSource == TargetSource.EmeraldDetection)
            {
                found = ReadEmeraldTarget();
                if (found == null) found = SphereScan();   // fallback
            }
            else
            {
                found = SphereScan();
            }

            if (found != null && target == null) OnTargetAcquired.Invoke();

            if (found != null)
            {
                float d = Vector3.Distance(transform.position, found.position);
                if (d > loseRange) found = null;
            }

            if (found == null && target != null) OnTargetLost.Invoke();
            target = found;
        }

        Transform ReadEmeraldTarget()
        {
            if (detection == null) return null;

            if (!emeraldTargetChecked)
            {
                emeraldTargetChecked = true;
                var t = detection.GetType();
                string[] names = { "CurrentTarget", "TargetTransform", "LookAtTarget", "CombatTarget", "Target" };
                foreach (var n in names)
                {
                    var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
                    if (p != null && typeof(Transform).IsAssignableFrom(p.PropertyType)) { emeraldTargetMember = p; break; }
                    var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
                    if (f != null && typeof(Transform).IsAssignableFrom(f.FieldType)) { emeraldTargetMember = f; break; }
                }
            }

            if (emeraldTargetMember == null) return null;

            var prop = emeraldTargetMember as PropertyInfo;
            if (prop != null) return prop.GetValue(detection) as Transform;
            var field = emeraldTargetMember as FieldInfo;
            if (field != null) return field.GetValue(detection) as Transform;
            return null;
        }

        Transform SphereScan()
        {
            var hits = Physics.OverlapSphere(transform.position, detectRange, playerLayers);
            Transform best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                Transform root = hits[i].transform.root;
                if (!string.IsNullOrEmpty(playerTag) && !root.CompareTag(playerTag)) continue;

                float d = (root.position - transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = root; }
            }
            return best;
        }

        // =====================================================================
        //  States
        // =====================================================================
        void TickIdle()
        {
            agent.isStopped = true;

            if (target != null) { SetState(State.Chase); return; }

            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f && waypoints != null && waypoints.Length > 0)
                SetState(State.Patrol);
        }

        void TickPatrol()
        {
            if (target != null) { SetState(State.Chase); return; }
            if (waypoints == null || waypoints.Length == 0) { SetState(State.Idle); return; }

            agent.isStopped = false;
            agent.speed = walkSpeed;

            Transform wp = waypoints[waypointIndex];
            if (wp == null) { NextWaypoint(); return; }

            agent.SetDestination(wp.position);
            FaceMoveDirection();

            if (!agent.pathPending && agent.remainingDistance <= 0.4f)
            {
                NextWaypoint();
                idleTimer = Random.Range(idleWaitRange.x, idleWaitRange.y);
                SetState(State.Idle);
            }
        }

        void TickChase()
        {
            if (target == null) { idleTimer = 0.5f; SetState(State.Idle); return; }

            float dist = Vector3.Distance(transform.position, target.position);

            if (dist <= attackRange)
            {
                SetState(State.Attack);
                return;
            }

            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(target.position);
            FaceMoveDirection();
        }

        void TickAttack()
        {
            if (target == null) { SetState(State.Idle); return; }

            float dist = Vector3.Distance(transform.position, target.position);
            if (dist > attackRange * 1.15f)
            {
                SetState(State.Chase);
                return;
            }

            agent.isStopped = true;
            FaceTarget();

            if (combat != null) combat.TryAttack(target);
        }

        void SetState(State next)
        {
            if (state == next) return;
            state = next;
        }

        void NextWaypoint()
        {
            if (waypoints.Length == 0) return;
            waypointIndex = randomWaypointOrder
                ? Random.Range(0, waypoints.Length)
                : (waypointIndex + 1) % waypoints.Length;
        }

        // =====================================================================
        //  Rotation + animation
        // =====================================================================
        void FaceMoveDirection()
        {
            Vector3 v = agent.desiredVelocity;
            v.y = 0f;
            if (v.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(v), turnSpeed * Time.deltaTime);
        }

        void FaceTarget()
        {
            if (target == null) return;
            Vector3 v = target.position - transform.position;
            v.y = 0f;
            if (v.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(v), turnSpeed * Time.deltaTime);
        }

        void DriveAnimator()
        {
            if (animator == null || string.IsNullOrEmpty(speedParam)) return;
            float speed = agent.isStopped ? 0f : agent.velocity.magnitude;
            animator.SetFloat(speedParam, speed, speedDamp, Time.deltaTime);
        }

        // =====================================================================
        //  Death
        // =====================================================================
        public void Die()
        {
            if (dead) return;
            dead = true;
            state = State.Dead;

            if (agent != null && agent.enabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.enabled = false;
            }

            if (combat != null) combat.enabled = false;

            if (animator != null && !string.IsNullOrEmpty(dieParam))
                animator.SetTrigger(dieParam);

            if (disableCollidersOnDeath)
            {
                var cols = GetComponentsInChildren<Collider>();
                for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
            }

            OnDeath.Invoke();

            if (destroyDelay > 0f) StartCoroutine(DestroyLater());
        }

        IEnumerator DestroyLater()
        {
            yield return new WaitForSeconds(destroyDelay);
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectRange);
            Gizmos.color = Color.grey;   Gizmos.DrawWireSphere(transform.position, loseRange);
            Gizmos.color = Color.red;    Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
