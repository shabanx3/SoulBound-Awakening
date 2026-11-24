using AbyssalWarden;
using UnityEngine;

// =========================================================
// HEAL DROP
// =========================================================
// Defeated enemies may drop a glowing heal orb. It bobs on the
// ground, drifts to the player when close and heals a fixed
// amount on pickup (CustomEmeraldDamage.Heal). Settings live
// in Resources/PlayerSpellConfig (Heal Drops).
// =========================================================

public class HealDrop : MonoBehaviour
{
    private static Transform player;

    private PlayerSpellConfig config;
    private Vector3 basePosition;
    private float spawnTime;

    public static void TrySpawn(Vector3 position, float chance)
    {
        if (Random.value <= chance) Spawn(position);
    }

    public static void Spawn(Vector3 position)
    {
        PlayerSpellConfig config = PlayerCombatBootstrap.Config;
        var go = new GameObject("Heal Orb");
        Vector2 jitter = Random.insideUnitCircle * .8f;
        go.transform.position = position + new Vector3(jitter.x, 0f, jitter.y);
        var drop = go.AddComponent<HealDrop>();
        drop.config = config;

        WardenVFX.Spawn(config.healOrbEffect, go.transform.position, Quaternion.identity, 0f, go.transform, config.healOrbScale);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(.35f, 1f, .45f);
        light.range = 3f;
        light.intensity = 2f;
        Destroy(go, config.healOrbLifetime);
    }

    private void Start()
    {
        // Settle on the ground below the death position.
        Vector3 p = transform.position;
        if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 10f, ~(1 << 20), QueryTriggerInteraction.Ignore))
            p = hit.point;
        basePosition = p + Vector3.up * .6f;
        transform.position = basePosition;
        spawnTime = Time.time;
    }

    private void Update()
    {
        if (player == null)
        {
            var receiver = FindFirstObjectByType<PlayerWardenDamageReceiver>();
            if (receiver == null) return;
            player = receiver.transform;
        }

        Vector3 chest = player.position + Vector3.up;
        float distance = Vector3.Distance(transform.position, chest);

        // Short delay so the orb is visible where the enemy fell before it flies to the player.
        if (Time.time - spawnTime > .6f && distance <= config.healMagnetRange)
        {
            basePosition = Vector3.MoveTowards(transform.position, chest, Mathf.Lerp(10f, 4f, distance / config.healMagnetRange) * Time.deltaTime);
            transform.position = basePosition;
        }
        else
        {
            transform.position = basePosition + Vector3.up * Mathf.Sin((Time.time - spawnTime) * 3f) * .15f;
        }

        if (distance <= 1.1f) Pickup();
    }

    private void Pickup()
    {
        var health = player.GetComponent<CustomEmeraldDamage>();
        if (health != null) health.Heal(config.healAmount);

        WardenVFX.Spawn(config.healPickupEffect, player.position, player.rotation, 2.5f, player);
        if (config.healPickupSound != null) AudioSource.PlayClipAtPoint(config.healPickupSound, player.position);
        Destroy(gameObject);
    }
}
