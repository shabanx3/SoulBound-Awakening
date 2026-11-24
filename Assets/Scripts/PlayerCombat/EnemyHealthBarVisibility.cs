using System.Collections.Generic;
using EmeraldAI;
using UnityEngine;

// =========================================================
// ENEMY HEALTH BAR VISIBILITY
// =========================================================
// Emerald shows an enemy's head bar whenever the player is in
// its detection radius: all around, even behind walls. This
// fades each bar in only when the enemy is on screen in front
// of the camera, within range and in line of sight, or is the
// locked target, or was hit a moment ago.
// (Uses a CanvasGroup, so Emerald's own toggling is untouched.)
// =========================================================

public class EnemyHealthBarVisibility : MonoBehaviour
{
    public PlayerSpellConfig config;

    private class Bar
    {
        public EmeraldSystem ai;
        public CanvasGroup group;
        public int lastHealth;
        public float lastHitTime = -100f;
        public bool visible;
    }

    private readonly Dictionary<EmeraldUI, Bar> bars = new();
    private PlayerTargeting targeting;
    private float nextScan, nextCheck;

    private void Awake() => targeting = GetComponent<PlayerTargeting>();

    private void LateUpdate()
    {
        if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 1f; Scan(); }

        bool check = Time.unscaledTime >= nextCheck;
        if (check) nextCheck = Time.unscaledTime + .15f;

        Camera cam = Camera.main;
        Transform locked = targeting != null ? targeting.LockedTarget : null;

        foreach (var pair in bars)
        {
            Bar b = pair.Value;
            if (pair.Key == null || b.group == null || b.ai == null) continue;

            int health = b.ai.HealthComponent != null ? b.ai.HealthComponent.CurrentHealth : 0;
            if (health < b.lastHealth) b.lastHitTime = Time.time;
            b.lastHealth = health;

            if (check) b.visible = ShouldShow(b, cam, locked);
            b.group.alpha = Mathf.MoveTowards(b.group.alpha, b.visible ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        }
    }

    private void Scan()
    {
        foreach (EmeraldUI ui in FindObjectsByType<EmeraldUI>(FindObjectsSortMode.None))
        {
            if (ui.HealthBarCanvas == null || bars.ContainsKey(ui)) continue;
            var group = ui.HealthBarCanvas.GetComponent<CanvasGroup>();
            if (group == null) group = ui.HealthBarCanvas.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;
            var ai = ui.GetComponent<EmeraldSystem>();
            bars[ui] = new Bar { ai = ai, group = group, lastHealth = ai != null && ai.HealthComponent != null ? ai.HealthComponent.CurrentHealth : 0 };
        }

        var dead = new List<EmeraldUI>();
        foreach (var pair in bars) if (pair.Key == null) dead.Add(pair.Key);
        foreach (var key in dead) bars.Remove(key);
    }

    private bool ShouldShow(Bar b, Camera cam, Transform locked)
    {
        if (cam == null || !b.ai.gameObject.activeInHierarchy) return false;
        if (b.ai.AnimationComponent != null && b.ai.AnimationComponent.IsDead) return false;

        Vector3 chest = PlayerTargeting.AimPoint(b.ai.transform);
        float distance = Vector3.Distance(transform.position, b.ai.transform.position);
        if (distance > config.enemyBarRange) return false;

        bool special = b.ai.transform == locked || Time.time - b.lastHitTime <= config.enemyBarAfterHitSeconds;
        if (special) return true;

        // On screen and in front of the camera.
        Vector3 view = cam.WorldToViewportPoint(chest);
        if (view.z <= 0f || view.x < -.05f || view.x > 1.05f || view.y < -.05f || view.y > 1.1f) return false;
        if (Vector3.Angle(cam.transform.forward, chest - cam.transform.position) > config.enemyBarViewAngle) return false;

        return InLineOfSight(cam.transform.position, chest, b.ai.transform);
    }

    // Nothing solid (other than the player or the enemy itself) between the camera and the enemy.
    private bool InLineOfSight(Vector3 from, Vector3 to, Transform enemy)
    {
        Vector3 dir = to - from;
        RaycastHit[] hits = Physics.RaycastAll(from, dir.normalized, dir.magnitude, ~(1 << 20), QueryTriggerInteraction.Ignore);
        foreach (RaycastHit h in hits)
        {
            if (h.transform.IsChildOf(enemy) || h.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<EmeraldSystem>() != null) continue; // other enemies do not hide a bar
            return false;
        }
        return true;
    }
}
