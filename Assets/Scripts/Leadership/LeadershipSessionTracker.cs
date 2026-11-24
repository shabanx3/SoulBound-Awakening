using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// LEADERSHIP SESSION TRACKER
// =========================================================
// Records the four MLP input features for a campaign
// (New Game -> boss defeated), across deaths and scene reloads:
//   1. Recruitment duration  = sum of each ally task's time
//   2. Total game duration   = play time until the boss is defeated
//   3. Boss encounter time   = boss triggered -> boss defeated
//   4. Enemies killed
// Play time only counts while MenuManager is in Gameplay or
// AllyTask, so pause / observer mode / menus are excluded.
//
// Game code reports events through the static Report... methods,
// which do nothing if no tracker is in the scene.
// =========================================================

public class LeadershipSessionTracker : MonoBehaviour
{
    public static LeadershipSessionTracker Instance { get; private set; }

    [SerializeField] private float autosaveInterval = 15f;

    [SerializeField] private ProvisionalLeadershipEvaluator evaluator = new();

    [Tooltip("MLP exported by MLP_Training/train_leadership_mlp.py, relative to StreamingAssets.")]
    [SerializeField] private string mlpModelFile = "Leadership/leadership_mlp.json";

    public LeadershipCampaignData Current { get; private set; }

    public LeadershipMlpModel Model { get; private set; }

    /// <summary> Why no model is loaded (null when one is). </summary>
    public string ModelStatus { get; private set; }

    public List<LeadershipCampaignData> History { get; private set; } = new();

    public bool IsTracking => Current != null && Current.outcome == LeadershipCampaignData.OutcomeInProgress;

    public event Action<LeadershipCampaignData> OnCampaignCompleted;

    private float nextAutosaveTime;
    private bool deathReportedThisLoad;
    private bool loggedEncounterIgnored;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("LeadershipSessionTracker: A second tracker was found and disabled.", this);
            enabled = false;
            return;
        }

        Instance = this;
        Current = LeadershipStorage.LoadCurrent();
        History = LeadershipStorage.LoadHistory();
        LoadModel();
    }

    private void LoadModel()
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, mlpModelFile);
        Model = LeadershipMlpModel.TryLoad(path, out string error);
        ModelStatus = error;

        if (Model != null)
            Debug.Log($"<color=green>[Leadership]</color> MLP loaded ({Model.Data.labelSource}, trained {Model.Data.createdUtc}).");
        else
            Debug.Log($"[Leadership] No MLP model, using the rule-based estimate. {error}");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!IsTracking)
            return;

        MenuManager menu = MenuManager.Instance;
        if (menu == null)
            return;

        if (menu.gameState == GameState.Gameplay || menu.gameState == GameState.AllyTask)
        {
            float dt = Time.deltaTime;
            Current.playTimeSeconds += dt;

            RecruiterInfo active = menu.currentActiveRecruiterTask;
            if (active != null && active.isTaskActive)
            {
                AllyTaskRecord task = GetOrCreateTask(active.recruiterName);
                if (!task.completed)
                    task.activeSeconds += dt;
            }
        }

        if (Time.unscaledTime >= nextAutosaveTime)
            Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Save();
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    private void OnDisable()
    {
        // Covers scene reloads (e.g. after Level Fail).
        Save();
    }

    // =========================================================
    // STATIC REPORT API (safe when no tracker exists)
    // =========================================================

    public static void StartNewCampaign() { if (Instance) Instance.BeginNewCampaign(); }
    public static void NotifyGameplayStarted() { if (Instance) Instance.EnsureCampaign(); }
    public static void ReportAllyTaskAssigned(string recruiterName) { if (Instance) Instance.OnTaskAssigned(recruiterName); }
    public static void ReportAllyTaskCompleted(string recruiterName) { if (Instance) Instance.OnTaskCompleted(recruiterName); }
    public static void ReportEnemyKilled() { if (Instance) Instance.OnEnemyKilled(); }
    public static void ReportPlayerDeath() { if (Instance) Instance.OnPlayerDeath(); }
    public static void ReportBossAreaEntered() { if (Instance) Instance.OnBossAreaEntered(); }
    public static bool ReportBossEncounterStarted() => Instance && Instance.OnBossEncounterStarted();
    public static bool ReportBossDefeated() => Instance && Instance.OnBossDefeated();
    public static void ReportAlliesRecruited(int count) { if (Instance) Instance.OnAlliesRecruited(count); }
    public static void ReportAlliesAtTeleport(int withPlayer, List<string> leftBehind) { if (Instance) Instance.OnAlliesAtTeleport(withPlayer, leftBehind); }

    /// <summary> True when the collider belongs to the player (or to the horse the player is riding). </summary>
    public static bool IsPlayerCollider(Collider other)
    {
        if (other == null || MenuManager.Instance == null || MenuManager.Instance.player == null)
            return false;

        Transform player = MenuManager.Instance.player.transform;

        return other.transform.IsChildOf(player) || player.IsChildOf(other.transform.root);
    }

    // =========================================================
    // CAMPAIGN LIFECYCLE
    // =========================================================

    private void BeginNewCampaign()
    {
        if (Current != null && Current.outcome == LeadershipCampaignData.OutcomeInProgress && Current.playTimeSeconds > 0f)
        {
            Current.outcome = LeadershipCampaignData.OutcomeAbandoned;
            Current.endedUtc = DateTime.UtcNow.ToString("o");
            LeadershipStorage.AppendHistory(Current);
            History.Add(Current);
        }

        Current = CreateCampaign();
        Save();
    }

    private void EnsureCampaign()
    {
        deathReportedThisLoad = false;

        if (Current == null)
        {
            Current = CreateCampaign();
            Save();
            return;
        }

        if (IsTracking)
        {
            SyncCompletedRecruiters();
            Current.totalEnemies = Mathf.Max(Current.totalEnemies, CountEnemiesInArea());
        }
    }

    /// <summary> Enemies the level's spawners will produce. Includes inactive ones: All_Enemy only switches on at the teleporter. </summary>
    private static int CountEnemiesInArea()
    {
        int total = 0;

        foreach (EnemySpawner spawner in FindObjectsByType<EnemySpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            total += spawner.GetPlannedEnemyCount();

        return total;
    }

    private LeadershipCampaignData CreateCampaign()
    {
        LeadershipCampaignData data = new()
        {
            campaignId = Guid.NewGuid().ToString("N"),
            startedUtc = DateTime.UtcNow.ToString("o"),
            totalEnemies = CountEnemiesInArea()
        };

        Current = data;

        // Recruiters already done in PlayerPrefs (old save) have unknown task times.
        if (SyncCompletedRecruiters())
            data.startedMidway = true;

        return data;
    }

    /// <summary> Marks recruiters that PlayerPrefs reports as done but whose completion was never observed. </summary>
    private bool SyncCompletedRecruiters()
    {
        bool foundUnknown = false;

        foreach (RecruiterInfo recruiter in GetRecruiters())
        {
            if (!recruiter.CheckTaskStatus())
                continue;

            AllyTaskRecord task = GetOrCreateTask(recruiter.recruiterName);
            if (task.completed)
                continue;

            task.completed = true;
            task.timeUnknown = true;
            foundUnknown = true;
        }

        return foundUnknown;
    }

    // =========================================================
    // EVENTS
    // =========================================================

    private void OnTaskAssigned(string recruiterName)
    {
        if (!IsTracking) return;

        AllyTaskRecord task = GetOrCreateTask(recruiterName);
        if (task.firstAssignedTime < 0f)
            task.firstAssignedTime = Current.playTimeSeconds;

        Save();
    }

    private void OnTaskCompleted(string recruiterName)
    {
        if (!IsTracking) return;

        AllyTaskRecord task = GetOrCreateTask(recruiterName);
        task.completed = true;
        task.completedTime = Current.playTimeSeconds;

        Save();
    }

    private void OnEnemyKilled()
    {
        if (!IsTracking) return;

        Current.enemiesKilled++;
    }

    private void OnPlayerDeath()
    {
        if (!IsTracking || deathReportedThisLoad) return;

        deathReportedThisLoad = true;
        Current.playerDeaths++;

        if (Current.bossAreaEntered && !Current.bossDefeated)
            Current.bossAreaDeaths++;

        Save();
    }

    /// <summary> Allows the next death to be counted (e.g. after a respawn without a scene reload). </summary>
    public static void ResetDeathGuard() { if (Instance) Instance.deathReportedThisLoad = false; }

    private void OnAlliesRecruited(int count)
    {
        if (!IsTracking) return;

        Current.alliesRecruited = Mathf.Max(Current.alliesRecruited, count);
        Save();
    }

    private void OnAlliesAtTeleport(int withPlayer, List<string> leftBehind)
    {
        if (!IsTracking || Current.alliesCheckedAtTeleport) return;

        Current.alliesCheckedAtTeleport = true;
        Current.alliesWithPlayer = withPlayer;
        Current.alliesLeftBehind = leftBehind.Count;
        Current.leftBehindAllyNames = new List<string>(leftBehind);
        Save();

        Debug.Log($"<color=yellow>[Leadership]</color> Allies at teleport: {withPlayer} with player, {leftBehind.Count} left behind.");
    }

    private void OnBossAreaEntered()
    {
        if (!IsTracking || Current.bossAreaEntered) return;

        Current.bossAreaEntered = true;
        Current.bossAreaEnterTime = Current.playTimeSeconds;
        Save();

        Debug.Log($"<color=yellow>[Leadership]</color> Boss area entered at {Current.bossAreaEnterTime:0.0}s");
    }

    private bool OnBossEncounterStarted()
    {
        if (!IsTracking) return false;
        if (Current.bossEncounterStarted) return true;

        if (!Current.bossAreaEntered)
        {
            if (!loggedEncounterIgnored)
                Debug.Log("[Leadership] Boss trigger ignored: the boss area has not been entered through the teleporter yet.");

            loggedEncounterIgnored = true;
            return false;
        }

        Current.bossEncounterStarted = true;
        Current.bossEncounterStartTime = Current.playTimeSeconds;
        Save();

        Debug.Log($"<color=yellow>[Leadership]</color> Boss encounter started at {Current.bossEncounterStartTime:0.0}s");
        return true;
    }

    private bool OnBossDefeated()
    {
        if (!IsTracking) return false;

        if (!Current.bossEncounterStarted)
        {
            Debug.LogWarning("[Leadership] Boss defeat ignored: the boss encounter was never started.");
            return false;
        }

        float now = Current.playTimeSeconds;

        Current.bossDefeated = true;
        Current.bossDefeatTime = now;

        Current.recruitmentDurationSeconds = Current.KnownRecruitmentSeconds();
        Current.totalGameDurationSeconds = now;
        Current.bossEncounterDurationSeconds = now - Current.bossEncounterStartTime;
        Current.bossAreaDurationSeconds = now - Current.bossAreaEnterTime;
        Current.enemiesUndefeated = Current.UndefeatedNow;

        LeadershipResult provisional = evaluator.Evaluate(Current);
        Current.provisionalTier = provisional.tier.ToString();
        Current.provisionalScore = provisional.score;

        LeadershipResult result = Model != null ? EvaluateMlp(Current) : provisional;
        Current.leadershipTier = result.tier.ToString();
        Current.leadershipScore = result.score;
        Current.leadershipConfidence = result.confidence;
        Current.leadershipSource = result.source;

        Current.outcome = LeadershipCampaignData.OutcomeCompleted;
        Current.endedUtc = DateTime.UtcNow.ToString("o");

        LeadershipStorage.AppendHistory(Current);
        History.Add(Current);
        Save();

        Debug.Log(
            $"<color=green>[Leadership]</color> Campaign complete | " +
            $"Recruitment: {Current.recruitmentDurationSeconds:0.0}s | " +
            $"Total: {Current.totalGameDurationSeconds:0.0}s | " +
            $"Boss: {Current.bossEncounterDurationSeconds:0.0}s | " +
            $"Kills: {Current.enemiesKilled} | Deaths: {Current.playerDeaths}");

        OnCampaignCompleted?.Invoke(Current);
        return true;
    }

    // =========================================================
    // QUERIES
    // =========================================================

    public AllyReaction GetAllyReaction(RecruiterInfo recruiter)
    {
        if (Current == null)
            return new AllyReaction { mood = AllyMood.Waiting, reason = "Needs your help" };

        int leftBehind = AllyCompanionManager.CountLeftBehind(recruiter.recruiterName, Current.leftBehindAllyNames);
        return evaluator.EvaluateAlly(Current, Current.FindTask(recruiter.recruiterName), leftBehind);
    }

    public LeadershipResult Evaluate()
    {
        if (Current != null && Current.IsCompleted)
        {
            // All four features exist once the boss is defeated, so the MLP can assess the run.
            if (Model != null)
                return EvaluateMlp(Current);

            // No model: keep the result the run was given at completion.
            if (Enum.TryParse(Current.leadershipTier, out LeadershipTier tier))
            {
                return new LeadershipResult
                {
                    tier = tier,
                    score = Current.leadershipScore,
                    confidence = Current.leadershipConfidence,
                    source = Current.leadershipSource,
                    details = ProvisionalLeadershipEvaluator.TierDescription(tier)
                };
            }
        }

        // During play: boss and total time don't exist yet, so the MLP can't run and the score just builds up.
        return IsTracking ? evaluator.EvaluateLive(Current, GetRecruiters().Count) : evaluator.Evaluate(Current);
    }

    /// <summary> MLP assessment of a run's four features. Requires a loaded model. </summary>
    public LeadershipResult EvaluateMlp(LeadershipCampaignData data)
    {
        string[] featureNames = Model.Data.featureNames;
        float[] features = new float[featureNames.Length];

        for (int i = 0; i < featureNames.Length; i++)
            features[i] = data.GetFeature(featureNames[i]);

        float[] probs = Model.Predict(features);

        string[] classes = Model.Data.classes;

        int best = 0;
        for (int i = 1; i < probs.Length; i++)
            if (probs[i] > probs[best]) best = i;

        Enum.TryParse(classes[best], out LeadershipTier tier);

        // Score: probability-weighted tier position (Normal = 0 ... Gorgeous = 1).
        float score = 0f;

        for (int i = 0; i < probs.Length; i++)
        {
            if (Enum.TryParse(classes[i], out LeadershipTier t))
                score += probs[i] * ((int)t - (int)LeadershipTier.Normal) / (float)(LeadershipTier.Gorgeous - LeadershipTier.Normal);
        }

        string trainedOn = Model.Data.realLabelledSamples > 0
            ? $"{Model.Data.realLabelledSamples} real + {Model.Data.syntheticSamples} synthetic runs"
            : $"{Model.Data.syntheticSamples} synthetic expert-rule runs";

        return new LeadershipResult
        {
            tier = tier,
            score = Mathf.Clamp01(score),
            confidence = probs[best],
            source = $"MLP neural network (trained on {trainedOn})",
            details = ProvisionalLeadershipEvaluator.TierDescription(tier)
        };
    }

    /// <summary> The most recent completed campaign other than the current one. </summary>
    public LeadershipCampaignData GetPreviousCompleted()
    {
        for (int i = History.Count - 1; i >= 0; i--)
        {
            LeadershipCampaignData run = History[i];
            if (run.IsCompleted && (Current == null || run.campaignId != Current.campaignId))
                return run;
        }

        return null;
    }

    public LeadershipCampaignData GetBestCompleted()
    {
        LeadershipCampaignData best = null;

        foreach (LeadershipCampaignData run in History)
        {
            if (run.IsCompleted && (best == null || run.leadershipScore > best.leadershipScore))
                best = run;
        }

        return best;
    }

    public List<RecruiterInfo> GetRecruiters()
    {
        List<RecruiterInfo> recruiters = new();

        MenuManager menu = MenuManager.Instance;
        if (menu == null)
            return recruiters;

        foreach (GameObject go in menu.availableReacruiter)
        {
            if (go == null) continue;

            Recruite_Trigger trigger = go.GetComponent<Recruite_Trigger>();
            if (trigger != null && trigger.recruiter != null)
                recruiters.Add(trigger.recruiter);
        }

        return recruiters;
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private AllyTaskRecord GetOrCreateTask(string recruiterName)
    {
        AllyTaskRecord task = Current.FindTask(recruiterName);
        if (task != null)
            return task;

        task = new AllyTaskRecord { recruiterName = recruiterName };
        Current.allyTasks.Add(task);
        return task;
    }

    public void Save()
    {
        nextAutosaveTime = Time.unscaledTime + autosaveInterval;

        if (Current != null)
            LeadershipStorage.SaveCurrent(Current);
    }

    // =========================================================
    // DEBUG (right-click the component in the Inspector)
    // =========================================================

    [ContextMenu("Debug/Enter Boss Area")]
    private void DebugEnterBossArea() => OnBossAreaEntered();

    [ContextMenu("Debug/Open Save Folder Path")]
    private void DebugLogFolder() => Debug.Log($"[Leadership] Save folder: {LeadershipStorage.Folder}");
}
