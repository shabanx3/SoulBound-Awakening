using UnityEngine;
using MalbersAnimations;
using MalbersAnimations.Reactions;
using AbyssalWarden;

// =========================================================
// MALBERS -> ABYSSAL WARDEN
// =========================================================
// Put on The Abyssal Warden (same object as AbyssalWardenBoss).
// Malbers weapons / attack triggers look for IMDamage in the
// hit collider's parents, so this receives the player's hits
// and forwards the final damage to AbyssalWardenBoss.TakeDamage.
// =========================================================

[RequireComponent(typeof(AbyssalWardenBoss))]
public class WardenMalbersDamage : MonoBehaviour, IMDamage
{
    [Tooltip("Multiplier applied to the Malbers damage before it reaches the Warden.")]
    [SerializeField] private float damageMultiplier = 1f;

    [Tooltip("Fraction of the damage that also builds stagger (matches AbyssalWardenBoss.ReceiveDamage).")]
    [SerializeField] private float staggerFraction = 0.45f;

    [SerializeField] private bool debugLog = false;

    private AbyssalWardenBoss boss;

    public Vector3 HitDirection { get; set; }
    public Vector3 HitPosition { get; private set; }
    public Transform Transform => transform;
    public GameObject Damager { get; set; }
    public GameObject Damagee => gameObject;
    public Collider HitCollider { get; set; }
    public ForceMode LastForceMode { get; set; }
    public SurfaceID Surface => null;

    private void Awake()
    {
        boss = GetComponent<AbyssalWardenBoss>();
    }

    public void ReceiveDamage(Vector3 Direction, Vector3 Position, GameObject Damager, StatModifier stat,
        bool IsCritical, Reaction2 custom, bool ignoreDamageeM, StatElement element, bool missed)
    {
        if (missed) return;

        HitDirection = Direction;
        HitPosition = Position;
        this.Damager = Damager;

        ApplyDamage(stat.Value, Position);
    }

    public void ReceiveDamage(StatID stat, float amount)
    {
        ApplyDamage(amount);
    }

    public void Profile_Set(string name) { }

    public void Profile_Restore() { }

    private void ApplyDamage(float amount, Vector3? hitPoint = null)
    {
        if (boss == null || boss.IsDead)
            return;

        float damage = Mathf.Abs(amount) * damageMultiplier;
        if (damage <= 0f)
            return;

        boss.TakeDamage(damage, damage * staggerFraction, hitPoint);

        if (debugLog)
            Debug.Log($"<color=cyan>Malbers → Warden</color> | Damage: {damage} | Warden Health: {boss.Health}/{boss.maxHealth}");
    }
}
