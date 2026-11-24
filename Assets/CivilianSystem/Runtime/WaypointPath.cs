// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEngine;

namespace CivilianSystem
{
    public enum PathMode { Loop, PingPong, Once }

    /// <summary>Scene path made of child transforms (or explicit list). Assign to Civilian for patrol.</summary>
    public class WaypointPath : MonoBehaviour
    {
        public PathMode mode = PathMode.Loop;
        [Tooltip("Leave empty to auto-use children")]
        public Transform[] waypoints;

        void Awake()
        {
            if (waypoints == null || waypoints.Length == 0)
            {
                waypoints = new Transform[transform.childCount];
                for (int i = 0; i < transform.childCount; i++)
                    waypoints[i] = transform.GetChild(i);
            }
        }

        public int Count => waypoints != null ? waypoints.Length : 0;
        public Vector3 GetPoint(int i) => waypoints[Mathf.Clamp(i, 0, Count - 1)].position;

        /// <summary>Next index. direction flips for ping-pong. Returns -1 when 'Once' path finished.</summary>
        public int NextIndex(int current, ref int direction)
        {
            if (Count == 0) return -1;
            int next = current + direction;

            switch (mode)
            {
                case PathMode.Loop:
                    return (next + Count) % Count;
                case PathMode.PingPong:
                    if (next >= Count || next < 0) { direction = -direction; next = current + direction; }
                    return Mathf.Clamp(next, 0, Count - 1);
                case PathMode.Once:
                    return next >= Count ? -1 : next;
            }
            return -1;
        }

        void OnDrawGizmos()
        {
            var pts = (waypoints != null && waypoints.Length > 0) ? waypoints : null;
            int n = pts != null ? pts.Length : transform.childCount;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < n; i++)
            {
                Transform a = pts != null ? pts[i] : transform.GetChild(i);
                if (a == null) continue;
                Gizmos.DrawSphere(a.position, 0.15f);
                int j = (i + 1) % n;
                if (mode == PathMode.Once && j == 0) continue;
                Transform b = pts != null ? pts[j] : transform.GetChild(j);
                if (b != null) Gizmos.DrawLine(a.position, b.position);
            }
        }
    }
}
