using UnityEngine;
using AbyssalWarden;

// =========================================================
// ABYSSAL WARDEN -> MALBERS
// =========================================================
// Put on the Player. The Warden's melee hits and spells look
// for IWardenDamageable; this forwards them into the player's
// Malbers health through CustomEmeraldDamage.DamageToMDamage,
// so player death still runs the existing Stats events
// (e.g. MenuManager.ShowLevelFailPanel).
// =========================================================

[RequireComponent(typeof(CustomEmeraldDamage))]
public class PlayerWardenDamageReceiver : MonoBehaviour, IWardenDamageable
{
    [SerializeField] private float damageMultiplier = 1f;

    private CustomEmeraldDamage customDamage;

    private void Awake()
    {
        customDamage = GetComponent<CustomEmeraldDamage>();
    }

    public void ReceiveDamage(float amount, Transform attacker)
    {
        // Perfect dodge (Q at the right moment): the hit does nothing.
        if (PlayerDodgeGuard.TryAvoid(gameObject))
            return;

        int damage = Mathf.RoundToInt(amount * damageMultiplier);

        if (customDamage == null || damage <= 0)
            return;

        customDamage.DamageToMDamage(damage);

        // Boss melee, meteors, slams, breath... all land here: shake the camera with the hit.
        CameraShake.ShakeForDamage(damage, PlayerCombatBootstrap.Config);
    }
}
