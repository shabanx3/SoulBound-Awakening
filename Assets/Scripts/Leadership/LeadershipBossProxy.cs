using UnityEngine;
using UnityEngine.Events;

// =========================================================
// LEADERSHIP BOSS PROXY
// =========================================================
// Stand-in boss for the leadership assessment.
// - Player enters this trigger      -> boss encounter starts
// - DefeatBoss() is called          -> boss + area times end
//
// Moving to a real boss later:
//   Add this component to the real boss (or keep this object
//   at the arena) and call DefeatBoss() from the boss's death
//   event, e.g. EmeraldEvents > OnDeathEvent.
//   BeginEncounter() can also be called from the boss's own
//   aggro / detection event instead of using the trigger.
// =========================================================

[RequireComponent(typeof(Collider))]
public class LeadershipBossProxy : MonoBehaviour
{
    [Tooltip("Show the campaign complete panel after the boss is defeated.")]
    [SerializeField] private bool endCampaignOnDefeat = true;

    [Tooltip("Real seconds between the boss dying and the game stopping on the complete panel (lets the death animation play).")]
    [SerializeField] private float completeDelay = 3f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Tooltip("Editor / development builds only: press to defeat the stand-in boss during the encounter.")]
    [SerializeField] private KeyCode debugDefeatKey = KeyCode.F9;
#endif

    public UnityEvent onEncounterStarted;
    public UnityEvent onDefeated;

    private bool encounterStarted;
    private bool defeated;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
       // if (encounterStarted && !defeated && Input.GetKeyDown(debugDefeatKey))
        if (Input.GetKeyDown(debugDefeatKey))
            DefeatBoss();
    }
#endif

    // Stay (not Enter) so a player already standing in the trigger
    // when the boss area starts still begins the encounter.
    private void OnTriggerStay(Collider other)
    {
        //if (!encounterStarted && LeadershipSessionTracker.IsPlayerCollider(other))
        if (!encounterStarted && other.CompareTag("Animal"))
            BeginEncounter();
    }

    [ContextMenu("Begin Encounter")]
    public void BeginEncounter()
    {
        if (encounterStarted || defeated)
            return;

        if (!LeadershipSessionTracker.ReportBossEncounterStarted())
            return;

        encounterStarted = true;
        onEncounterStarted?.Invoke();
    }

    [ContextMenu("Defeat Boss")]
    public void DefeatBoss()
    {
        if (defeated)
            return;

        if (!LeadershipSessionTracker.ReportBossDefeated())
            return;

        defeated = true;
        onDefeated?.Invoke();

        if (endCampaignOnDefeat && MenuManager.Instance != null)
            StartCoroutine(CompleteAfterDelay());
    }

    private System.Collections.IEnumerator CompleteAfterDelay()
    {
        // The Warden's ending cinematic plays first; the complete panel follows it.
        if (AbyssalWarden.WardenEndingCinematic.Pending)
        {
            while (AbyssalWarden.WardenEndingCinematic.Pending)
                yield return null;
            yield return new WaitForSecondsRealtime(.3f);
        }
        else
        {
            yield return new WaitForSecondsRealtime(completeDelay);
        }
        MenuManager.Instance.ShowCampaignComplete();
    }
}
