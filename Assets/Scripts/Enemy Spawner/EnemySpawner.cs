using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class EnemyType
    {
        public string enemyName;
        public GameObject prefab;

        [Min(0.1f)]
        public float radius = 1f;

        [Min(0f)]
        public float spawnWeight = 1f;
    }

    [System.Serializable]
    public class Wave
    {
        public string waveName = "Wave";

        [Min(1)]
        public int enemyCount = 5;

        [Min(0f)]
        public float spawnDelay = 0.5f;

        public EnemyType[] enemies;
    }

    [Header("WAVE SYSTEM")]
    [SerializeField] private bool waveSystem = true;

    [SerializeField] private Wave[] waves;

    [Min(0f)]
    [SerializeField] private float timeBetweenWaves = 3f;

    [Header("SPAWN AREA")]
    [SerializeField] private float spawnRadius = 15f;

    [Header("SPAWN SETTINGS")]
    [SerializeField] private int maxSpawnAttempts = 50;

    [Header("HEIGHT")]
    [SerializeField] private bool useSpawnerY = true;
    [SerializeField] private float spawnY = 0f;

    [Header("GIZMOS")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showEnemyRadii = true;

    private readonly List<SpawnedEnemy> spawnedEnemies = new();

    private int currentWaveIndex = -1;
    private bool waveRunning;

    private class SpawnedEnemy
    {
        public Transform transform;
        public float radius;
    }

    private void Start()
    {
        if (waveSystem)
        {
            StartWaves();
        }
    }

    // =========================================================
    // WAVE SYSTEM
    // =========================================================

    public void StartWaves()
    {
        if (!waveSystem)
            return;

        if (waves == null || waves.Length == 0)
        {
            Debug.LogWarning("No waves assigned.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(WaveRoutine());
    }

    private IEnumerator WaveRoutine()
    {
        waveRunning = true;

        for (int i = 0; i < waves.Length; i++)
        {
            if (!waveSystem)
                break;

            currentWaveIndex = i;

            Debug.Log(
                $"Starting {waves[i].waveName}"
            );

            // Spawn current wave
            yield return StartCoroutine(
                SpawnWave(waves[i])
            );

            // Hold the next wave until every enemy from this one is dead.
            yield return new WaitUntil(
                () =>
                {
                    ClearDeadEnemies();
                    return spawnedEnemies.Count == 0;
                }
            );

            Debug.Log(
                $"{waves[i].waveName} completed."
            );

            // Wait before next wave
            if (i < waves.Length - 1)
            {
                yield return new WaitForSeconds(
                    timeBetweenWaves
                );
            }
        }

        waveRunning = false;

        Debug.Log("ALL WAVES COMPLETED");
    }

    // =========================================================
    // SPAWN WAVE
    // =========================================================

    private IEnumerator SpawnWave(Wave wave)
    {
        if (wave == null ||
            wave.enemies == null ||
            wave.enemies.Length == 0)
        {
            yield break;
        }

        for (int i = 0; i < wave.enemyCount; i++)
        {
            if (!waveSystem)
                yield break;

            TrySpawnEnemy(wave);

            yield return new WaitForSeconds(
                wave.spawnDelay
            );
        }
    }

    // =========================================================
    // SPAWN ENEMY
    // =========================================================

    private bool TrySpawnEnemy(Wave wave)
    {
        EnemyType enemyType = GetRandomEnemyType(wave);

        if (enemyType == null ||
            enemyType.prefab == null)
        {
            return false;
        }

        for (int attempt = 0;
             attempt < maxSpawnAttempts;
             attempt++)
        {
            Vector3 spawnPosition =
                GetRandomSpawnPosition();

            if (!IsPositionAvailable(
                    spawnPosition,
                    enemyType.radius))
            {
                continue;
            }

            GameObject enemy =
                Instantiate(
                    enemyType.prefab,
                    spawnPosition,
                    Quaternion.identity
                );

            spawnedEnemies.Add(
                new SpawnedEnemy
                {
                    transform = enemy.transform,
                    radius = enemyType.radius
                }
            );

            return true;
        }

        Debug.LogWarning(
            $"Could not find position for {enemyType.enemyName}"
        );

        return false;
    }

    // =========================================================
    // RANDOM ENEMY
    // =========================================================

    private EnemyType GetRandomEnemyType(Wave wave)
    {
        float totalWeight = 0f;

        foreach (EnemyType enemy in wave.enemies)
        {
            if (enemy != null &&
                enemy.prefab != null &&
                enemy.spawnWeight > 0)
            {
                totalWeight += enemy.spawnWeight;
            }
        }

        if (totalWeight <= 0)
            return null;

        float randomValue =
            Random.Range(0f, totalWeight);

        foreach (EnemyType enemy in wave.enemies)
        {
            if (enemy == null ||
                enemy.prefab == null ||
                enemy.spawnWeight <= 0)
            {
                continue;
            }

            randomValue -= enemy.spawnWeight;

            if (randomValue <= 0)
                return enemy;
        }

        return null;
    }

    // =========================================================
    // POSITION
    // =========================================================

    private Vector3 GetRandomSpawnPosition()
    {
        Vector2 randomPosition =
            Random.insideUnitCircle * spawnRadius;

        float y = useSpawnerY
            ? transform.position.y
            : spawnY;

        return new Vector3(
            transform.position.x + randomPosition.x,
            y,
            transform.position.z + randomPosition.y
        );
    }

    private bool IsPositionAvailable(
        Vector3 position,
        float newRadius)
    {
        ClearDeadEnemies();

        foreach (SpawnedEnemy enemy in spawnedEnemies)
        {
            float distance =
                Vector3.Distance(
                    position,
                    enemy.transform.position
                );

            float requiredDistance =
                newRadius + enemy.radius;

            if (distance < requiredDistance)
                return false;
        }

        return true;
    }

    // =========================================================
    // DEAD ENEMY CHECK
    // =========================================================

    private void ClearDeadEnemies()
    {
        spawnedEnemies.RemoveAll(
            enemy =>
                enemy == null ||
                enemy.transform == null
        );
    }

    // =========================================================
    // PUBLIC BOOL CHECK
    // =========================================================

    public bool IsWaveSystemActive()
    {
        return waveSystem;
    }

    public bool IsWaveRunning()
    {
        return waveRunning;
    }

    public int GetCurrentWave()
    {
        return currentWaveIndex + 1;
    }

    // Total enemies this spawner will produce across all waves.
    public int GetPlannedEnemyCount()
    {
        if (!waveSystem || waves == null)
            return 0;

        int total = 0;

        foreach (Wave wave in waves)
        {
            if (wave != null && wave.enemies != null && wave.enemies.Length > 0)
                total += wave.enemyCount;
        }

        return total;
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
            return;

        // Spawn range
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            spawnRadius
        );

        // Enemy radius
        if (!showEnemyRadii)
            return;

        foreach (SpawnedEnemy enemy in spawnedEnemies)
        {
            if (enemy == null ||
                enemy.transform == null)
                continue;

            Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(
                enemy.transform.position,
                enemy.radius
            );
        }
    }
}