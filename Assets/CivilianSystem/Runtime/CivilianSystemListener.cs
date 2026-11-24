// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEngine;
using UnityEngine.Events;

namespace CivilianSystem
{
    /// <summary>
    /// Inspector-friendly listener for everything happening in the civilian system.
    /// Sits on any GameObject; the UnityEvents are wired in the Inspector.
    ///
    /// Also exposes static status getters for polling from other scripts:
    ///   CivilianSystemListener.IsPlayerWanted
    ///   CivilianSystemListener.WantedLastKnownPosition
    ///   CivilianSystemListener.AliveCivilianCount
    /// </summary>
    public class CivilianSystemListener : MonoBehaviour
    {
        [System.Serializable] public class CivilianEvent : UnityEvent<Civilian> { }
        [System.Serializable] public class CivilianDamageEvent : UnityEvent<Civilian, float> { }

        [Header("Wanted / Police")]
        [Tooltip("Player became wanted (police witnessed a crime)")]
        public UnityEvent onPlayerWanted;
        [Tooltip("Heat expired or manually cleared")]
        public UnityEvent onWantedCleared;
        [Tooltip("Police completed an arrest")]
        public UnityEvent onPlayerArrested;

        [Header("NPC Life")]
        [Tooltip("Any civilian took damage (civilian, amount)")]
        public CivilianDamageEvent onNpcDamaged;
        [Tooltip("Any civilian died")]
        public CivilianEvent onNpcDeath;

        [Header("Interaction")]
        [Tooltip("Player started a conversation with an NPC (E key)")]
        public CivilianEvent onPlayerTalkStarted;

        // ---------- static status API (poll from anywhere) ----------

        public static bool IsPlayerWanted => WantedSystem.IsWanted;
        public static Vector3 WantedLastKnownPosition => WantedSystem.LastKnownPosition;
        public static int AliveCivilianCount => Civilian.AllCivilians.Count;

        /// <summary>Manually clear the wanted status (paid fine, mission scripted, etc).</summary>
        public static void ClearWanted() => WantedSystem.Clear();

        // ---------- wiring ----------

        void OnEnable()
        {
            WantedSystem.OnWanted += HandleWanted;
            WantedSystem.OnCleared += HandleCleared;
            WantedSystem.OnArrested += HandleArrested;
            Civilian.OnAnyDamaged += HandleDamaged;
            Civilian.OnAnyDeath += HandleDeath;
            Civilian.OnPlayerTalk += HandlePlayerTalk;
        }

        void OnDisable()
        {
            WantedSystem.OnWanted -= HandleWanted;
            WantedSystem.OnCleared -= HandleCleared;
            WantedSystem.OnArrested -= HandleArrested;
            Civilian.OnAnyDamaged -= HandleDamaged;
            Civilian.OnAnyDeath -= HandleDeath;
            Civilian.OnPlayerTalk -= HandlePlayerTalk;
        }

        void HandleWanted() => onPlayerWanted?.Invoke();
        void HandleCleared() => onWantedCleared?.Invoke();
        void HandleArrested() => onPlayerArrested?.Invoke();
        void HandleDamaged(Civilian c, float amount) => onNpcDamaged?.Invoke(c, amount);
        void HandleDeath(Civilian c) => onNpcDeath?.Invoke(c);
        void HandlePlayerTalk(Civilian c) => onPlayerTalkStarted?.Invoke(c);
    }
}
