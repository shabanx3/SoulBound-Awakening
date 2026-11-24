using System;
using System.Collections.Generic;
using EmeraldAI;
using UnityEngine;

// =========================================================
// ALLY COMPANION MANAGER
// =========================================================
// Recruited allies only exist inside the boss area, where they are
// placed in the scene. Their positions are never changed by code.
// - A recruiter's task completed -> its allies are marked as joined but
//                                   stay inactive (nothing in the village)
// - Boss teleport                -> every recruited ally is activated
//                                   where it stands in the boss area and
//                                   follows the player; any that died
//                                   count as "left behind"
// Left-behind allies lower the leadership assessment.
// =========================================================

public class AllyCompanionManager : MonoBehaviour
{
    [Serializable]
    public class RecruiterAllies
    {
        public Recruite_Trigger recruiter;
        public EmeraldSystem[] allies;
    }

    public static AllyCompanionManager Instance { get; private set; }

    [Tooltip("Parent of the ally characters (All_Allies). Activated at runtime; allies stay hidden until recruited.")]
    [SerializeField] private GameObject alliesRoot;

    [SerializeField] private List<RecruiterAllies> recruiterAllies = new();

    private readonly List<EmeraldSystem> joined = new();

    public IReadOnlyList<EmeraldSystem> JoinedAllies => joined;

    private void Awake()
    {
        Instance = this;

        // Hide every mapped ally before the root is shown, so none appear before they are recruited.
        // Some allies have the player saved as TargetToFollow in the scene; clear it so they never follow before the boss area.
        foreach (RecruiterAllies entry in recruiterAllies)
            foreach (EmeraldSystem ally in entry.allies)
                if (ally != null)
                {
                    ally.gameObject.SetActive(false);
                    StopFollowing(ally);
                }

        if (alliesRoot != null)
            alliesRoot.SetActive(true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================================================
    // STATIC API (safe when no manager exists)
    // =========================================================

    /// <summary>
    /// Called when gameplay starts (after New Game resets the recruiters):
    /// allies of completed recruiters join, allies of reset recruiters leave.
    /// </summary>
    public static void SyncWithRecruiters()
    {
        if (Instance == null) return;

        foreach (RecruiterAllies entry in Instance.recruiterAllies)
        {
            if (entry.recruiter == null || entry.recruiter.recruiter == null) continue;

            if (entry.recruiter.recruiter.CheckTaskStatus())
            {
                Instance.Join(entry);
            }
            else
            {
                foreach (EmeraldSystem ally in entry.allies)
                {
                    if (ally == null || !Instance.joined.Remove(ally)) continue;
                    ally.gameObject.SetActive(false);
                }
            }
        }
    }

    public static void OnRecruiterCompleted(string recruiterName)
    {
        if (Instance == null) return;

        foreach (RecruiterAllies entry in Instance.recruiterAllies)
        {
            if (entry.recruiter != null && entry.recruiter.recruiter != null && entry.recruiter.recruiter.recruiterName == recruiterName)
                Instance.Join(entry);
        }
    }

    /// <summary> How many of a recruiter's allies are in the given left-behind list. </summary>
    public static int CountLeftBehind(string recruiterName, List<string> leftBehindNames)
    {
        if (Instance == null || leftBehindNames == null || leftBehindNames.Count == 0) return 0;

        int count = 0;

        foreach (RecruiterAllies entry in Instance.recruiterAllies)
        {
            if (entry.recruiter == null || entry.recruiter.recruiter == null || entry.recruiter.recruiter.recruiterName != recruiterName)
                continue;

            foreach (EmeraldSystem ally in entry.allies)
                if (ally != null && leftBehindNames.Contains(ally.name))
                    count++;
        }

        return count;
    }

    /// <summary> Activates every recruited ally where it stands in the boss area, following the player; reports dead ones as left behind. </summary>
    public static void HandleBossTeleport(Vector3 teleporterPosition, Transform arrivalPoint)
    {
        if (Instance == null) return;

        int withPlayer = 0;
        List<string> leftBehind = new();
        Transform player = Instance.Player;

        foreach (EmeraldSystem ally in Instance.joined)
        {
            if (ally == null) continue;

            if (!IsDead(ally))
            {
                Instance.Activate(ally, player);
                withPlayer++;
            }
            else
            {
                leftBehind.Add(ally.name);
            }
        }

        LeadershipSessionTracker.ReportAlliesAtTeleport(withPlayer, leftBehind);
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private Transform Player => MenuManager.Instance != null && MenuManager.Instance.player != null
        ? MenuManager.Instance.player.transform
        : null;

    private void Join(RecruiterAllies entry)
    {
        foreach (EmeraldSystem ally in entry.allies)
        {
            if (ally == null || joined.Contains(ally)) continue;

            // Stays inactive until the boss teleport.
            StopFollowing(ally);
            ally.gameObject.SetActive(false);
            joined.Add(ally);
        }

        LeadershipSessionTracker.ReportAlliesRecruited(joined.Count);
    }

    private void Activate(EmeraldSystem ally, Transform player)
    {
        if (ally.gameObject.activeSelf)
        {
            if (player != null) EmeraldAPI.Detection.SetTargetToFollow(ally, player, false);
            return;
        }

        // Set before activation so Emerald picks the follow target up when it initialises.
        EmeraldBehaviors behaviors = ally.GetComponent<EmeraldBehaviors>();
        if (behaviors != null) behaviors.TargetToFollow = player;
        ally.TargetToFollow = player;
        EmeraldMovement movement = ally.GetComponent<EmeraldMovement>();
        if (movement != null) movement.WanderType = EmeraldMovement.WanderTypes.Stationary;
        ally.gameObject.SetActive(true);
    }

    private static void StopFollowing(EmeraldSystem ally)
    {
        EmeraldBehaviors behaviors = ally.GetComponent<EmeraldBehaviors>();
        if (behaviors != null) behaviors.TargetToFollow = null;
        ally.TargetToFollow = null;
    }

    private static bool IsDead(EmeraldSystem ally)
    {
        EmeraldHealth health = ally.GetComponent<EmeraldHealth>();
        return health != null && health.CurrentHealth <= 0;
    }
}
