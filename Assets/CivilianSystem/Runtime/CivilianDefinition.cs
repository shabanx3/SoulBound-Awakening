// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEngine;

namespace CivilianSystem
{
    public enum BehaviorType { Passive, Aggressive, Coward, Police }
    public enum RigType { Humanoid, Generic }

    /// <summary>
    /// ScriptableObject describing one civilian archetype.
    /// Create via: Assets > Create > Civilian System > Civilian Definition
    /// </summary>
    [CreateAssetMenu(menuName = "Civilian System/Civilian Definition", fileName = "NewCivilianDefinition")]
    public class CivilianDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string civilianName = "Civilian";
        public GameObject prefab;               // fully set-up civilian prefab (use Setup Tool)
        public RigType rigType = RigType.Humanoid;

        [Header("Stats")]
        public float maxHealth = 100f;
        public float walkSpeed = 1.8f;
        public float runSpeed = 4.5f;

        [Header("Behavior")]
        public BehaviorType behavior = BehaviorType.Passive;
        [Tooltip("Radius civilian notices threats / player")]
        public float perceptionRadius = 12f;
        [Tooltip("Coward: how far it runs from threat. Aggressive: chase give-up distance")]
        public float fleeOrChaseDistance = 20f;
        [Tooltip("Seconds frozen in place when hit, before reacting")]
        public float hitStaggerTime = 0.5f;
        [Tooltip("Seconds catching breath after escaping, before returning to routine")]
        public float fleeCooldown = 3f;

        [Header("Player Yield")]
        [Tooltip("Player closer than this while NPC walks casually -> NPC stops politely. 0 = disabled")]
        public float playerYieldDistance = 1.2f;
        [Tooltip("Seconds NPC waits after player contact before moving again")]
        public float playerYieldCooldown = 1.5f;

        [Header("Task Points")]
        [Range(0f, 1f), Tooltip("Chance per check (~1s) to use a free nearby TaskPoint")]
        public float taskPointChance = 0.15f;
        public float taskPointSearchRadius = 8f;

        [Header("NPC Conversations")]
        [Range(0f, 1f), Tooltip("Chance per encounter with a same-type civilian")]
        public float conversationChance = 0.3f;
        [Tooltip("Distance to notice a chat partner")]
        public float conversationRange = 2.5f;
        public Vector2 conversationDuration = new Vector2(4f, 8f);
        [Tooltip("Seconds before this civilian can chat again")]
        public float conversationCooldown = 25f;

        [Header("Police (Police behavior only)")]
        [Tooltip("Distance at which arrest begins")]
        public float arrestRange = 2f;
        [Tooltip("Seconds police must hold near the player to complete arrest")]
        public float arrestTime = 2f;
        [Tooltip("Seconds spent searching around last known position before giving up")]
        public float searchDuration = 15f;

        [Header("Chit-chat Lines")]
        public string[] chitChatLines =
        {
            "Nice weather today, no?",
            "Did you hear the news?",
            "Work never ends around here...",
            "How's the family doing?",
            "These roads need fixing.",
            "I heard wolves near the fields.",
        };
        public string[] playerTalkLines =
        {
            "Hello there, traveler.",
            "Stay safe out here.",
            "Need something from me?",
            "Fine day, isn't it?",
        };

        [Header("Combat (Aggressive only)")]
        public float attackDamage = 10f;
        public float attackRange = 1.6f;
        public float attackCooldown = 1.5f;

        [Header("Wander")]
        public float wanderRadius = 10f;
        public Vector2 idleTimeRange = new Vector2(2f, 6f);

        [Header("Loot")]
        public LootTable lootTable;
        [Tooltip("Seconds corpse stays before returning to pool")]
        public float corpseLifetime = 10f;

        [Header("World UI")]
        [Tooltip("Custom canvas prefab with CivilianWorldUIRefs component. Empty = default code-built UI. Generate a starter: Tools > Civilian System > Create World UI Prefab")]
        public GameObject uiPrefab;

        [Header("Audio")]
        public AudioClip[] idleClips;
        public AudioClip[] hurtClips;
        public AudioClip[] deathClips;
        public AudioClip[] attackClips;
        public AudioClip[] footstepClips;
    }
}
