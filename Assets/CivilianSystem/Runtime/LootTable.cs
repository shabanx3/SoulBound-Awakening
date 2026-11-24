// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEngine;

namespace CivilianSystem
{
    [CreateAssetMenu(menuName = "Civilian System/Loot Table", fileName = "NewLootTable")]
    public class LootTable : ScriptableObject
    {
        [System.Serializable]
        public class LootEntry
        {
            public GameObject itemPrefab;
            [Range(0f, 1f)] public float dropChance = 0.5f;
            public int minCount = 1;
            public int maxCount = 1;
        }

        public LootEntry[] entries;

        [Tooltip("Random scatter radius around corpse")]
        public float scatterRadius = 0.75f;
        public float dropUpwardForce = 2f;

        /// <summary>Spawn drops at position. Each entry rolled independently.</summary>
        public void SpawnDrops(Vector3 position)
        {
            if (entries == null) return;

            foreach (var e in entries)
            {
                if (e.itemPrefab == null) continue;
                if (Random.value > e.dropChance) continue;

                int count = Random.Range(e.minCount, e.maxCount + 1);
                for (int i = 0; i < count; i++)
                {
                    Vector2 scatter = Random.insideUnitCircle * scatterRadius;
                    Vector3 pos = position + new Vector3(scatter.x, 0.25f, scatter.y);
                    GameObject item = Object.Instantiate(e.itemPrefab, pos, Random.rotation);

                    if (item.TryGetComponent<Rigidbody>(out var rb))
                        rb.AddForce(Vector3.up * dropUpwardForce + new Vector3(scatter.x, 0, scatter.y), ForceMode.Impulse);
                }
            }
        }
    }
}
