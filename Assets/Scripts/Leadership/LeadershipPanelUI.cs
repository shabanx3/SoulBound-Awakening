using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// LEADERSHIP PANEL UI
// =========================================================
// Fills the "Leadership Panel" in the Main UI canvas. Every
// field is optional, so the panel can be redesigned freely:
// move, restyle or delete any text and the rest still works.
// Toggle with the panel key (gameplay, pause, observer mode,
// campaign complete) or call Toggle() from a UI Button.
// =========================================================

public class LeadershipPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private KeyCode toggleKey = KeyCode.L;
    [SerializeField] private bool showOnCampaignComplete = true;
    [SerializeField] private float refreshInterval = 0.5f;

    [Header("Status")]
    [SerializeField] private Image statusBadge;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI sourceText;
    [SerializeField] private TextMeshProUGUI detailsText;

    [Header("Sections")]
    [SerializeField] private TextMeshProUGUI allyTasksText;
    [SerializeField] private TextMeshProUGUI alliesText;
    [SerializeField] private TextMeshProUGUI combatText;
    [SerializeField] private TextMeshProUGUI timingsText;
    [SerializeField] private TextMeshProUGUI comparisonText;
    [SerializeField] private TextMeshProUGUI hintText;

    [Header("Status Colours")]
    [SerializeField] private Color pendingColor = new(0.3f, 0.3f, 0.3f, 0.95f);
    [SerializeField] private Color normalColor = new(0.45f, 0.45f, 0.5f, 0.95f);
    [SerializeField] private Color averageColor = new(0.2f, 0.45f, 0.75f, 0.95f);
    [SerializeField] private Color goodColor = new(0.2f, 0.6f, 0.3f, 0.95f);
    [SerializeField] private Color gorgeousColor = new(0.8f, 0.6f, 0.1f, 0.95f);

    private float nextRefreshTime;
    private bool subscribed;

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

    private void Start()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (hintText != null)
            hintText.text = $"Press {toggleKey} to close";

        // Only ever described how the result was calculated; players don't need it.
        if (sourceText != null)
            sourceText.gameObject.SetActive(false);

        TrySubscribe();
    }

    private void OnDestroy()
    {
        if (subscribed && LeadershipSessionTracker.Instance != null)
            LeadershipSessionTracker.Instance.OnCampaignCompleted -= HandleCampaignCompleted;
    }

    private void Update()
    {
        TrySubscribe();

        if (panelRoot == null)
            return;

        if (Input.GetKeyDown(toggleKey) && CanToggle())
            Toggle();

        if (panelRoot.activeSelf && Time.unscaledTime >= nextRefreshTime)
            Refresh();
    }

    public void Toggle() => SetVisible(!IsVisible);

    public void SetVisible(bool visible)
    {
        if (panelRoot == null)
            return;

        panelRoot.SetActive(visible);

        if (visible)
            Refresh();
    }

    private bool CanToggle()
    {
        MenuManager menu = MenuManager.Instance;
        if (menu == null) return false;

        return menu.gameState == GameState.Gameplay ||
               menu.gameState == GameState.Pause ||
               menu.gameState == GameState.ObserverMode ||
               menu.gameState == GameState.CampaignComplete;
    }

    private void TrySubscribe()
    {
        if (subscribed || LeadershipSessionTracker.Instance == null)
            return;

        LeadershipSessionTracker.Instance.OnCampaignCompleted += HandleCampaignCompleted;
        subscribed = true;
    }

    private void HandleCampaignCompleted(LeadershipCampaignData data)
    {
        // Wait for the Warden's ending cinematic, if one is playing.
        if (showOnCampaignComplete)
            AbyssalWarden.WardenEndingCinematic.RunWhenFinished(() => SetVisible(true));
    }

    // =========================================================
    // CONTENT
    // =========================================================

    private void Refresh()
    {
        nextRefreshTime = Time.unscaledTime + refreshInterval;

        LeadershipSessionTracker tracker = LeadershipSessionTracker.Instance;

        if (tracker == null)
        {
            SetText(statusText, "UNAVAILABLE");
            SetText(detailsText, "No LeadershipSessionTracker in the scene.");
            return;
        }

        LeadershipCampaignData data = tracker.Current;
        LeadershipResult result = tracker.Evaluate();

        if (result.tier == LeadershipTier.Pending && result.score >= 0f)
            SetText(statusText, $"IN PROGRESS  <size=60%>({result.score * 100f:0.#}/100)</size>");
        else if (result.score >= 0f)
            SetText(statusText, $"{result.tier.ToString().ToUpper()}  <size=60%>({result.score * 100f:0}/100)</size>");
        else
            SetText(statusText, result.tier.ToString().ToUpper());

        if (statusBadge != null)
            statusBadge.color = TierColor(result.tier);

        string notice = "";
        if (data != null && data.IsCompleted)
            notice = "\n<color=#7CFC9A>Campaign complete. Return to the menu and start a New Game to record a new run.</color>";
        else if (data != null && data.startedMidway)
            notice = "\n<color=#FFB347>Some tasks were finished before this run was recorded, so their times are not counted.</color>";

        SetText(detailsText, result.details + notice);

        if (data == null)
        {
            SetText(allyTasksText, "");
            SetText(alliesText, "");
            SetText(combatText, "");
            SetText(timingsText, "");
            SetText(comparisonText, "");
            return;
        }

        SetText(allyTasksText, BuildAllyTasks(tracker, data));
        SetText(alliesText, BuildAllies(data) + "\n" + BuildAllyReactions(tracker));
        SetText(combatText, BuildCombat(data));
        SetText(timingsText, BuildTimings(data));
        SetText(comparisonText, BuildComparison(tracker, data));
    }

    private static string BuildAllyTasks(LeadershipSessionTracker tracker, LeadershipCampaignData data)
    {
        StringBuilder sb = new("<b>Ally Tasks</b>\n");

        MenuManager menu = MenuManager.Instance;
        string activeName = menu != null && menu.currentActiveRecruiterTask != null && menu.currentActiveRecruiterTask.isTaskActive
            ? menu.currentActiveRecruiterTask.recruiterName
            : null;

        foreach (RecruiterInfo recruiter in tracker.GetRecruiters())
        {
            AllyTaskRecord task = data.FindTask(recruiter.recruiterName);
            string state;

            if (task != null && task.completed)
                state = task.timeUnknown
                    ? "<color=#7CFC9A>Done</color> (time not recorded)"
                    : $"<color=#7CFC9A>Done</color> in {FormatTime(task.activeSeconds)}";
            else if (recruiter.recruiterName == activeName)
                state = $"<color=#FFD966>In progress</color> {FormatTime(task != null ? task.activeSeconds : 0f)}";
            else if (task != null && task.activeSeconds > 0f)
                state = $"<color=#FFB347>Paused</color> {FormatTime(task.activeSeconds)}";
            else
                state = "<color=#AAAAAA>Not started</color>";

            sb.AppendLine($"  {recruiter.recruiterName.Trim()}: {state}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildAllies(LeadershipCampaignData data)
    {
        StringBuilder sb = new("<b>Allies</b>\n");
        sb.AppendLine($"  Recruited: {data.alliesRecruited}");

        if (!data.alliesCheckedAtTeleport)
        {
            sb.Append("  <color=#AAAAAA>Checked when you teleport to the boss area. Keep your allies close!</color>");
            return sb.ToString();
        }

        sb.AppendLine($"  With you at the boss: {data.alliesWithPlayer}");
        sb.Append(data.alliesLeftBehind > 0
            ? $"  <color=#FF7B7B>Left behind: {data.alliesLeftBehind}</color> ({string.Join(", ", data.leftBehindAllyNames)})"
            : "  <color=#7CFC9A>No ally left behind</color>");

        return sb.ToString();
    }

    private static string BuildAllyReactions(LeadershipSessionTracker tracker)
    {
        StringBuilder sb = new("<b>Allies Reaction</b>\n");

        foreach (RecruiterInfo recruiter in tracker.GetRecruiters())
        {
            AllyReaction reaction = tracker.GetAllyReaction(recruiter);
            // First part of the name only ("Sir Aldric, The Ashen Knight"), so each reaction fits on one line.
            string shortName = recruiter.recruiterName.Split(',')[0].Trim();
            sb.AppendLine($"  {shortName}: {MoodLabel(reaction.mood)} <size=85%>({reaction.reason})</size>");
        }

        return sb.ToString().TrimEnd();
    }

    private static string MoodLabel(AllyMood mood)
    {
        switch (mood)
        {
            case AllyMood.Happy: return "<color=#7CFC9A>Happy</color>";
            case AllyMood.Okay: return "<color=#FFD966>Okay</color>";
            case AllyMood.Unhappy: return "<color=#FF7B7B>Not happy</color>";
            default: return "<color=#AAAAAA>Waiting</color>";
        }
    }

    private static string BuildCombat(LeadershipCampaignData data)
    {
        int undefeated = data.IsCompleted ? data.enemiesUndefeated : data.UndefeatedNow;

        return "<b>Combat</b>\n" +
               $"  Enemies in the area: {data.totalEnemies}\n" +
               $"  Total kills: {data.enemiesKilled}\n" +
               $"  Undefeated enemies: {(undefeated > 0 ? $"<color=#FF7B7B>{undefeated}</color>" : "0")}\n" +
               $"  Total deaths: {data.playerDeaths}" +
               (data.bossAreaDeaths > 0 ? $" ({data.bossAreaDeaths} in the boss area)" : "");
    }

    private static string BuildTimings(LeadershipCampaignData data)
    {
        return "<b>Timings</b>\n" +
               $"  Play time: {FormatTime(data.IsCompleted ? data.totalGameDurationSeconds : data.playTimeSeconds)}\n" +
               $"  Recruitment (sum of tasks): {FormatTime(data.KnownRecruitmentSeconds())}\n" +
               $"  Boss area: {BossTime(data, data.bossAreaEntered, data.bossAreaEnterTime, data.bossAreaDurationSeconds)}\n" +
               $"  Boss encounter: {BossTime(data, data.bossEncounterStarted, data.bossEncounterStartTime, data.bossEncounterDurationSeconds)}";
    }

    private static string BuildComparison(LeadershipSessionTracker tracker, LeadershipCampaignData data)
    {
        LeadershipCampaignData previous = tracker.GetPreviousCompleted();
        LeadershipCampaignData best = tracker.GetBestCompleted();

        if (previous == null)
            return "<b>Previous Runs</b>\n  No completed runs yet.";

        StringBuilder sb = new("<b>Previous Runs</b>\n");
        sb.AppendLine($"  Last: {previous.leadershipTier}, {FormatTime(previous.totalGameDurationSeconds)}, " +
                      $"{previous.enemiesKilled} kills, {previous.playerDeaths} deaths");

        if (best != null && best != previous)
            sb.AppendLine($"  Best: {best.leadershipTier}, {FormatTime(best.totalGameDurationSeconds)}");

        if (data.IsCompleted)
        {
            sb.Append($"  vs last: time {Delta(data.totalGameDurationSeconds - previous.totalGameDurationSeconds, true)}, " +
                      $"boss {Delta(data.bossEncounterDurationSeconds - previous.bossEncounterDurationSeconds, true)}, " +
                      $"kills {Delta(data.enemiesKilled - previous.enemiesKilled, false)}, " +
                      $"deaths {Delta(previous.playerDeaths - data.playerDeaths, false)}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BossTime(LeadershipCampaignData data, bool started, float startTime, float finalDuration)
    {
        if (!started) return "<color=#AAAAAA>Not started</color>";
        if (data.bossDefeated) return FormatTime(finalDuration);
        return $"<color=#FFD966>Running</color> {FormatTime(data.playTimeSeconds - startTime)}";
    }

    // Green = improvement. For times lower is better; for other values higher is better.
    private static string Delta(float value, bool isTime)
    {
        bool better = isTime ? value < 0f : value > 0f;
        string color = Mathf.Approximately(value, 0f) ? "#AAAAAA" : better ? "#7CFC9A" : "#FF7B7B";
        string sign = value > 0f ? "+" : value < 0f ? "-" : "±";
        string amount = isTime ? FormatTime(Mathf.Abs(value)) : Mathf.Abs(value).ToString("0");
        return $"<color={color}>{sign}{amount}</color>";
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return total >= 3600
            ? $"{total / 3600}:{total / 60 % 60:00}:{total % 60:00}"
            : $"{total / 60:00}:{total % 60:00}";
    }

    private Color TierColor(LeadershipTier tier)
    {
        switch (tier)
        {
            case LeadershipTier.Normal: return normalColor;
            case LeadershipTier.Average: return averageColor;
            case LeadershipTier.Good: return goodColor;
            case LeadershipTier.Gorgeous: return gorgeousColor;
            default: return pendingColor;
        }
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
            text.text = value;
    }
}
