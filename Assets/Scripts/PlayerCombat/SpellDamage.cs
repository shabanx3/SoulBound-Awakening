using System.Collections.Generic;
using AbyssalWarden;
using EmeraldAI;
using UnityEngine;

// =========================================================
// SPELL DAMAGE
// =========================================================
// Routes player spell damage to whatever was hit:
// - The Abyssal Warden  -> AbyssalWardenBoss.TakeDamage
// - Emerald AI enemies  -> EmeraldHealth.Damage (it already
//                          ignores attackers it is friendly to,
//                          so allies are not hurt)
// =========================================================

public static class SpellDamage
{
    private static readonly Collider[] buffer = new Collider[64];

    /// <summary> Damages the boss / Emerald AI that owns this collider. Returns the damaged root, or null. </summary>
    public static GameObject Apply(Collider hit, int damage, Vector3 point, Transform attacker)
    {
        if (hit == null || damage <= 0) return null;
        if (attacker != null && hit.transform.IsChildOf(attacker)) return null;

        AbyssalWardenBoss boss = hit.GetComponentInParent<AbyssalWardenBoss>();
        if (boss != null)
        {
            if (boss.IsDead) return null;
            boss.TakeDamage(damage, damage * .45f, point);
            return boss.gameObject;
        }

        EmeraldHealth health = hit.GetComponentInParent<EmeraldHealth>();
        if (health != null && health.CurrentHealth > 0)
        {
            health.Damage(damage, attacker, 150);
            return health.gameObject;
        }
        return null;
    }

    /// <summary> Damages every boss / Emerald AI inside the sphere once. Returns the roots that were hit. </summary>
    public static List<GameObject> Area(Vector3 center, float radius, int damage, Transform attacker, GameObject skip = null)
    {
        int count = Physics.OverlapSphereNonAlloc(center, radius, buffer, ~0, QueryTriggerInteraction.Collide);
        var done = new HashSet<GameObject>();
        var hitRoots = new List<GameObject>();
        if (skip != null) done.Add(skip);
        for (int i = 0; i < count; i++)
        {
            Collider col = buffer[i];
            if (col == null || (attacker != null && col.transform.IsChildOf(attacker))) continue;
            GameObject root = Root(col);
            if (root == null || !done.Add(root)) continue;
            if (damage > 0) Apply(col, damage, col.ClosestPoint(center), attacker);
            hitRoots.Add(root);
        }
        return hitRoots;
    }

    private static GameObject Root(Collider col)
    {
        AbyssalWardenBoss boss = col.GetComponentInParent<AbyssalWardenBoss>();
        if (boss != null) return boss.gameObject;
        EmeraldHealth health = col.GetComponentInParent<EmeraldHealth>();
        return health != null ? health.gameObject : null;
    }
}
