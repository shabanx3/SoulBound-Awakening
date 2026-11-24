// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEngine;

namespace CivilianSystem.Demo
{
    /// <summary>Demo loot: spins, auto-despawns, player touch picks it up.</summary>
    public class DemoLootItem : MonoBehaviour
    {
        public string itemName = "Coin";
        public float spinSpeed = 120f;
        public float lifetime = 30f;

        void Start() => Destroy(gameObject, lifetime);

        void Update() => transform.Rotate(0, spinSpeed * Time.deltaTime, 0, Space.World);

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<DemoPlayer>() != null)
            {
                Debug.Log($"[Loot] Picked up: {itemName}");
                Destroy(gameObject);
            }
        }
    }
}
