using System.Collections.Generic;
using AbyssalWarden;
using EmeraldAI;
using MalbersAnimations.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// PLAYER TARGETING (lock-on + aim assist)
// =========================================================
// Malbers' LockOnTarget (with its own lock-on camera) only
// locks onto objects in its "Lock on Targets" set, and Emerald
// enemies / the Warden never join that set. This keeps the set
// filled with hostile, living targets near the player.
// Each target is registered through a "Lock Point" child at
// chest height, so the lock-on camera looks at the body and
// not at the feet. Middle mouse toggles the lock.
// =========================================================

public class PlayerTargeting : MonoBehaviour
{
    public PlayerSpellConfig config;

    private LockOnTarget malbersLock;
    private readonly HashSet<GameObject> registered = new();
    private readonly List<Component> candidates = new();
    private readonly Dictionary<Component, Transform> lockPoints = new();
    private float nextRefresh, nextCandidateScan;
    private bool lastLockState;

    // Fallback lock when the player has no Malbers LockOnTarget.
    private Transform ownLocked;

    private Transform markerTarget;
    private GameObject marker;

    /// <summary> The locked target's root (boss / Emerald AI), or null. </summary>
    public Transform LockedTarget
    {
        get
        {
            Transform t = RootOf(malbersLock != null ? malbersLock.LockedTarget : ownLocked);
            return t != null && IsValid(t) ? t : null;
        }
    }

    private void Awake()
    {
        malbersLock = GetComponentInChildren<LockOnTarget>(true);
    }

    private void OnDisable()
    {
        if (malbersLock != null && malbersLock.Targets != null)
            foreach (GameObject go in registered)
                if (go != null) malbersLock.Targets.Item_Remove(go);
        registered.Clear();
        if (marker != null) Destroy(marker);
    }

    private void Update()
    {
        if (Time.unscaledTime >= nextCandidateScan)
        {
            nextCandidateScan = Time.unscaledTime + 1f;
            ScanCandidates();
        }

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + .25f;
            SyncLockSet();
        }

        if (malbersLock == null && Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame && Time.timeScale > 0f)
        {
            Transform best = FindTarget(config.lockRange, 180f);
            ownLocked = ownLocked != null || best == null ? null : LockPoint(best);
        }

        if (ownLocked != null && (LockedTarget == null || Flat(ownLocked.position - transform.position) > config.lockBreakRange))
            ownLocked = null;
    }

    private void LateUpdate()
    {
        if (malbersLock != null)
        {
            // Malbers' LockOn input is on V; middle mouse is handled here (only if it did not already toggle).
            bool pressed = Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame && Time.timeScale > 0f;
            if (pressed && malbersLock.LockingOn == lastLockState)
            {
                SyncLockSet();
                malbersLock.LockTargetToggle();
            }

            // Locking with nothing in range leaves Malbers "locking" with no target: switch it back off.
            if (malbersLock.LockingOn && malbersLock.LockedTarget == null && registered.Count == 0)
                malbersLock.LockTarget(false);

            lastLockState = malbersLock.LockingOn;
        }

        UpdateMarker();
    }

    // ---------------------------------------------------------------- targets
    private void ScanCandidates()
    {
        candidates.Clear();
        foreach (EmeraldSystem ai in FindObjectsByType<EmeraldSystem>(FindObjectsSortMode.None)) candidates.Add(ai);
        foreach (AbyssalWardenBoss boss in FindObjectsByType<AbyssalWardenBoss>(FindObjectsSortMode.None)) candidates.Add(boss);

        var stale = new List<Component>();
        foreach (var pair in lockPoints) if (pair.Key == null || pair.Value == null) stale.Add(pair.Key);
        foreach (Component c in stale) lockPoints.Remove(c);
    }

    private void SyncLockSet()
    {
        if (malbersLock == null || malbersLock.Targets == null) return;

        Transform locked = LockedTarget;
        var wanted = new HashSet<GameObject>();
        foreach (Component c in candidates)
        {
            if (c == null || !IsValid(c.transform)) continue;
            float d = Flat(c.transform.position - transform.position);
            if (d <= config.lockRange || (c.transform == locked && d <= config.lockBreakRange))
                wanted.Add(LockPoint(c.transform).gameObject);
        }

        foreach (GameObject go in new List<GameObject>(registered))
        {
            if (go != null && wanted.Contains(go)) continue;
            registered.Remove(go);
            if (go != null) malbersLock.Targets.Item_Remove(go);
        }
        foreach (GameObject go in wanted)
        {
            if (registered.Add(go) && !malbersLock.Targets.Has_Item(go))
                malbersLock.Targets.Item_Add(go);
        }
    }

    /// <summary> Child of the target at chest height; what the lock-on camera and Aim look at. </summary>
    private Transform LockPoint(Transform root)
    {
        Component key = root.GetComponent<AbyssalWardenBoss>();
        if (key == null) key = root.GetComponent<EmeraldSystem>();
        if (key == null) return root;
        if (lockPoints.TryGetValue(key, out Transform point) && point != null) return point;

        Collider col = SolidCollider(root);
        float height = col != null ? col.bounds.size.y : 1.8f;
        float bottom = col != null ? col.bounds.min.y : root.position.y;
        point = new GameObject("Lock Point").transform;
        point.SetParent(root, false);
        point.position = new Vector3(root.position.x, bottom + height * config.lockPointHeight, root.position.z);
        lockPoints[key] = point;
        return point;
    }

    /// <summary> The boss / Emerald AI that owns this transform (lock points resolve to their owner). </summary>
    public static Transform RootOf(Transform t)
    {
        if (t == null) return null;
        AbyssalWardenBoss boss = t.GetComponentInParent<AbyssalWardenBoss>();
        if (boss != null) return boss.transform;
        EmeraldSystem ai = t.GetComponentInParent<EmeraldSystem>();
        return ai != null ? ai.transform : t;
    }

    /// <summary> Hostile, alive and active: an Emerald AI that treats the player as an enemy, or the Warden. </summary>
    public bool IsValid(Transform t)
    {
        if (t == null || !t.gameObject.activeInHierarchy) return false;

        AbyssalWardenBoss boss = t.GetComponentInParent<AbyssalWardenBoss>();
        if (boss != null) return !boss.IsDead;

        EmeraldSystem ai = t.GetComponentInParent<EmeraldSystem>();
        if (ai == null) return false;
        if (ai.AnimationComponent != null && ai.AnimationComponent.IsDead) return false;
        if (ai.HealthComponent != null && ai.HealthComponent.CurrentHealth <= 0) return false;
        return ai.DetectionComponent != null && ai.DetectionComponent.GetTargetFactionRelation(transform) == "Enemy";
    }

    /// <summary> Locked target, else the hostile closest to the screen centre within the aim-assist cone. </summary>
    public Transform AimAssistTarget()
    {
        Transform locked = LockedTarget;
        return locked != null ? locked : FindTarget(config.assistRange, config.assistAngle);
    }

    /// <summary> Best hostile within range and within maxAngle of the camera's view (centre of screen wins). </summary>
    public Transform FindTarget(float range, float maxAngle)
    {
        Camera cam = Camera.main;
        Vector3 origin = cam != null ? cam.transform.position : transform.position + Vector3.up * 1.6f;
        Vector3 forward = cam != null ? cam.transform.forward : transform.forward;

        Transform best = null;
        float bestScore = float.MaxValue;
        foreach (Component c in candidates)
        {
            if (c == null || !IsValid(c.transform)) continue;
            Vector3 point = AimPoint(c.transform);
            float d = Vector3.Distance(transform.position, point);
            if (d > range) continue;
            float angle = Vector3.Angle(forward, point - origin);
            if (angle > maxAngle) continue;
            float score = angle * 2f + d * .5f;
            if (score < bestScore) { bestScore = score; best = c.transform; }
        }
        return best;
    }

    /// <summary> Where to aim on a target: the middle of its body collider, or chest height. </summary>
    public static Vector3 AimPoint(Transform t)
    {
        Transform root = RootOf(t);
        if (root == null) return t != null ? t.position : Vector3.zero;
        Collider col = SolidCollider(root);
        return col != null ? col.bounds.center : root.position + Vector3.up * 1.2f;
    }

    private static Collider SolidCollider(Transform root)
    {
        foreach (Collider col in root.GetComponents<Collider>()) if (col.enabled && !col.isTrigger) return col;
        foreach (Collider col in root.GetComponentsInChildren<Collider>()) if (col.enabled && !col.isTrigger) return col;
        return null;
    }

    /// <summary> Point under the screen centre (crosshair), ignoring the player. </summary>
    public Vector3 CrosshairPoint(float maxDistance)
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position + transform.forward * maxDistance;
        Ray ray = cam.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance + 20f, ~(1 << 20), QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 point = ray.GetPoint(maxDistance);
        foreach (RaycastHit h in hits)
        {
            if (h.transform.IsChildOf(transform)) continue;
            if (h.distance < best) { best = h.distance; point = h.point; }
        }
        return point;
    }

    private static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

    // ---------------------------------------------------------------- marker
    private void UpdateMarker()
    {
        Transform locked = LockedTarget;
        if (locked == markerTarget) return;

        if (marker != null) Destroy(marker);
        marker = null;
        markerTarget = locked;
        if (locked == null || config.lockMarkerEffect == null) return;

        marker = Instantiate(config.lockMarkerEffect, locked.position + Vector3.up * .05f, Quaternion.identity, locked);
        marker.transform.localScale *= config.lockMarkerScale;
    }
}
