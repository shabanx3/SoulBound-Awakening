using UnityEngine;
using MalbersAnimations.Controller;
using MalbersAnimations;
using EmeraldAI;

public class CustomEmeraldDamage : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private bool skipFirstDamage = false;

    private bool firstDamageSkipped = false;

    public int maxHealth = 100;

    // =========================================================
    // MALBERS -> EMERALD
    // =========================================================
    // Connected to:
    // MDamageable
    // -> Events
    // -> On Receiving Damage (Single)
    //
    // The event must pass the dynamic float value, not a fixed number.
    // =========================================================

    public void DamageMe(float damageAmount)
    {
        // Optional first-hit protection
        if (skipFirstDamage && !firstDamageSkipped)
        {
            firstDamageSkipped = true;

            Debug.Log(
                "CustomEmeraldDamage: First damage skipped."
            );

            return;
        }

        if (damageAmount <= 0f)
        {
            Debug.LogWarning(
                $"CustomEmeraldDamage: Invalid damage amount = {damageAmount}"
            );

            return;
        }

        EmeraldGeneralTargetBridge emerald =
            GetComponent<EmeraldGeneralTargetBridge>();

        if (emerald == null)
        {
            Debug.LogError(
                $"CustomEmeraldDamage: EmeraldGeneralTargetBridge not found on {gameObject.name}."
            );

            return;
        }

        // Convert Malbers' final damage to an integer
        // because Emerald's Damage() expects an int.
        int finalDamage =
            Mathf.RoundToInt(damageAmount);

        if (finalDamage <= 0)
            return;

        // Apply EXACT damage calculated by Malbers
        emerald.Damage(
            finalDamage,
            null,
            100,
            false
        );

        Debug.Log(
            $"<color=cyan>Malbers → Emerald</color> | " +
            $"Malbers Damage: {damageAmount} | " +
            $"Emerald Damage: {finalDamage} | " +
            $"Emerald Health: {emerald.Health}/{emerald.StartingHealth}"
        );
    }


    // =========================================================
    // DAMAGE EMERALD TO ZERO
    // =========================================================

    public void DamageToZeroEmerald()
    {
        EmeraldGeneralTargetBridge emerald =
            GetComponent<EmeraldGeneralTargetBridge>();

        if (emerald == null)
        {
            Debug.LogError(
                $"DamageToZeroEmerald: EmeraldGeneralTargetBridge not found on {gameObject.name}."
            );

            return;
        }

        int currentHealth =
            emerald.Health;

        if (currentHealth <= 0)
        {
            Debug.Log(
                "DamageToZeroEmerald: Emerald is already dead."
            );

            return;
        }

        emerald.Damage(
            currentHealth,
            null,
            maxHealth,
            false
        );

        Debug.Log(
            $"<color=red>Emerald killed.</color> " +
            $"Damage applied: {currentHealth}"
        );
    }


    // =========================================================
    // MALBERS STATE VALUE -> EMERALD
    // =========================================================

    public void DamageMeByStateValue(Stats state)
    {
        if (skipFirstDamage && !firstDamageSkipped)
        {
            firstDamageSkipped = true;
            return;
        }

        if (state == null)
        {
            Debug.LogWarning(
                "DamageMeByStateValue: Stats is null."
            );

            return;
        }

        if (state.stats == null ||
            state.stats.Count == 0)
        {
            Debug.LogWarning(
                "DamageMeByStateValue: Stats list is empty."
            );

            return;
        }

        IDamageable damageable =
            GetComponent<IDamageable>();

        if (damageable == null)
        {
            Debug.LogError(
                $"DamageMeByStateValue: IDamageable not found on {gameObject.name}."
            );

            return;
        }

        int stateValue =
            Mathf.RoundToInt(
                state.stats[0].Value
            );

        int damageAmount =
            Mathf.Max(
                0,
                maxHealth - stateValue
            );

        if (damageAmount <= 0)
            return;

        damageable.Damage(
            damageAmount,
            null,
            0,
            false
        );

        Debug.Log(
            $"<color=yellow>State → Emerald</color> | " +
            $"State Value: {stateValue} | " +
            $"Damage: {damageAmount}"
        );
    }


    // =========================================================
    // EMERALD -> MALBERS
    // =========================================================
    // Uses the damage value stored by EmeraldGeneralTargetBridge.
    // =========================================================

    public void DamageToMDamage()
    {
        EmeraldGeneralTargetBridge emerald =
            GetComponent<EmeraldGeneralTargetBridge>();

        if (emerald == null)
        {
            Debug.LogError(
                "DamageToMDamage: EmeraldGeneralTargetBridge not found."
            );

            return;
        }

        int damageAmount =
            emerald.LastReceivedDamage;

        Debug.Log(
            $"<color=cyan>Emerald → Malbers</color> | " +
            $"Damage received by bridge: {damageAmount}"
        );

        if (damageAmount <= 0)
        {
            Debug.LogWarning(
                "DamageToMDamage: LastReceivedDamage is 0."
            );

            return;
        }

        DamageToMDamage(
            damageAmount
        );
    }


    // =========================================================
    // EMERALD -> MALBERS WITH SPECIFIC DAMAGE
    // =========================================================

    public void DamageToMDamage(int damageAmount)
    {
        if (damageAmount <= 0)
            return;

        MDamageable mDamageable =
            GetComponent<MDamageable>();

        if (mDamageable == null)
        {
            Debug.LogError(
                $"DamageToMDamage: MDamageable not found on {gameObject.name}."
            );

            return;
        }

        Stats stats =
            GetComponent<Stats>();

        if (stats == null)
        {
            Debug.LogError(
                $"DamageToMDamage: Stats not found on {gameObject.name}."
            );

            return;
        }

        if (stats.stats == null ||
            stats.stats.Count == 0)
        {
            Debug.LogError(
                "DamageToMDamage: Stats list is empty."
            );

            return;
        }

        // Emerald AI keeps Health as the first entry in the stat list.
        StatID healthID =
            stats.stats[0].ID;

        Debug.Log(
            $"<color=red>Emerald → Malbers</color> | " +
            $"Damage: {damageAmount} | " +
            $"Health ID: {healthID}"
        );

        mDamageable.ReceiveDamage(
            healthID,
            damageAmount
        );
    }


    // =========================================================
    // DAMAGE MALBERS TO ZERO
    // =========================================================

    public void DamageToZeroMalbers()
    {
        MDamageable mDamageable =
            GetComponent<MDamageable>();

        Stats stats =
            GetComponent<Stats>();

        if (mDamageable == null)
        {
            Debug.LogError(
                $"DamageToZeroMalbers: MDamageable not found on {gameObject.name}."
            );

            return;
        }

        if (stats == null)
        {
            Debug.LogError(
                $"DamageToZeroMalbers: Stats not found on {gameObject.name}."
            );

            return;
        }

        if (stats.stats == null ||
            stats.stats.Count == 0)
        {
            Debug.LogError(
                "DamageToZeroMalbers: Stats list is empty."
            );

            return;
        }

        StatID healthID =
            stats.stats[0].ID;

        float currentHealth =
            stats.stats[0].Value;

        if (currentHealth <= 0f)
        {
            Debug.Log(
                "DamageToZeroMalbers: Malbers is already dead."
            );

            return;
        }

        mDamageable.ReceiveDamage(
            healthID,
            currentHealth
        );

        Debug.Log(
            $"<color=red>Malbers killed.</color> " +
            $"Damage applied: {currentHealth}"
        );
    }


    // =========================================================
    // REGENERATE EMERALD HEALTH
    // =========================================================

    public void Health_Regenrate()
    {
        EmeraldGeneralTargetBridge emerald =
            GetComponent<EmeraldGeneralTargetBridge>();

        Stats stats =
            GetComponent<Stats>();

        if (emerald == null)
        {
            Debug.LogWarning(
                "Health_Regenrate: EmeraldGeneralTargetBridge not found."
            );

            return;
        }

        if (stats == null)
        {
            Debug.LogWarning(
                "Health_Regenrate: Stats not found."
            );

            return;
        }

        if (stats.stats == null ||
            stats.stats.Count == 0)
        {
            Debug.LogWarning(
                "Health_Regenrate: Stats list is empty."
            );

            return;
        }

        int health =
            Mathf.RoundToInt(
                stats.stats[0].Value
            );

        health =
            Mathf.Clamp(
                health,
                0,
                emerald.StartingHealth
            );

        emerald.Health =
            health;

        Debug.Log(
            $"<color=green>Emerald Health Regenerated:</color> {health}"
        );
    }


    // =========================================================
    // RESET DAMAGE STATE
    // =========================================================

    public void ResetDamageState()
    {
        firstDamageSkipped = false;

        Debug.Log(
            "CustomEmeraldDamage: Damage state reset."
        );
    }


    /// <summary> Heals both health systems by an amount (used by enemy heal drops). </summary>
    public void Heal(float amount)
    {
        if (amount <= 0f)
            return;

        // Emerald
        EmeraldGeneralTargetBridge emerald =
            GetComponent<EmeraldGeneralTargetBridge>();

        if (emerald != null)
        {
            emerald.Health = Mathf.Min(
                emerald.StartingHealth,
                emerald.Health + Mathf.RoundToInt(amount)
            );
        }

        // Malbers
        Stats stats =
            GetComponent<Stats>();

        if (stats != null &&
            stats.stats != null &&
            stats.stats.Count > 0)
        {
            stats.stats[0].Value = Mathf.Min(
                stats.stats[0].MaxValue,
                stats.stats[0].Value + amount
            );

            Debug.Log(
                $"<color=green>Healed:</color> +{amount} | Malbers Health: {stats.stats[0].Value}"
            );
        }
    }

    public void RegenerateHealthToMax()
    {
        // Emerald
        EmeraldGeneralTargetBridge emerald =
            GetComponent<EmeraldGeneralTargetBridge>();

        if (emerald != null)
        {
            emerald.Health = emerald.StartingHealth;

            Debug.Log(
                $"<color=green>Emerald Health Regenerated To Max:</color> {emerald.Health}"
            );
        }

        // Malbers
        Stats stats =
            GetComponent<Stats>();

        if (stats != null &&
            stats.stats != null &&
            stats.stats.Count > 0)
        {
            // Set Malbers health stat to its maximum value
            stats.stats[0].Value = stats.stats[0].MaxValue;

            Debug.Log(
                $"<color=green>Malbers Health Regenerated To Max:</color> {stats.stats[0].Value}"
            );
        }
    }
}