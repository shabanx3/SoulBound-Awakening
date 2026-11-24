using UnityEngine;
using System.Collections;

public class EnemyDeathEffect : MonoBehaviour
{
    [Header("Death Effect")]
    [SerializeField] private GameObject deathParticlePrefab;
    [SerializeField] private float deathDelay = 2f;

    private bool isDead;

    /// <summary> False for boss summons, so they do not change the leadership kill count. </summary>
    public bool CountsTowardLeadership { get; set; } = true;

    public void Die()
    {
        if (isDead) return;

        isDead = true;
        if (CountsTowardLeadership)
            LeadershipSessionTracker.ReportEnemyKilled();

        PlayerSpellConfig config = PlayerCombatBootstrap.Config;
        HealDrop.TrySpawn(transform.position, CountsTowardLeadership ? config.healDropChance : config.summonHealDropChance);
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        // Save the death position.
        Vector3 deathPosition = transform.position;
        Quaternion deathRotation = transform.rotation;

        // Allow the death animation to play.
        yield return new WaitForSeconds(deathDelay);

        // Spawn death particle at the original death position.
        if (deathParticlePrefab != null)
        {
            Instantiate(
                deathParticlePrefab,
                deathPosition,
                deathRotation
            );
        }

        // Destroy the enemy.
        Destroy(gameObject);
    }
}