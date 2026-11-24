using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================
// PROVISIONAL LEADERSHIP EVALUATOR
// =========================================================
// Rule-based ("expert rules") estimate. It is used:
//   - live during a run (the MLP needs the finished run's features)
//   - as the label source for the MLP's synthetic training data
// MLP_Training/expert_rules.json must match these values, so
// retrain the MLP after changing them.
//
// Score = weighted mean of the parts below - death penalty.
// =========================================================

[Serializable]
public class ProvisionalLeadershipEvaluator
{
    public const string SourceName = "Rule-based estimate (no MLP model loaded)";

    [Header("Ally Tasks (average seconds per task)")]
    public float goodTaskSeconds = 120f;
    public float poorTaskSeconds = 600f;
    public float taskWeight = 1f;

    [Header("Total Game Duration (seconds)")]
    public float goodGameSeconds = 1800f;
    public float poorGameSeconds = 5400f;
    public float gameWeight = 1f;

    [Header("Boss Encounter (seconds)")]
    public float goodBossSeconds = 90f;
    public float poorBossSeconds = 600f;
    public float bossWeight = 1f;

    [Header("Enemy Clearance (killed / enemies in the area)")]
    [Range(0f, 1f)] public float poorClearance = 0.3f;
    [Range(0f, 1f)] public float goodClearance = 0.9f;
    public float clearanceWeight = 1.5f;

    [Header("Allies Kept (1 - left behind / recruited)")]
    public float alliesWeight = 1.5f;

    [Header("Player Deaths")]
    [Range(0f, 0.5f)] public float penaltyPerDeath = 0.07f;
    [Range(0f, 1f)] public float maxDeathPenalty = 0.35f;

    [Header("Tier Thresholds (score 0..1)")]
    [Range(0f, 1f)] public float averageFrom = 0.35f;
    [Range(0f, 1f)] public float goodFrom = 0.55f;
    [Range(0f, 1f)] public float gorgeousFrom = 0.75f;

    [Header("Ally Reaction (task speed 0..1 - death penalty)")]
    [Range(0f, 1f)] public float allyHappyFrom = 0.6f;
    [Range(0f, 1f)] public float allyOkayFrom = 0.3f;

    [Header("Live Score (points out of 100; the rest is only earned by defeating the boss)")]
    public float allyTaskPoints = 30f;
    public float enemyKillPoints = 30f;
    public float alliesKeptPoints = 10f;

    /// <summary> How a recruiter feels about the player. Display only: it does not change the leadership score. </summary>
    public AllyReaction EvaluateAlly(LeadershipCampaignData data, AllyTaskRecord task, int alliesLeftBehind)
    {
        if (task == null || !task.completed)
            return new AllyReaction { mood = AllyMood.Waiting, reason = "Needs your help" };

        if (alliesLeftBehind > 0)
            return new AllyReaction { mood = AllyMood.Unhappy, reason = "You left their people behind" };

        if (task.timeUnknown)
            return new AllyReaction { mood = AllyMood.Okay, reason = "Glad you helped" };

        float speed = Mathf.InverseLerp(poorTaskSeconds, goodTaskSeconds, task.activeSeconds);
        float value = speed - Mathf.Min(maxDeathPenalty, data.playerDeaths * penaltyPerDeath);

        if (value >= allyHappyFrom)
            return new AllyReaction { mood = AllyMood.Happy, reason = "You helped them quickly" };

        if (value >= allyOkayFrom)
            return new AllyReaction
            {
                mood = AllyMood.Okay,
                reason = speed >= allyHappyFrom ? "Worried by your falls in battle" : "Wished you had come sooner"
            };

        return new AllyReaction
        {
            mood = AllyMood.Unhappy,
            reason = speed >= allyOkayFrom ? "Losing faith after your falls in battle" : "You kept them waiting too long"
        };
    }

    /// <summary> Score that builds up during a run. It stays Pending: the level is only given once the boss is defeated. </summary>
    public LeadershipResult EvaluateLive(LeadershipCampaignData data, int allyTaskCount)
    {
        float taskShare = allyTaskPoints / Mathf.Max(1, allyTaskCount, data.allyTasks.Count);
        float taskPoints = 0f;

        foreach (AllyTaskRecord task in data.allyTasks)
        {
            if (!task.completed) continue;

            // A finished task always earns at least half its share; speed earns the rest.
            float speed = task.timeUnknown ? 0f : Mathf.InverseLerp(poorTaskSeconds, goodTaskSeconds, task.activeSeconds);
            taskPoints += taskShare * Mathf.Lerp(.5f, 1f, speed);
        }

        float killsForFullPoints = data.totalEnemies * goodClearance;
        float killPoints = killsForFullPoints > 0f ? enemyKillPoints * Mathf.Clamp01(data.enemiesKilled / killsForFullPoints) : 0f;

        float allyPoints = 0f;
        if (data.alliesCheckedAtTeleport)
        {
            float kept = data.alliesRecruited > 0 ? 1f - (float)data.alliesLeftBehind / data.alliesRecruited : 1f;
            allyPoints = alliesKeptPoints * Mathf.Clamp01(kept);
        }

        float deathPoints = Mathf.Min(maxDeathPenalty, data.playerDeaths * penaltyPerDeath) * 100f;
        float total = Mathf.Max(0f, taskPoints + killPoints + allyPoints - deathPoints);

        return new LeadershipResult
        {
            tier = LeadershipTier.Pending,
            score = Mathf.Clamp01(total / 100f),
            confidence = -1f,
            source = "",
            details =
                "Your score grows as you play. Your final score and level come after you defeat the boss." +
                $"\nAlly tasks: +{taskPoints:0.#}   Enemies: +{killPoints:0.#}" +
                (data.alliesCheckedAtTeleport ? $"   Allies kept: +{allyPoints:0.#}" : "") +
                (deathPoints > 0f ? $"   Deaths: -{deathPoints:0.#}" : "")
        };
    }

    public LeadershipResult Evaluate(LeadershipCampaignData data)
    {
        LeadershipResult result = new()
        {
            tier = LeadershipTier.Pending,
            score = -1f,
            confidence = -1f,
            source = SourceName
        };

        if (data == null)
        {
            result.details = "No campaign is being tracked yet. Start a New Game to begin an assessment.";
            return result;
        }

        int knownTasks = data.CompletedTaskCount(true);

        if (knownTasks == 0)
        {
            result.details = "Complete at least one ally task to receive a leadership estimate.";
            return result;
        }

        List<(string name, float value, float weight)> parts = new();

        float avgTask = data.KnownRecruitmentSeconds() / knownTasks;
        parts.Add(("Ally task speed", Mathf.InverseLerp(poorTaskSeconds, goodTaskSeconds, avgTask), taskWeight));

        float clearance = data.totalEnemies > 0 ? (float)data.enemiesKilled / data.totalEnemies : 1f;
        parts.Add(("Enemies defeated", Mathf.InverseLerp(poorClearance, goodClearance, clearance), clearanceWeight));

        if (data.alliesCheckedAtTeleport)
        {
            float kept = data.alliesRecruited > 0 ? 1f - (float)data.alliesLeftBehind / data.alliesRecruited : 1f;
            parts.Add(("Allies kept together", Mathf.Clamp01(kept), alliesWeight));
        }

        if (data.bossDefeated)
        {
            parts.Add(("Boss encounter speed",
                Mathf.InverseLerp(poorBossSeconds, goodBossSeconds, data.bossEncounterDurationSeconds), bossWeight));

            parts.Add(("Overall campaign speed",
                Mathf.InverseLerp(poorGameSeconds, goodGameSeconds, data.totalGameDurationSeconds), gameWeight));
        }

        float sum = 0f, weights = 0f;
        foreach (var part in parts)
        {
            sum += part.value * part.weight;
            weights += part.weight;
        }

        float deathPenalty = Mathf.Min(maxDeathPenalty, data.playerDeaths * penaltyPerDeath);
        float score = Mathf.Clamp01(sum / Mathf.Max(0.0001f, weights) - deathPenalty);

        result.score = score;
        result.tier = TierFor(score);

        parts.Sort((a, b) => b.value.CompareTo(a.value));

        result.details =
            TierDescription(result.tier) +
            $"\nStrongest area: {parts[0].name}. Needs work: {parts[parts.Count - 1].name}." +
            (data.alliesLeftBehind > 0 ? $"\nAllies left behind: {data.alliesLeftBehind}" : "") +
            (data.playerDeaths > 0 ? $"\nDeath penalty: -{deathPenalty:0.00}" : "");

        return result;
    }

    public LeadershipTier TierFor(float score)
    {
        if (score >= gorgeousFrom) return LeadershipTier.Gorgeous;
        if (score >= goodFrom) return LeadershipTier.Good;
        if (score >= averageFrom) return LeadershipTier.Average;
        return LeadershipTier.Normal;
    }

    public static string TierDescription(LeadershipTier tier)
    {
        switch (tier)
        {
            case LeadershipTier.Normal:
                return "Normal: you get the job done, but allies wait a long time for help, get left behind, or fights drag on.";
            case LeadershipTier.Average:
                return "Average: steady leadership with room to act faster, clear more threats and keep your allies together.";
            case LeadershipTier.Good:
                return "Good: decisive leadership. Allies are recruited quickly, kept close, and threats are handled well.";
            case LeadershipTier.Gorgeous:
                return "Gorgeous: outstanding leadership. Fast recruitment, a cleared region, every ally at your side and a commanding boss fight.";
            default:
                return "Pending: not enough data yet.";
        }
    }
}
