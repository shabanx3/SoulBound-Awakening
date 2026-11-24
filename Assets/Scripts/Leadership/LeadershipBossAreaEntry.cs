using UnityEngine;

// =========================================================
// LEADERSHIP BOSS AREA ENTRY
// =========================================================
// Put on the teleporter trigger that takes the player into
// the boss area. It only listens to the existing trigger
// collider; the Malbers teleport itself is left untouched.
// On first entry it:
//   - starts the boss area time
//   - disables the return teleporter, so the player can't leave
//   - brings nearby allies along (others are left behind)
//   - shows the loading panel for a moment
// =========================================================

[RequireComponent(typeof(Collider))]
public class LeadershipBossAreaEntry : MonoBehaviour
{
    public static bool PlayerInBossArea { get; private set; }

    [Tooltip("Where the teleport lands the player (Exit Point (1)). Allies are placed around it.")]
    [SerializeField] private Transform arrivalPoint;

    [Tooltip("Objects to disable once the player is in the boss area, e.g. the return Teleporter.")]
    [SerializeField] private GameObject[] disableOnEntry;

    [SerializeField] private float loadingDuration = 2.5f;

    private void Awake()
    {
        // Static state survives scene reloads, so reset it with the scene.
        PlayerInBossArea = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerInBossArea || !LeadershipSessionTracker.IsPlayerCollider(other))
            return;

        PlayerInBossArea = true;

        foreach (GameObject go in disableOnEntry)
            if (go != null) go.SetActive(false);

        LeadershipSessionTracker.ReportBossAreaEntered();
        AllyCompanionManager.HandleBossTeleport(transform.position, arrivalPoint);

        if (MenuManager.Instance != null)
            MenuManager.Instance.ShowTransitionLoading(loadingDuration);
    }
}
