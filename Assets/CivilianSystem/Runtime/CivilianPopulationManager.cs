// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace CivilianSystem
{
    /// <summary>
    /// Controls population: spawns civilians up to targets, respawns after deaths,
    /// runs distance-based on/off culling around the player, owns the pool.
    /// One per scene.
    /// </summary>
    [RequireComponent(typeof(CivilianPool))]
    public class CivilianPopulationManager : MonoBehaviour
    {
        [System.Serializable]
        public class PopulationEntry
        {
            public CivilianDefinition definition;
            [Min(0)] public int targetCount = 10;
            [Tooltip("Seconds before dead civilian replaced. -1 = never respawn")]
            public float respawnDelay = 15f;
            [Tooltip("Optional patrol path assigned to spawned civilians")]
            public WaypointPath assignPath;
            [HideInInspector] public int alive;
        }

        [Header("Population")]
        public List<PopulationEntry> populations = new();
        [Tooltip("Spawn area centers. Empty = use manager position")]
        public Transform[] spawnPoints;
        public float spawnRadius = 15f;
        [Tooltip("Spawns per second at startup (spread load)")]
        public float spawnRate = 5f;

        [Header("Player / Culling")]
        [Tooltip("Your player transform. Leave empty to auto-find by Player Tag below.")]
        public Transform player;
        [Tooltip("Tag used to auto-find the player when the field above is empty")]
        public string playerTag = "Player";
        [Tooltip("Layer(s) your player lives on — exposed for your own raycasts / checks via CivilianPopulationManager.PlayerLayer")]
        public LayerMask playerLayer = ~0;
        [Tooltip("Beyond this: AI + animator OFF")]
        public float aiCullDistance = 45f;
        [Tooltip("Beyond this: whole GameObject OFF")]
        public float fullCullDistance = 90f;
        [Tooltip("Civilians checked per frame (spread cost)")]
        public int cullChecksPerFrame = 20;

        public static Transform Player { get; private set; }
        public static LayerMask PlayerLayer { get; private set; }

        /// <summary>Swap the player at runtime (DemoPlayer -> real controller, respawn,
        /// vehicle enter/exit...). Everything — culling, police, perception — follows.</summary>
        public void SetPlayer(Transform t)
        {
            player = t;
            Player = t;
        }

        CivilianPool pool;
        readonly List<Civilian> active = new();
        Transform activeRoot; // hierarchy container for all live NPC clones
        int cullCursor;

        public int TotalAlive => active.Count;

        void Awake()
        {
            pool = GetComponent<CivilianPool>();
            Player = player;
            activeRoot = new GameObject("ActiveCivilians").transform;
            activeRoot.SetParent(transform, false);
            activeRoot.localPosition = Vector3.zero;
            activeRoot.localScale = Vector3.one;
        }

        void Start()
        {
            // culling + police need the player — auto-find by tag if not assigned
            if (player == null && !string.IsNullOrEmpty(playerTag))
            {
                var tagged = GameObject.FindGameObjectWithTag(playerTag);
                if (tagged != null) player = tagged.transform;
                else if (Camera.main != null) player = Camera.main.transform;
            }
            if (player == null)
                Debug.LogWarning($"[PopulationManager] No player assigned and none found by '{playerTag}' tag " +
                    "or MainCamera — distance culling (SetActive on/off by range) is DISABLED. " +
                    "Assign the Player field in the inspector or call SetPlayer().", this);
            Player = player;
            PlayerLayer = playerLayer;

            foreach (var p in populations)
                if (p.definition != null && p.definition.prefab != null)
                    pool.Prewarm(p.definition.prefab, p.targetCount);

            StartCoroutine(InitialSpawn());
        }

        IEnumerator InitialSpawn()
        {
            float interval = spawnRate > 0 ? 1f / spawnRate : 0f;
            foreach (var p in populations)
            {
                for (int i = 0; i < p.targetCount; i++)
                {
                    SpawnOne(p);
                    if (interval > 0) yield return new WaitForSeconds(interval);
                }
            }
        }

        // ---------- Public population control API ----------

        /// <summary>Change target for a definition at runtime. Spawns/despawns to match.</summary>
        public void SetTargetCount(CivilianDefinition def, int newTarget)
        {
            var entry = populations.Find(p => p.definition == def);
            if (entry == null) return;
            entry.targetCount = newTarget;

            while (entry.alive < newTarget) SpawnOne(entry);
            if (entry.alive > newTarget)
            {
                int toRemove = entry.alive - newTarget;
                for (int i = active.Count - 1; i >= 0 && toRemove > 0; i--)
                {
                    if (active[i].definition == def)
                    {
                        Despawn(active[i]);
                        toRemove--;
                    }
                }
            }
        }

        public Civilian SpawnOne(PopulationEntry entry)
        {
            Vector3 pos = PickSpawnPosition();
            var go = pool.Get(entry.definition.prefab, pos, Quaternion.Euler(0, Random.Range(0, 360f), 0));

            var civ = go.GetComponent<Civilian>();
            if (civ == null)
            {
                Debug.LogError($"[PopulationManager] Prefab '{entry.definition.prefab.name}' has no Civilian component. Rebuild with Setup Tool.", entry.definition.prefab);
                pool.Return(go);
                return null;
            }
            civ.definition = entry.definition;
            civ.population = this;
            if (entry.assignPath != null) civ.path = entry.assignPath;
            go.transform.SetParent(activeRoot, true); // organized hierarchy: Manager/ActiveCivilians/*

            // re-enable colliders (disabled on death)
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = true;

            civ.Spawn(pos);
            active.Add(civ);
            entry.alive++;
            return civ;
        }

        Vector3 PickSpawnPosition()
        {
            Vector3 center = (spawnPoints != null && spawnPoints.Length > 0)
                ? spawnPoints[Random.Range(0, spawnPoints.Length)].position
                : transform.position;

            // several tries at the wanted radius, then widen the mesh search
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 r = Random.insideUnitCircle * spawnRadius;
                Vector3 candidate = center + new Vector3(r.x, 0, r.y);
                float sample = attempt < 3 ? 5f : 15f; // late attempts search further up/down
                if (NavMesh.SamplePosition(candidate, out var hit, sample, NavMesh.AllAreas))
                    return hit.position;
            }

            // last resort: snap the center itself onto the mesh, wherever it is
            if (NavMesh.SamplePosition(center, out var centerHit, 50f, NavMesh.AllAreas))
                return centerHit.position;

            Debug.LogError("[PopulationManager] No NavMesh found anywhere near spawn area at " +
                $"{center}. Bake a NavMesh or move the manager / spawn points onto walkable ground.", this);
            return center;
        }

        public void NotifyDeath(Civilian civ, float corpseLifetime)
        {
            var entry = populations.Find(p => p.definition == civ.definition);
            StartCoroutine(DeathRoutine(civ, entry, corpseLifetime));
        }

        IEnumerator DeathRoutine(Civilian civ, PopulationEntry entry, float corpseLifetime)
        {
            yield return new WaitForSeconds(corpseLifetime);
            Despawn(civ);

            if (entry != null)
            {
                if (entry.respawnDelay >= 0)
                {
                    yield return new WaitForSeconds(entry.respawnDelay);
                    if (entry.alive < entry.targetCount) SpawnOne(entry);
                }
            }
        }

        void Despawn(Civilian civ)
        {
            var entry = populations.Find(p => p.definition == civ.definition);
            if (entry != null) entry.alive--;
            active.Remove(civ);
            pool.Return(civ.gameObject);
        }

        void OnDrawGizmosSelected()
        {
            Vector3 c = player != null ? player.position : transform.position;
            Gizmos.color = new Color(1f, 0.7f, 0f, 0.9f);   // amber: AI + animator off
            DrawRing(c, aiCullDistance);
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f); // red: GameObject SetActive(false)
            DrawRing(c, fullCullDistance);
        }

        static void DrawRing(Vector3 center, float r)
        {
            const int seg = 48;
            Vector3 prev = center + new Vector3(r, 0, 0);
            for (int i = 1; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        // ---------- Distance culling ----------

        void Update()
        {
            if (player == null || active.Count == 0) return;
            Player = player;

            Vector3 pPos = player.position;
            int checks = Mathf.Min(cullChecksPerFrame, active.Count);

            for (int i = 0; i < checks; i++)
            {
                cullCursor = (cullCursor + 1) % active.Count;
                var civ = active[cullCursor];
                if (civ == null) continue;

                // corpses: hands off — toggling the animator/object interrupts the die pose
                if (civ.State == CivilianState.Dead)
                {
                    if (!civ.gameObject.activeSelf) civ.gameObject.SetActive(true);
                    continue;
                }

                float sqrDist = (civ.transform.position - pPos).sqrMagnitude;

                bool fullOn = sqrDist < fullCullDistance * fullCullDistance;
                bool aiOn = sqrDist < aiCullDistance * aiCullDistance;

                if (civ.gameObject.activeSelf != fullOn)
                    civ.gameObject.SetActive(fullOn);

                if (fullOn)
                    civ.SetAIEnabled(aiOn);
            }
        }
    }
}
