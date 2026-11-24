using UnityEngine;
using AbyssalWarden;

// =========================================================
// WARDEN LEADERSHIP LINK
// =========================================================
// Connects The Abyssal Warden to the LeadershipBossProxy.
// Put on an always-active object (MLP_Boss_Placeholder),
// because the Warden itself starts inactive.
// - Activates the Warden once the boss area is entered.
// - Warden engages the player  -> proxy.BeginEncounter()
// - Warden dies                -> proxy.DefeatBoss()
// =========================================================

public class WardenLeadershipLink : MonoBehaviour
{
    [SerializeField] private AbyssalWardenBoss warden;
    [SerializeField] private LeadershipBossProxy proxy;

    [Tooltip("Keep the Warden inactive until the player teleports into the boss area.")]
    [SerializeField] private bool activateOnBossAreaEntry = true;

    private bool defeatReported;

    private void Awake()
    {
        if (proxy == null)
            proxy = GetComponent<LeadershipBossProxy>();
    }

    private void Update()
    {
        if (warden == null || proxy == null)
            return;

        if (activateOnBossAreaEntry && !warden.gameObject.activeSelf && BossAreaEntered())
            warden.gameObject.SetActive(true);

        if (!warden.gameObject.activeInHierarchy || defeatReported)
            return;

        if (warden.IsFighting) // after the intro cinematic, so it does not count as fight time
            proxy.BeginEncounter();

        if (warden.IsDead)
        {
            defeatReported = true;

            // Killed in a single hit never shows IsEngaged, so make sure the encounter is started.
            proxy.BeginEncounter();
            proxy.DefeatBoss();
        }
    }

    private static bool BossAreaEntered()
    {
        LeadershipSessionTracker tracker = LeadershipSessionTracker.Instance;
        return tracker != null && tracker.IsTracking && tracker.Current.bossAreaEntered;
    }
}
