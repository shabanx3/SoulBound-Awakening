// -----------------------------------------------------------------------------
//  EmeraldMalbersDamageSync.cs
//  ONE script, put on PLAYER root and on every EMERALD ENEMY root.
//  Implements NO interfaces - nothing to break when Malbers changes IMDamage.
//
//  PLAYER mode  (EmeraldHealth + MDamageable on the object):
//      Emerald enemy hits player -> EmeraldHealth.CurrentHealth drops
//      -> script sees drop -> MDamageable.ReceiveDamage(healthStatID, delta)
//      -> Malbers Stats + reaction + death run normally.
//
//  ENEMY mode   (EmeraldHealth + Stats used as a relay buffer):
//      HAP weapon hits enemy -> MDamageable drains the Malbers Health stat
//      -> script sees the drop -> forwards it to Emerald -> Emerald handles
//      health, hit reaction, ragdoll, aggro -> script refills the buffer stat.
//
//  Mode auto-detects. Override in the inspector if the guess is wrong.
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Events;
using EmeraldAI;
using MalbersAnimations;

namespace HAPEmeraldBridge
{
    [DisallowMultipleComponent]
    [AddComponentMenu("HAP Emerald Bridge/Emerald Malbers Damage Sync")]
    public class EmeraldMalbersDamageSync : MonoBehaviour
    {
        public enum SyncMode { Auto, Player_EmeraldToMalbers, Enemy_MalbersToEmerald }

        [Header("Mode")]
        public SyncMode mode = SyncMode.Auto;

        [Header("Required")]
        [Tooltip("Health StatID asset. Same one your Stats component uses.")]
        public StatID healthStatID;

        [Header("Scaling")]
        [Tooltip("Emerald damage -> Malbers damage multiplier (player mode).")]
        public float emeraldToMalbers = 1f;
        [Tooltip("Malbers damage -> Emerald damage multiplier (enemy mode).")]
        public float malbersToEmerald = 1f;

        [Header("Enemy Mode")]
        [Tooltip("Refill the Malbers buffer stat after relaying, so it never 'dies' on the Malbers side.")]
        public bool refillBufferStat = true;
        [Tooltip("Ragdoll push force passed to Emerald on each relayed hit.")]
        public int ragdollForce = 100;

        [Header("Debug")]
        public bool logDamage = false;

        [Header("Events")]
        public UnityEvent<float> OnDamageToMalbers;
        public UnityEvent<int> OnDamageToEmerald;

        EmeraldHealth emeraldHealth;
        IDamageable emeraldDamageable;
        MDamageable mDamageable;
        Stats stats;
        Stat healthStat;

        int lastEmeraldHealth;
        float lastMalbersHealth;
        SyncMode activeMode;

        void Awake()
        {
            emeraldHealth = GetComponentInParent<EmeraldHealth>();
            if (emeraldHealth == null) emeraldHealth = GetComponentInChildren<EmeraldHealth>();

            // EmeraldHealth implements IDamageable, so damage goes through that
            // interface: no EmeraldAPI call, and nothing protected is touched.
            if (emeraldHealth != null) emeraldDamageable = emeraldHealth as IDamageable;

            mDamageable = GetComponentInParent<MDamageable>();
            if (mDamageable == null) mDamageable = GetComponentInChildren<MDamageable>();

            stats = GetComponentInParent<Stats>();
            if (stats == null) stats = GetComponentInChildren<Stats>();

            if (stats != null && healthStatID != null) healthStat = stats.Stat_Get(healthStatID);

            activeMode = mode;
            if (activeMode == SyncMode.Auto)
            {
                // Enemies carry the full Emerald AI brain. The player only carries
                // EmeraldHealth + EmeraldDetection, so no brain = player.
                bool isAI = GetComponentInParent<EmeraldSystem>() != null;
                activeMode = isAI ? SyncMode.Enemy_MalbersToEmerald
                                  : SyncMode.Player_EmeraldToMalbers;

            }

            if (emeraldHealth == null)
                Debug.LogError("[DamageSync] No EmeraldHealth on " + name, this);
            if (healthStatID == null)
                Debug.LogError("[DamageSync] healthStatID not assigned on " + name, this);
        }

        void OnEnable()
        {
            lastEmeraldHealth = ReadEmerald();
            lastMalbersHealth = ReadMalbers();
        }

        void LateUpdate()
        {
            if (activeMode == SyncMode.Player_EmeraldToMalbers) TickPlayer();
            else TickEnemy();
        }

        // =====================================================================
        //  PLAYER: Emerald health drop -> Malbers MDamageable
        // =====================================================================
        void TickPlayer()
        {
            if (emeraldHealth == null || mDamageable == null) return;

            int current = ReadEmerald();
            if (current < lastEmeraldHealth)
            {
                float delta = (lastEmeraldHealth - current) * emeraldToMalbers;
                if (delta > 0f)
                {
                    mDamageable.ReceiveDamage(healthStatID, delta);

                    if (logDamage) Debug.Log($"[DamageSync] Emerald -> Malbers {delta} on {name}");
                    OnDamageToMalbers.Invoke(delta);
                }
            }
            lastEmeraldHealth = current;
        }

        // =====================================================================
        //  ENEMY: Malbers stat drop -> Emerald damage
        // =====================================================================
        void TickEnemy()
        {
            if (emeraldHealth == null || healthStat == null) return;

            float current = ReadMalbers();
            if (current < lastMalbersHealth)
            {
                float delta = (lastMalbersHealth - current) * malbersToEmerald;
                ApplyDamageToEmerald(delta, null);

                if (refillBufferStat)
                {
                    RefillBuffer();
                    lastMalbersHealth = ReadMalbers();
                    return;
                }
            }
            lastMalbersHealth = current;
        }

        /// <summary>Public entry point. Wire it to any UnityEvent that carries a float.</summary>
        public void ApplyDamageToEmerald(float amount)
        {
            ApplyDamageToEmerald(amount, null);
        }

        public void ApplyDamageToEmerald(float amount, Transform attacker)
        {
            if (emeraldDamageable == null || amount <= 0f) return;

            int final = Mathf.Max(1, Mathf.RoundToInt(amount));

            // Same entry point Emerald uses internally. Handles health, hit
            // reaction, ragdoll force and aggro onto the attacker.
            emeraldDamageable.Damage(final, attacker, ragdollForce, false);

            // emeraldDamageable.Damage(final, attacker);

            if (logDamage) Debug.Log($"[DamageSync] Malbers -> Emerald {final} on {name}");
            OnDamageToEmerald.Invoke(final);
        }

        // =====================================================================
        //  Readers
        // =====================================================================
        int ReadEmerald()
        {
            return emeraldHealth == null ? 0 : emeraldHealth.CurrentHealth;
        }

        float ReadMalbers()
        {
            return healthStat == null ? 0f : healthStat.Value;
        }

        void RefillBuffer()
        {
            if (healthStat == null) return;
            healthStat.SetValue(healthStat.MaxValue);

            // --- FALLBACK if SetValue does not exist ---
            // healthStat.Value = healthStat.MaxValue;
        }
    }
}