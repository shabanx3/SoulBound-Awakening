using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

// =========================================================
// LEADERSHIP STORAGE
// =========================================================
// Files live in Application.persistentDataPath/Leadership/
//   current_campaign.json  - the campaign being played
//   sessions.jsonl         - one finished/abandoned campaign per line
//   sessions.csv           - completed campaigns, for Python training.
//                            "leadership_label" is left empty on purpose:
//                            it must be filled with real labels
//                            (e.g. evaluator / questionnaire ratings).
// =========================================================

public static class LeadershipStorage
{
    private const string CsvHeader =
        "campaign_id,ended_utc,recruitment_duration_s,total_game_duration_s," +
        "boss_encounter_duration_s,enemies_killed,enemies_undefeated,allies_left_behind,player_deaths," +
        "total_enemies,allies_recruited,boss_area_deaths,boss_area_duration_s," +
        "started_midway,provisional_tier,provisional_score,mlp_tier,leadership_label";

    public static string Folder =>
        Path.Combine(Application.persistentDataPath, "Leadership");

    private static string CurrentPath => Path.Combine(Folder, "current_campaign.json");
    private static string HistoryPath => Path.Combine(Folder, "sessions.jsonl");
    private static string CsvPath => Path.Combine(Folder, "sessions.csv");

    // =========================
    // CURRENT CAMPAIGN
    // =========================

    public static LeadershipCampaignData LoadCurrent()
    {
        try
        {
            if (!File.Exists(CurrentPath))
                return null;

            return JsonUtility.FromJson<LeadershipCampaignData>(File.ReadAllText(CurrentPath));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LeadershipStorage: Could not read current campaign. {e.Message}");
            return null;
        }
    }

    public static void SaveCurrent(LeadershipCampaignData data)
    {
        if (data == null)
            return;

        try
        {
            Directory.CreateDirectory(Folder);

            // Write to a temp file first so a crash can't leave a half-written save.
            string tempPath = CurrentPath + ".tmp";
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
            File.Copy(tempPath, CurrentPath, true);
            File.Delete(tempPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LeadershipStorage: Could not save current campaign. {e.Message}");
        }
    }

    // =========================
    // HISTORY
    // =========================

    public static void AppendHistory(LeadershipCampaignData data)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.AppendAllText(HistoryPath, JsonUtility.ToJson(data, false) + Environment.NewLine);

            if (data.IsCompleted)
                AppendCsv(data);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LeadershipStorage: Could not append session history. {e.Message}");
        }
    }

    public static List<LeadershipCampaignData> LoadHistory()
    {
        List<LeadershipCampaignData> history = new();

        try
        {
            if (!File.Exists(HistoryPath))
                return history;

            foreach (string line in File.ReadAllLines(HistoryPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    LeadershipCampaignData data = JsonUtility.FromJson<LeadershipCampaignData>(line);
                    if (data != null)
                        history.Add(data);
                }
                catch (Exception)
                {
                    // Skip a corrupted line, keep the rest.
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LeadershipStorage: Could not read session history. {e.Message}");
        }

        return history;
    }

    private static void AppendCsv(LeadershipCampaignData d)
    {
        // An older file with different columns is kept aside instead of being mixed with the new rows.
        if (File.Exists(CsvPath) && File.ReadLines(CsvPath).FirstOrDefault() != CsvHeader)
        {
            string oldPath = Path.Combine(Folder, "sessions_v1.csv");
            if (!File.Exists(oldPath))
                File.Move(CsvPath, oldPath);
            else
                File.Move(CsvPath, Path.Combine(Folder, $"sessions_old_{DateTime.UtcNow:yyyyMMddHHmmss}.csv"));
        }

        bool writeHeader = !File.Exists(CsvPath);

        CultureInfo c = CultureInfo.InvariantCulture;
        bool fromMlp = d.leadershipConfidence >= 0f;

        string row = string.Join(",",
            d.campaignId,
            d.endedUtc,
            d.recruitmentDurationSeconds.ToString("F2", c),
            d.totalGameDurationSeconds.ToString("F2", c),
            d.bossEncounterDurationSeconds.ToString("F2", c),
            d.enemiesKilled.ToString(c),
            d.enemiesUndefeated.ToString(c),
            d.alliesLeftBehind.ToString(c),
            d.playerDeaths.ToString(c),
            d.totalEnemies.ToString(c),
            d.alliesRecruited.ToString(c),
            d.bossAreaDeaths.ToString(c),
            d.bossAreaDurationSeconds.ToString("F2", c),
            d.startedMidway ? "1" : "0",
            d.provisionalTier,
            d.provisionalScore.ToString("F3", c),
            fromMlp ? d.leadershipTier : "",
            "");

        File.AppendAllText(CsvPath, (writeHeader ? CsvHeader + Environment.NewLine : "") + row + Environment.NewLine);
    }
}
