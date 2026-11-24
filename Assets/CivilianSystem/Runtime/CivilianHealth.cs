// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using System;
using UnityEngine;

namespace CivilianSystem
{
    public interface IDamageable
    {
        void TakeDamage(float amount, GameObject source);
    }

    /// <summary>Health + damage + death. Fires events consumed by Civilian brain.</summary>
    public class CivilianHealth : MonoBehaviour, IDamageable
    {
        public float MaxHealth { get; private set; }
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;

        /// <param>damage amount, attacker (may be null)</param>
        public event Action<float, GameObject> OnDamaged;
        public event Action OnDeath;

        public void Init(float maxHealth)
        {
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnDamaged?.Invoke(amount, source);

            if (CurrentHealth <= 0f)
                OnDeath?.Invoke();
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        }
    }
}
