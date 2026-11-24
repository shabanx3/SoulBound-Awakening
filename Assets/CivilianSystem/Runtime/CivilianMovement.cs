// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEngine;
using UnityEngine.AI;

namespace CivilianSystem
{
    /// <summary>
    /// Wraps NavMeshAgent. Handles: waypoint paths, wander, flee, chase.
    /// Obstacle avoidance = NavMeshAgent built-in avoidance (agents + NavMeshObstacle carve).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CivilianMovement : MonoBehaviour
    {
        NavMeshAgent agent;
        float walkSpeed, runSpeed;
        float stuckTimer;
        int basePriority = 50;

        [Tooltip("Extra arrival slack — crowds can't reach exact points")]
        public float arrivalTolerance = 0.6f;
        [Tooltip("Seconds at near-zero speed with active path before counted as stuck/arrived")]
        public float stuckTimeout = 2.5f;

        public NavMeshAgent Agent => agent;
        public float CurrentSpeed => agent.velocity.magnitude;

        public bool HasArrived
        {
            get
            {
                if (!agent.enabled || !agent.isOnNavMesh) return true; // can't move = don't wait forever
                if (agent.pathPending) return false;
                if (agent.remainingDistance <= agent.stoppingDistance + arrivalTolerance) return true;
                return stuckTimer >= stuckTimeout; // jammed by crowd = treat as arrived
            }
        }

        void Update()
        {
            // stuck detection: has a path but barely moving
            if (agent != null && agent.enabled && agent.isOnNavMesh &&
                agent.hasPath && !agent.pathPending && agent.velocity.sqrMagnitude < 0.01f)
                stuckTimer += Time.deltaTime;
            else
                stuckTimer = 0f;
        }

        public void Init(CivilianDefinition def)
        {
            agent = GetComponent<NavMeshAgent>();
            walkSpeed = def.walkSpeed;
            runSpeed = def.runSpeed;
            agent.speed = walkSpeed;
            agent.stoppingDistance = 0.4f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            basePriority = Random.Range(30, 80);   // varied priority = crowds resolve instead of deadlock
            agent.avoidancePriority = basePriority;
            agent.autoBraking = true;
            stuckTimer = 0f;
        }

        public void SetEnabled(bool on)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (on && !agent.enabled) agent.enabled = true;
            else if (!on && agent.enabled) { agent.ResetPath(); agent.enabled = false; }
        }

        /// <summary>Proper agent placement — snaps to mesh, resets internal path state.
        /// Use instead of setting transform.position on a live agent.</summary>
        public bool Warp(Vector3 pos)
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (!agent.enabled) return false;
            return agent.Warp(pos);
        }

        public void Walk() => agent.speed = walkSpeed;
        public void Run() => agent.speed = runSpeed;

        public bool MoveTo(Vector3 destination)
        {
            if (!agent.enabled || !agent.isOnNavMesh) return false;
            agent.isStopped = false;
            agent.avoidancePriority = basePriority; // back to normal walker
            stuckTimer = 0f;
            return agent.SetDestination(destination);
        }

        public void Stop()
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;            // hard brake
                agent.velocity = Vector3.zero;     // no sliding while staggered / tasking / dying
                agent.avoidancePriority = 5;       // highest priority: walkers route AROUND, can't push
            }
        }

        /// <summary>Call every frame in stationary states — crowd avoidance keeps trying to
        /// shove stopped agents around, which makes them float/skate. This pins them.</summary>
        public void HoldStill()
        {
            if (agent.enabled && agent.isOnNavMesh)
                agent.velocity = Vector3.zero;
        }

        /// <summary>Random reachable point around origin.</summary>
        public bool TryGetWanderPoint(Vector3 origin, float radius, out Vector3 point)
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 candidate = origin + (Vector3)(Random.insideUnitCircle * radius).ToXZ();
                if (NavMesh.SamplePosition(candidate, out var hit, 2f, NavMesh.AllAreas))
                {
                    point = hit.position;
                    return true;
                }
            }
            point = origin;
            return false;
        }

        /// <summary>Point away from threat, sampled on navmesh.</summary>
        public bool TryGetFleePoint(Vector3 threat, float distance, out Vector3 point)
        {
            Vector3 dir = (transform.position - threat).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Random.insideUnitSphere;
            dir.y = 0;

            for (int i = 0; i < 4; i++)
            {
                // rotate direction a bit each retry so civilian not stuck vs wall
                Vector3 tryDir = Quaternion.Euler(0, Random.Range(-40f, 40f) * i, 0) * dir;
                Vector3 candidate = transform.position + tryDir.normalized * distance;
                if (NavMesh.SamplePosition(candidate, out var hit, 4f, NavMesh.AllAreas))
                {
                    point = hit.position;
                    return true;
                }
            }
            point = transform.position;
            return false;
        }
    }

    static class VecExt
    {
        public static Vector3 ToXZ(this Vector2 v) => new Vector3(v.x, 0f, v.y);
    }
}
