using UnityEngine;
using EmeraldAI;
using EmeraldAI.Utility;

/// <summary>
/// Elden Ring-style boss behaviour for the Demon Lord.
/// Phase 2 triggers at 50% HP: brief invincibility window, taunt animation,
/// increased speed, and intensified VFX.
/// </summary>
public class DemonLordBoss : MonoBehaviour
{
    [Header("Phase Settings")]
    [SerializeField] private float phase2HealthThreshold = 0.5f;
    [SerializeField] private float phase2InvincibilityDuration = 3.5f;

    [Header("Phase 2 Movement")]
    [SerializeField] private float phase2WalkSpeed = 3.2f;
    [SerializeField] private float phase2RunSpeed = 8.5f;

    [Header("VFX References")]
    public ParticleSystem bodySmoke;
    public ParticleSystem phase2Smoke;
    public GameObject castBall;
    public GameObject swipeTrail;

    private EmeraldSystem emeraldSystem;
    private EmeraldHealth emeraldHealth;
    private EmeraldMovement emeraldMovement;

    private bool phase2Triggered;

    private void Start()
    {
        emeraldSystem  = GetComponent<EmeraldSystem>();
        emeraldHealth  = GetComponent<EmeraldHealth>();
        emeraldMovement = GetComponent<EmeraldMovement>();

        if (bodySmoke != null)
        {
            bodySmoke.gameObject.SetActive(true);
            bodySmoke.Play();
        }
    }

    private void Update()
    {
        if (phase2Triggered || emeraldHealth == null) return;

        float hpRatio = (float)emeraldHealth.CurrentHealth / Mathf.Max(1, emeraldHealth.StartingHealth);
        if (hpRatio <= phase2HealthThreshold)
            TriggerPhase2();
    }

    private void TriggerPhase2()
    {
        phase2Triggered = true;

        if (emeraldHealth != null)
            emeraldHealth.Immortal = true;

        // Play taunt / roar emote (emote ID 1 = taunt clip)
        if (emeraldSystem != null)
            EmeraldAPI.Animation.PlayEmoteAnimation(emeraldSystem, 1);

        Invoke(nameof(EndPhase2Invincibility), phase2InvincibilityDuration);

        if (phase2Smoke != null)
        {
            phase2Smoke.gameObject.SetActive(true);
            phase2Smoke.Play();
        }

        Debug.Log("<color=red>[Demon Lord] PHASE 2 — LORD OF INFERNO AWAKENS</color>");
    }

    private void EndPhase2Invincibility()
    {
        if (emeraldHealth != null)
            emeraldHealth.Immortal = false;

        if (emeraldMovement != null)
        {
            emeraldMovement.WalkSpeed = phase2WalkSpeed;
            emeraldMovement.RunSpeed  = phase2RunSpeed;
        }
    }

    // Called from Animation Events on attack animations
    public void OnAttackSwipeStart()
    {
        if (swipeTrail != null) swipeTrail.SetActive(true);
    }

    public void OnAttackSwipeEnd()
    {
        if (swipeTrail != null) swipeTrail.SetActive(false);
    }

    // Called from Animation Events on cast animations
    public void OnCastStart()
    {
        if (castBall != null) castBall.SetActive(true);
    }

    public void OnCastEnd()
    {
        if (castBall != null) castBall.SetActive(false);
    }
}
