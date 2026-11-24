// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using System.Collections.Generic;
using UnityEngine;

namespace CivilianSystem
{
    /// <summary>
    /// World point where a civilian stops and performs a task (bench, market stall,
    /// well, notice board...). One occupant at a time — busy point gets ignored by others.
    /// Place in scene, optionally rotate: civilian faces the point's forward direction.
    /// </summary>
    public class TaskPoint : MonoBehaviour
    {
        [Tooltip("Animator trigger fired while performing (e.g. Task, Talk, Sit)")]
        public string animTrigger = "Task";
        [Tooltip("Min/max seconds a civilian spends here")]
        public Vector2 useDuration = new Vector2(3f, 7f);
        [Tooltip("Seconds the point stays unavailable after use")]
        public float cooldownAfterUse = 5f;

        public static readonly List<TaskPoint> All = new List<TaskPoint>();

        Civilian occupant;
        float freeAt;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool IsFree => occupant == null && Time.time >= freeAt;

        public bool TryClaim(Civilian c)
        {
            if (!IsFree) return false;
            occupant = c;
            return true;
        }

        public void Release(Civilian c)
        {
            if (occupant != c) return;
            occupant = null;
            freeAt = Time.time + cooldownAfterUse;
        }

        public float RollDuration() => Random.Range(useDuration.x, useDuration.y);

        /// <summary>Closest free point within radius, or null.</summary>
        public static TaskPoint FindFree(Vector3 pos, float radius)
        {
            TaskPoint best = null;
            float bestSqr = radius * radius;
            for (int i = 0; i < All.Count; i++)
            {
                var tp = All[i];
                if (!tp.IsFree) continue;
                float d = (tp.transform.position - pos).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = tp; }
            }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = (occupant != null) ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.8f);
        }
    }
}
