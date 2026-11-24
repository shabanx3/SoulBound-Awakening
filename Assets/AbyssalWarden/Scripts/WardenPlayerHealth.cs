using UnityEngine;
namespace AbyssalWarden
{
    // Optional test component. Existing player systems should implement IWardenDamageable instead.
    public class WardenPlayerHealth : MonoBehaviour,IWardenDamageable
    {
        public float maxHealth=200f;
        public float currentHealth=200f;
        public bool invincible;
        public void ReceiveDamage(float amount,Transform attacker)
        {
            if(invincible || currentHealth<=0) return;
            currentHealth=Mathf.Max(0,currentHealth-amount);
            Debug.Log($"Player hit for {amount:0} HP. Remaining: {currentHealth:0}",this);
        }
    }
}
