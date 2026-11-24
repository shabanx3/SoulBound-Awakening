using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// LEADERSHIP DATA
// =========================================================
// Plain data saved to disk by LeadershipStorage.
// All "...Time" values are on the campaign play clock
// (seconds of unpaused gameplay, kept across scene reloads).
// =========================================================

public enum LeadershipTier
{
    Pending,
    Normal,
    Average,
    Good,
    Gorgeous
}

[Serializable]
public class AllyTaskRecord
{
    public string recruiterName;

    // Time the task was the active task (the "task time").
    public float activeSeconds;

    public bool completed;

    public float firstAssignedTime = -1f;
    public float completedTime = -1f;

    // True when the task was already done before tracking began
    // (e.g. an old save), so its time is not known.
    public bool timeUnknown;
}

[Serializable]
public class LeadershipCampaignData
{
    public string campaignId;
    public string startedUtc;
    public string endedUtc;

    // InProgress, Completed or Abandoned
    public string outcome = LeadershipCampaignData.OutcomeInProgress;

    // Recruiter progress already existed when tracking started.
    // Such runs should be excluded from training data.
    public bool startedMidway;

    public float playTimeSeconds;
    public int enemiesKilled;
    public int playerDeaths;
    public int bossAreaDeaths;

    // Enemies the active spawners will produce in the level ("enemies in the area").
    public int totalEnemies;

    // Allies recruited, and how many were not with the player at the boss teleport.
    public int alliesRecruited;
    public int alliesWithPlayer;
    public int alliesLeftBehind;
    public bool alliesCheckedAtTeleport;
    public List<string> leftBehindAllyNames = new();

    public List<AllyTaskRecord> allyTasks = new();

    public bool bossAreaEntered;
    public float bossAreaEnterTime = -1f;

    public bool bossEncounterStarted;
    public float bossEncounterStartTime = -1f;

    public bool bossDefeated;
    public float bossDefeatTime = -1f;

    // ---- MLP input features (filled when the campaign completes) ----
    public float recruitmentDurationSeconds;
    public float totalGameDurationSeconds;
    public float bossEncounterDurationSeconds;
    // enemiesKilled, enemiesUndefeated, alliesLeftBehind and playerDeaths complete the inputs.
    public int enemiesUndefeated;

    // Extra (not an MLP input)
    public float bossAreaDurationSeconds;

    // ---- Assessment at completion ----
    // Final result: from the MLP when a model is loaded, otherwise the rule-based estimate.
    public string leadershipTier;
    public float leadershipScore;
    public float leadershipConfidence = -1f;
    public string leadershipSource;

    // Rule-based estimate, always kept for comparison with the MLP.
    public string provisionalTier;
    public float provisionalScore;

    public const string OutcomeInProgress = "InProgress";
    public const string OutcomeCompleted = "Completed";
    public const string OutcomeAbandoned = "Abandoned";

    public bool IsCompleted => outcome == OutcomeCompleted;

    /// <summary> Enemies not killed yet (live), or at the end of the run. </summary>
    public int UndefeatedNow => Mathf.Max(0, totalEnemies - enemiesKilled);

    /// <summary> Feature values by name, as used by the MLP (see train_leadership_mlp.py). </summary>
    public float GetFeature(string name)
    {
        switch (name)
        {
            case "recruitment_duration_s": return recruitmentDurationSeconds;
            case "total_game_duration_s": return totalGameDurationSeconds;
            case "boss_encounter_duration_s": return bossEncounterDurationSeconds;
            case "enemies_killed": return enemiesKilled;
            case "enemies_undefeated": return enemiesUndefeated;
            case "allies_left_behind": return alliesLeftBehind;
            case "player_deaths": return playerDeaths;
            default: throw new ArgumentException($"Unknown feature '{name}'");
        }
    }

    public AllyTaskRecord FindTask(string recruiterName)
    {
        return allyTasks.Find(t => t.recruiterName == recruiterName);
    }

    public float KnownRecruitmentSeconds()
    {
        float total = 0f;

        foreach (AllyTaskRecord task in allyTasks)
        {
            if (task.completed && !task.timeUnknown)
                total += task.activeSeconds;
        }

        return total;
    }

    public int CompletedTaskCount(bool onlyKnownTime)
    {
        int count = 0;

        foreach (AllyTaskRecord task in allyTasks)
        {
            if (task.completed && (!onlyKnownTime || !task.timeUnknown))
                count++;
        }

        return count;
    }
}

public enum AllyMood
{
    Waiting,
    Happy,
    Okay,
    Unhappy
}

public struct AllyReaction
{
    public AllyMood mood;
    public string reason;
}

public struct LeadershipResult
{
    public LeadershipTier tier;
    public float score;          // 0..1, -1 when pending
    public float confidence;     // MLP top-class probability, -1 when not from the MLP
    public string source;        // where the result came from
    public string details;       // human readable explanation
}
