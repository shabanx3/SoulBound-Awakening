using AbyssalWarden;
using EmeraldAI;
using UnityEngine;
using UnityEngine.AI;

// =========================================================
// ENEMY STUN
// =========================================================
// Stuns the Warden (AbyssalWardenBoss.Stun) or an Emerald AI.
// Emerald only plays a stun if its animation profile has a
// Stunned clip; when it does not, the AI is held in place with
// a slowed animator instead, so every enemy reacts.
// =========================================================

public class EnemyStun : MonoBehaviour
{
    private EmeraldSystem ai;
    private Animator animator;
    private NavMeshAgent agent;
    private GameObject effect;
    private float until, checkAt;
    private bool frozen;

    public static void Apply(GameObject target, float seconds, GameObject stunEffect, float effectScale, float bossMultiplier)
    {
        if (target == null || seconds <= 0f) return;

        AbyssalWardenBoss boss = target.GetComponentInParent<AbyssalWardenBoss>();
        if (boss != null)
        {
            float bossSeconds = seconds * bossMultiplier;
            if (boss.Stun(bossSeconds))
                WardenVFX.Spawn(stunEffect, boss.transform.position, boss.transform.rotation, bossSeconds, boss.transform, effectScale * 1.6f);
            return;
        }

        EmeraldSystem emerald = target.GetComponentInParent<EmeraldSystem>();
        if (emerald == null) return;
        EnemyStun stun = emerald.GetComponent<EnemyStun>();
        if (stun == null) stun = emerald.gameObject.AddComponent<EnemyStun>();
        stun.Begin(seconds, stunEffect, effectScale);
    }

    private void Begin(float seconds, GameObject stunEffect, float effectScale)
    {
        ai = GetComponent<EmeraldSystem>();
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();

        until = Mathf.Max(until, Time.time + seconds);
        if (effect == null) effect = WardenVFX.Spawn(stunEffect, transform.position, transform.rotation, 0f, transform, effectScale);

        if (ai != null && ai.CombatComponent != null) ai.CombatComponent.TriggerStun(seconds);
        checkAt = Time.time + .7f; // Emerald starts its stun after 0.5 s
        enabled = true;
    }

    private void LateUpdate()
    {
        bool dead = ai != null && ai.AnimationComponent != null && ai.AnimationComponent.IsDead;
        if (dead || Time.time >= until) { End(); return; }

        // No Stunned clip in the profile: Emerald ignored the stun, so hold the AI ourselves.
        if (!frozen && Time.time >= checkAt && animator != null && !HasActiveStun())
        {
            frozen = true;
            animator.speed = .12f;
        }

        if (frozen && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    private bool HasActiveStun()
    {
        foreach (AnimatorControllerParameter p in animator.parameters)
            if (p.name == "Stunned Active") return animator.GetBool("Stunned Active");
        return false;
    }

    private void End()
    {
        if (frozen)
        {
            if (animator != null) animator.speed = 1f;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        }
        frozen = false;
        if (effect != null) WardenVFX.StopAndDestroy(effect, .5f);
        effect = null;
        enabled = false;
    }

    private void OnDisable()
    {
        if (frozen && animator != null) animator.speed = 1f;
        frozen = false;
    }
}
