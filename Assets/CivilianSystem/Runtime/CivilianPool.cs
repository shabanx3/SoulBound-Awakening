// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using System.Collections.Generic;
using UnityEngine;

namespace CivilianSystem
{
    /// <summary>
    /// Simple per-prefab pool. Population manager uses it; also usable for loot etc.
    /// </summary>
    public class CivilianPool : MonoBehaviour
    {
        readonly Dictionary<GameObject, Stack<GameObject>> pools = new();
        readonly Dictionary<GameObject, GameObject> instanceToPrefab = new();
        Transform inactiveRoot;

        void Awake()
        {
            inactiveRoot = new GameObject("PooledInactive").transform;
            inactiveRoot.SetParent(transform);
            inactiveRoot.gameObject.SetActive(false);
        }

        public void Prewarm(GameObject prefab, int count)
        {
            if (!pools.TryGetValue(prefab, out var stack))
                pools[prefab] = stack = new Stack<GameObject>();

            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(prefab, inactiveRoot);
                instanceToPrefab[go] = prefab;
                stack.Push(go);
            }
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!pools.TryGetValue(prefab, out var stack))
                pools[prefab] = stack = new Stack<GameObject>();

            GameObject go;
            if (stack.Count > 0)
            {
                go = stack.Pop();
                go.transform.SetParent(null);
                go.transform.SetPositionAndRotation(position, rotation);
                go.SetActive(true);
            }
            else
            {
                go = Instantiate(prefab, position, rotation);
                instanceToPrefab[go] = prefab;
            }
            return go;
        }

        public void Return(GameObject instance)
        {
            if (!instanceToPrefab.TryGetValue(instance, out var prefab))
            {
                Destroy(instance); // not ours
                return;
            }
            instance.transform.SetParent(inactiveRoot);
            pools[prefab].Push(instance);
        }
    }
}
