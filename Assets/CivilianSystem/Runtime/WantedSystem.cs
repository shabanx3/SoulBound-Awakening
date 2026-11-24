// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using System;
using UnityEngine;

namespace CivilianSystem
{
    /// <summary>
    /// Global player wanted state. Police report crimes here, read last known position,
    /// and clear it on arrest.
    /// </summary>
    public static class WantedSystem
    {
        public static bool IsWanted { get; private set; }
        public static Vector3 LastKnownPosition { get; private set; }

        [Tooltip("Seconds until heat dies down without new sightings")]
        public static float wantedDuration = 60f;

        static float wantedUntil;

        public static event Action OnWanted;
        public static event Action OnCleared;
        public static event Action OnArrested;

        /// <summary>Police witnessed player attacking an NPC.</summary>
        public static void ReportCrime(Vector3 playerPos)
        {
            bool wasWanted = IsWanted;
            IsWanted = true;
            LastKnownPosition = playerPos;
            wantedUntil = Time.time + wantedDuration;
            if (!wasWanted) OnWanted?.Invoke();
        }

        /// <summary>Police currently sees the wanted player — refresh position + timer.</summary>
        public static void UpdateSighting(Vector3 playerPos)
        {
            if (!IsWanted) return;
            LastKnownPosition = playerPos;
            wantedUntil = Time.time + wantedDuration;
        }

        /// <summary>Call periodically (police civilians do). Expires old heat.</summary>
        public static void Tick()
        {
            if (IsWanted && Time.time > wantedUntil) Clear();
        }

        public static void Clear()
        {
            if (!IsWanted) return;
            IsWanted = false;
            OnCleared?.Invoke();
        }

        public static void Arrest()
        {
            OnArrested?.Invoke();
            IsWanted = false;
            OnCleared?.Invoke();
        }
    }
}
