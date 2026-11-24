using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// PLAYER SPELL CONFIG
// =========================================================
// Lives at Resources/PlayerSpellConfig. Holds every tunable
// number, VFX and SFX for the player's spells, aim assist,
// lock-on, heal drops and camera shake, so they can be
// tweaked in the Inspector without touching the Player prefab.
// Keys avoid the Malbers bindings (Q = Dodge, F = Mount,
// R = Heal flask, V = Malbers lock).
// =========================================================

[CreateAssetMenu(menuName = "Final Year Project/Player Spell Config", fileName = "PlayerSpellConfig")]
public class PlayerSpellConfig : ScriptableObject
{
    [System.Serializable]
    public class Spell
    {
        public string name = "Spell";
        public Key key = Key.Z;
        public float cooldown = 1.5f;
        public int damage = 30;
        [Tooltip("Seconds after the cast animation starts before the spell is released.")]
        public float releaseDelay = .3f;
        [Tooltip("Malbers mode + ability that plays the cast animation.")]
        public int modeID = 1;
        public int ability = 1;
        public GameObject castEffect;
        public GameObject mainEffect;
        public GameObject impactEffect;
        public float effectScale = 1f;
        public AudioClip castSound;
        public AudioClip impactSound;
    }

    [Header("Arcane Fire Bolt (homing projectile)")]
    public Spell fireBolt = new Spell { name = "Fire Bolt", key = Key.Z, cooldown = 1.1f, damage = 35, releaseDelay = .28f, modeID = 1, ability = 1 };
    [Tooltip("Ability used on alternate casts (left hand).")]
    public int fireBoltAltAbility = 2;
    public float boltSpeed = 28f;
    [Tooltip("How hard the bolt turns toward its target (degrees per second).")]
    public float boltHoming = 240f;
    public float boltLifetime = 3f;
    public float boltSplashRadius = 1.5f;

    [Header("Thunder Strike (needs a target)")]
    public Spell thunder = new Spell { name = "Thunder Strike", key = Key.X, cooldown = 6f, damage = 75, releaseDelay = .3f, modeID = 1, ability = 2, effectScale = 3f };
    public float thunderRadius = 3f;
    [Tooltip("Telegraph time between marking the target and the lightning landing.")]
    public float thunderDelay = .45f;
    public float thunderRange = 30f;
    [Tooltip("Without a lock, the nearest enemy within this angle of the camera is struck.")]
    public float thunderSearchAngle = 60f;

    [Header("Frost Nova (area around the player)")]
    public Spell frostNova = new Spell { name = "Frost Nova", key = Key.G, cooldown = 8f, damage = 55, releaseDelay = .9f, modeID = 4, ability = 27 };
    public float novaRadius = 6f;

    [Header("Shockwave (stuns enemies around the player)")]
    public Spell shockwave = new Spell { name = "Shockwave", key = Key.T, cooldown = 12f, damage = 20, releaseDelay = .55f, modeID = 1, ability = 4, effectScale = 3f };
    public float shockRadius = 7f;
    public float stunSeconds = 3f;
    [Tooltip("Shown on each stunned enemy for the stun duration.")]
    public GameObject stunEffect;
    public float stunEffectScale = 1f;

    [Header("Weapon (Malbers draw / store modes, as HAPWeaponScrollControl uses)")]
    [Tooltip("If a weapon is in hand it is stored before a spell and drawn again afterwards.")]
    public bool storeWeaponForSpells = true;
    public int drawWeaponModeID = 99;
    public int storeWeaponModeID = 98;
    [Tooltip("Longest wait for the store animation before the weapon is holstered instantly.")]
    public float storeWeaponTimeout = .9f;

    [Header("Aim Assist / Lock On")]
    public float lockRange = 30f;
    [Tooltip("A locked target further than this is released.")]
    public float lockBreakRange = 40f;
    [Tooltip("Where the lock-on camera looks on a target, as a fraction of its collider height (0 = feet, 1 = top).")]
    [Range(0f, 1f)] public float lockPointHeight = .7f;
    public float assistRange = 35f;
    [Tooltip("Max angle from the camera's centre for aim assist to pick a target.")]
    public float assistAngle = 18f;
    public GameObject lockMarkerEffect;
    public float lockMarkerScale = 1.2f;

    [Header("Heal Drops (from defeated enemies)")]
    [Range(0f, 1f)] public float healDropChance = .6f;
    [Range(0f, 1f)] public float summonHealDropChance = .25f;
    public int bossHealDrops = 3;
    public float healAmount = 25f;
    [Tooltip("Orbs drift to the player inside this range.")]
    public float healMagnetRange = 4f;
    public float healOrbLifetime = 30f;
    public GameObject healOrbEffect;
    public float healOrbScale = .45f;
    public GameObject healPickupEffect;
    public AudioClip healPickupSound;

    [Header("Perfect Dodge")]
    [Tooltip("Malbers Dodge mode ID (Q).")]
    public int dodgeModeID = 5;
    [Tooltip("Seconds after a dodge starts during which every hit is avoided (0 damage).")]
    public float perfectDodgeWindow = .45f;
    [Tooltip("Brief slow motion on a perfect dodge (0 = off).")]
    [Range(0f, 1f)] public float perfectDodgeTimeScale = .4f;
    public float perfectDodgeSlowMoSeconds = .25f;
    public AudioClip perfectDodgeSound;

    [Header("Enemy Health Bars")]
    [Tooltip("Emerald health bars only show for enemies within this range that are on screen and in line of sight.")]
    public float enemyBarRange = 25f;
    [Tooltip("Max angle from the camera's forward for an enemy to count as 'in front'.")]
    public float enemyBarViewAngle = 65f;
    [Tooltip("A damaged enemy keeps its bar visible this long, even off to the side.")]
    public float enemyBarAfterHitSeconds = 4f;

    [Header("Camera Shake (when the boss hurts the player)")]
    public float shakeMin = .35f;
    public float shakeMax = 1.6f;
    [Tooltip("Damage that produces the maximum shake.")]
    public float shakeDamageForMax = 60f;
}
