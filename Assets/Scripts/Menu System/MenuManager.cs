using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using System;
using System.Collections.Generic;



public enum GameState
{
    MainMenu,
    Loading,
    Gameplay,
    Pause,
    CampaignComplete,

    ObserverMode,

    AllyTask,

    LevelFail,
}



[Serializable]
public class RecruiterInfo
{
    [TextArea] public string recruiterName;
    [TextArea] public string recruiterDetails;
    [TextArea] public string taskDetails;
    [TextArea] public string completedDetails;
    public Sprite recruiterProfileImage;

    public int requiredNumberOfElements = 5;

    public GameObject elementsLocation;

    public bool isTaskActive = false;

    public void SaveTaskStartingTime()
    {
        PlayerPrefs.SetString(recruiterName + "_TaskStartTime", DateTime.Now.ToString());
    }

    public void SaveTaskCompletionTime()
    {
        PlayerPrefs.SetString(recruiterName + "_TaskCompletionTime", DateTime.Now.ToString());
    }

    public void LoadTaskStartTime()
    {
        string startTime = PlayerPrefs.GetString(recruiterName + "_TaskStartTime", string.Empty);
        if (!string.IsNullOrEmpty(startTime))
        {
            Debug.Log($"Task for {recruiterName} started at: {startTime}");
        }
    }

    public void LoadTaskCompletionTime()
    {
        string completionTime = PlayerPrefs.GetString(recruiterName + "_TaskCompletionTime", string.Empty);
        if (!string.IsNullOrEmpty(completionTime))
        {
            Debug.Log($"Task for {recruiterName} completed at: {completionTime}");
        }
    }


    public (string, string) OverallTimeTaking()
    {
        string startTimeStr = PlayerPrefs.GetString(recruiterName + "_TaskStartTime", string.Empty);
        string completionTimeStr = PlayerPrefs.GetString(recruiterName + "_TaskCompletionTime", string.Empty);
        if (!string.IsNullOrEmpty(startTimeStr) && !string.IsNullOrEmpty(completionTimeStr))
        {
            DateTime startTime = DateTime.Parse(startTimeStr);
            DateTime completionTime = DateTime.Parse(completionTimeStr);
            TimeSpan duration = completionTime - startTime;
            Debug.Log($"Overall time taken for {recruiterName}'s task: {duration.TotalSeconds} seconds");

            return ($"Overall time taken for {recruiterName}'s task: {duration.TotalSeconds} seconds", completionTimeStr);
        }

        return (string.Empty, string.Empty);
    }

    public void SubmitElements(int amount)
    {
        // Logic to submit the task for this recruiter
        if (amount >= requiredNumberOfElements)
        {
            PlayerPrefs.SetInt(recruiterName + "_ElementsSubmitted", 1);
            elementsLocation.SetActive(false);

            MenuManager.Instance.currentActiveRecruiterTask.isTaskActive = false;
            MenuManager.Instance.currentActiveRecruiterTask = null;
            MenuManager.Instance.taskObjective.gameObject.SetActive(false);
            MenuManager.Instance.CountDoneRecruiter();
            Debug.Log($"Task submitted for {recruiterName}");
        }

      

    }


    public bool CheckTaskStatus()
    {
        return PlayerPrefs.GetInt(recruiterName + "_ElementsSubmitted", 0) == 1;   
    }

    public void ResetPreferences()
    {
        PlayerPrefs.DeleteKey(recruiterName + "_ElementsSubmitted");
        PlayerPrefs.DeleteKey(recruiterName + "_TaskStartTime");
        PlayerPrefs.DeleteKey(recruiterName + "_TaskCompletionTime");
    }
}


public class MenuManager : MonoBehaviour
{

    public static MenuManager Instance { get; private set; }


    #region Feilds


    public GameState gameState;

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private GameObject gameplayPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject campaignCompletePanel;
    public GameObject lvlFailPanell;
    [SerializeField] private GameObject continueBtnInteractable;
    [SerializeField] private GameObject continueBtnNotInteractable;



    [Header("Player Stuff")]
    public GameObject player;
    public Behaviour[] playerInputs;
    public ParticleSystem playerLightBeam;
    public ParticleSystem playerPickItemEffect;
    public ParticleSystem playerHealEffect;



    [Header("Loading Settings")]
    [SerializeField] private float loadingScreenDuration = 5f;
    [SerializeField] private TextMeshProUGUI loadingPercentage;
    [SerializeField] private Image loadingFillImage;

    [Header("Gameplay Scene")]
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private string firstTimePref = "FirstTime";
    [SerializeField] private GameObject allMineParent;
    [Tooltip("Each run starts from New Game, so the Continue button is hidden.")]
    [SerializeField] private bool showContinueButton = false;

    [Header("Universal Message System")]
    [SerializeField] private GameObject universalMessagePanel;
    [SerializeField] private TextMeshProUGUI universalMessageText;

    [Header("Recruiter System")]
    public List<GameObject> availableReacruiter = new();
    public List<RecruiterInfo> recruiterInfoList = new();
    public GameObject recruiterInfoPanel;
    public Image recruiterProfileImage;
    public TextMeshProUGUI recruiterNameText;
    public TextMeshProUGUI recruiterDetails;
    public TextMeshProUGUI taskDetails;
    public TextMeshProUGUI taskStatus;

    public Button assignTaskButton;
    public Button submitTaskButton;

    public RecruiterInfo currentActiveRecruiterTask;

    public TextMeshProUGUI taskObjective;
    public TextMeshProUGUI totalDoneRecruiterText;

    public int currentSubmittedElements = 0;


    public int totalRecruitersDone = 0;

    public GameObject teleporter;

    [Header("Observer Section")]
    public GameObject observerPanel;

    [Header("Sound")]

    public AudioSource PickItemSoundVFX;
    public AudioSource ObjectiveCompleteVFX;
    

    [Header("Story CS")]
    [SerializeField] private GameObject storyCS;
    [Tooltip("New Game: play the Abyssal Warden prologue cinematic (WardenPrologueCinematic) before gameplay.")]
    [SerializeField] private bool playPrologueCinematic = true;
    public bool showStartingStory = true;



  
    #endregion


    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;   
        }
      
    }
    private void Start()
    {
        TogglePlayerInputs(false);
        LoadInitalData();
        ShowMainMenu();
        

       
    }


    private void Update()
    {

        if(gameState == GameState.Gameplay) Cursor.lockState = CursorLockMode.Locked; else Cursor.lockState = CursorLockMode.None;

        if (Input.GetKeyDown(KeyCode.Escape) && gameState == GameState.Gameplay)
        {
            PauseGame();
        }
        else
        if (Input.GetKeyDown(KeyCode.O) && gameState == GameState.Gameplay)
        {

            //gameState = GameState.ObserverMode;
            //ShowObserverPanel(true);
 
     
        }
        else
        if (Input.GetKeyDown(KeyCode.O) && gameState == GameState.ObserverMode)
        {

            //gameState = GameState.Gameplay;
            //ShowObserverPanel(false);

        }

    }


    #region Load Data
    private void LoadInitalData()
    {
        if (!showContinueButton)
        {
            continueBtnInteractable.SetActive(false);
            continueBtnNotInteractable.SetActive(false);
        }
        else if (PlayerPrefs.GetInt(firstTimePref, 0) == 0)
        {
            continueBtnInteractable.SetActive(false);
            continueBtnNotInteractable.SetActive(true);
        }
        else
        {
            continueBtnInteractable.SetActive(true);
            continueBtnNotInteractable.SetActive(false);
        }


        CountDoneRecruiter();
    }

    public void NewGame()
    {
        PlayerPrefs.SetInt(firstTimePref, 0);

        foreach (var item in availableReacruiter)
        {
            item.GetComponent<Recruite_Trigger>().recruiter.ResetPreferences();
        }

        player.GetComponent<Player_Heal_Manager>().ResetHealPreferences();
        CountDoneRecruiter();
        LeadershipSessionTracker.StartNewCampaign();
        CashManager.Instance.ResetCash();
        ShowLoadingPanel();
        StartCoroutine(LoadGameplayScene());
    }

    #endregion



    public void TogglePlayerInputs(bool enabled)
    {
        foreach (var input in playerInputs)
        {
            input.enabled = enabled;
        }
    }

    #region Menu Button Functions

    

    public void PlayGame()
    {
        ShowLoadingPanel();
        StartCoroutine(LoadGameplayScene());
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    // Reloads the scene so everything (enemies, allies, player) is reset; it opens on the main menu.
    public void RestartToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // =========================
    // LOADING
    // =========================

    private void ShowLoadingPanel()
    {
        mainMenuPanel.SetActive(false);
        loadingPanel.SetActive(true);
        gameplayPanel.SetActive(false);
        pausePanel.SetActive(false);
        campaignCompletePanel.SetActive(false);
    }

    private IEnumerator LoadGameplayScene()
    {
        float elapsedTime = 0f;

        loadingFillImage.fillAmount = 0f;
        loadingPercentage.text = "0%";

        gameState = GameState.Loading;

        while (elapsedTime < loadingScreenDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsedTime / loadingScreenDuration);

            loadingPercentage.text = Mathf.FloorToInt(progress * 100f) + "%";
            loadingFillImage.fillAmount = progress;

            yield return null;
        }

        // Finish exactly on 100 %, not on the last interpolated step.
        loadingFillImage.fillAmount = 1f;
        loadingPercentage.text = "100%";

        loadingPanel.SetActive(false);

        if (PlayerPrefs.GetInt(firstTimePref, 0) == 0 && (showStartingStory || playPrologueCinematic))
        {
            PlayStoryCS();
        }
        else
        {
            storyCS.SetActive(false);
            ShowGameplayPanel();
        }
    }



    // Short loading screen during gameplay (teleport / respawn). The game is frozen meanwhile.
    // onLoadingShown runs once the panel is up, e.g. to move the player out of sight.
    public void ShowTransitionLoading(float duration, Action onLoadingShown = null)
    {
        StartCoroutine(TransitionLoadingRoutine(duration, onLoadingShown));
    }

    private IEnumerator TransitionLoadingRoutine(float duration, Action onLoadingShown)
    {
        gameState = GameState.Loading;
        TogglePlayerInputs(false);
        Time.timeScale = 0f;

        gameplayPanel.SetActive(false);
        loadingPanel.SetActive(true);
        loadingFillImage.fillAmount = 0f;
        loadingPercentage.text = "0%";

        onLoadingShown?.Invoke();

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsedTime / duration);

            loadingPercentage.text = Mathf.FloorToInt(progress * 100f) + "%";
            loadingFillImage.fillAmount = progress;

            yield return null;
        }

        loadingPanel.SetActive(false);
        ShowGameplayPanel();
    }

    // =========================
    // GAMEPLAY
    // =========================

    public void ShowGameplayPanel()
    {
        mainMenuPanel.SetActive(false);
        loadingPanel.SetActive(false);
        gameplayPanel.SetActive(true);
        pausePanel.SetActive(false);
        campaignCompletePanel.SetActive(false);
        gameState = GameState.Gameplay;
        TogglePlayerInputs(true);
        allMineParent.SetActive(true);
        Time.timeScale = 1f;
        LeadershipSessionTracker.NotifyGameplayStarted();
        AllyCompanionManager.SyncWithRecruiters();
    }

    // =========================
    // PAUSE
    // =========================

    public void PauseGame()
    {
        gameplayPanel.SetActive(false);
        pausePanel.SetActive(true);
        gameState = GameState.Pause;
        TogglePlayerInputs(false);
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        TogglePlayerInputs(true);
        pausePanel.SetActive(false);
        gameplayPanel.SetActive(true);
        gameState = GameState.Gameplay;
        Time.timeScale = 1f;
    }

  

    // =========================
    // MAIN MENU
    // =========================

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        loadingPanel.SetActive(false);
        gameplayPanel.SetActive(false);
        pausePanel.SetActive(false);
        campaignCompletePanel.SetActive(false);

        gameState = GameState.MainMenu;

        Time.timeScale = 1f;
    }

    // =========================
    // NEXT CAMPAIGN
    // =========================

    public void ContinueCampaign()
    {
        Time.timeScale = 1f;
        // Reserved for a second level; the delivered build has one campaign.
    }

    #endregion

    #region Story CS
    public void CloseStoryCS()
    {
        storyCS.SetActive(false);
        
        PlayerPrefs.SetInt(firstTimePref, 1);
        ShowGameplayPanel();
    }

    public void PlayStoryCS()
    {
        // The prologue cinematic tells the story, then starts gameplay the same way the story panel's close button does.
        if (playPrologueCinematic && AbyssalWarden.WardenPrologueCinematic.TryPlay(CloseStoryCS))
            return;

        // No prologue and no story panel to show: go straight into the game.
        if (!showStartingStory)
        {
            CloseStoryCS();
            return;
        }

        storyCS.SetActive(true);
        
    }

    #endregion


    #region Compain Complete
    // =========================
    // CAMPAIGN COMPLETE
    // =========================

    public void ShowCampaignComplete()
    {
        gameplayPanel.SetActive(false);
        campaignCompletePanel.SetActive(true);

        gameState = GameState.CampaignComplete;

        TogglePlayerInputs(false);
        Time.timeScale = 0f;
    }
    #endregion


   
    #region Recruiter System

    public void CountDoneRecruiter()
    {
        totalRecruitersDone = 0;

        for (int i = 0; i < availableReacruiter.Count; i++)
        {
            Recruite_Trigger trigger = availableReacruiter[i].GetComponent<Recruite_Trigger>();

            if (trigger != null && trigger.recruiter != null)
            {
                if (trigger.recruiter.CheckTaskStatus())
                {
                    totalRecruitersDone++;
                }
            }
        }

        totalDoneRecruiterText.text =
            $"Total Recruiters Done: {totalRecruitersDone}/{availableReacruiter.Count}";

        if (totalRecruitersDone == availableReacruiter.Count)
        {
            teleporter.SetActive(true);
        }
        else
        {
            teleporter.SetActive(false);
        }

    }


    public void ShowRecruiterInfo(RecruiterInfo recruiterInfo)
    {
        if (recruiterInfo == null)
            return;


        gameState = GameState.AllyTask;
        TogglePlayerInputs(false);

        recruiterProfileImage.sprite = recruiterInfo.recruiterProfileImage;
        recruiterNameText.text = recruiterInfo.recruiterName;
        recruiterDetails.text = recruiterInfo.recruiterDetails;

        recruiterInfoPanel.SetActive(true);

        // Remove previous button listeners
        assignTaskButton.onClick.RemoveAllListeners();

        // Add listener for this recruiter
        assignTaskButton.onClick.AddListener(() =>
            AssignTaskToRecruiter(recruiterInfo));

        Debug.Log($"Current Active Recruiter: {recruiterInfo?.recruiterName ?? "None"}");

        taskStatus.text = "Status: Undone";

        // --------------------------------------------------
        // TASK ALREADY COMPLETED
        // --------------------------------------------------

        if (recruiterInfo.CheckTaskStatus())
        {
            taskDetails.text = recruiterInfo.completedDetails;

            assignTaskButton.gameObject.SetActive(false);
            submitTaskButton.gameObject.SetActive(false);

            taskStatus.text = "Status: Task is already Completed.";

            return;
        }

        // --------------------------------------------------
        // TASK NOT COMPLETED
        // --------------------------------------------------

        taskDetails.text = recruiterInfo.taskDetails;

        // Check if this recruiter is currently active
        if (currentActiveRecruiterTask != null && currentActiveRecruiterTask == recruiterInfo)
        {
            assignTaskButton.gameObject.SetActive(false);

            taskStatus.text = "Status: Task is already assigned.";

            // Show submit button only when enough elements are collected
            if (currentSubmittedElements >= recruiterInfo.requiredNumberOfElements)
            {
                submitTaskButton.gameObject.SetActive(true);
            }
            else
            {
                submitTaskButton.gameObject.SetActive(false);
            }
        }
        else
        {
            // Another recruiter or no recruiter is currently active
            assignTaskButton.gameObject.SetActive(true);
            submitTaskButton.gameObject.SetActive(false);

            if (currentActiveRecruiterTask != null && currentActiveRecruiterTask.isTaskActive)
            {
                assignTaskButton.gameObject.SetActive(false);
                taskStatus.text = "Status: Another task is already assigned.";
            }
        }

        
    }


    public void CloseRecruiterInfo()
    {
        recruiterInfoPanel.SetActive(false);
        gameState = GameState.Gameplay;
        TogglePlayerInputs(true);
    }


    private void AssignTaskToRecruiter(RecruiterInfo recruiterInfo)
    {
        if (recruiterInfo == null)
            return;

        // --------------------------------------------------
        // CHECK IF ANOTHER TASK IS ALREADY ACTIVE
        // --------------------------------------------------

        if (currentActiveRecruiterTask != null && currentActiveRecruiterTask.isTaskActive == true)
        {
            ShowMessage("Some Task is already assigned.");
            return;
        }

        // --------------------------------------------------
        // ASSIGN NEW TASK
        // --------------------------------------------------

        currentActiveRecruiterTask = recruiterInfo;
        currentSubmittedElements = 0;
        currentActiveRecruiterTask.isTaskActive = true;
        LeadershipSessionTracker.ReportAllyTaskAssigned(currentActiveRecruiterTask.recruiterName);
        taskObjective.text =
            $"<color=yellow>{currentActiveRecruiterTask.recruiterName} Task:</color> " +
            $"<color=green>{currentActiveRecruiterTask.taskDetails}</color>";

        // Enable objective location
        if (currentActiveRecruiterTask.elementsLocation != null)
        {
            currentActiveRecruiterTask.elementsLocation.SetActive(true);
        }

        taskObjective.gameObject.SetActive(true);

        submitTaskButton.gameObject.SetActive(false);

        CloseRecruiterInfo();

        ShowMessage($"Task assigned to {currentActiveRecruiterTask.recruiterName}.");
    }


    public void SubmitTaskForCurrentRecruiter()
    {
        if (currentActiveRecruiterTask.isTaskActive == false)
        {
            ShowMessage("No active task to submit.");
            return;
        }

        // Reject the submission unless every required item has been collected.
        if (currentSubmittedElements <
            currentActiveRecruiterTask.requiredNumberOfElements)
        {
            ShowMessage(
                $"You need {currentActiveRecruiterTask.requiredNumberOfElements} elements."
            );

            return;
        }

        // Submit collected elements
        string completedRecruiterName =
            currentActiveRecruiterTask.recruiterName;

        currentActiveRecruiterTask.SubmitElements(currentSubmittedElements);
        LeadershipSessionTracker.ReportAllyTaskCompleted(completedRecruiterName);
        AllyCompanionManager.OnRecruiterCompleted(completedRecruiterName);

        // Reset objective
        currentSubmittedElements = 0;

        taskObjective.gameObject.SetActive(false);
        submitTaskButton.gameObject.SetActive(false);

        ShowMessage(
            $"Task submitted for {completedRecruiterName}."
        );


        playerLightBeam.Play();
        ObjectiveCompleteVFX.Play();

        bool portalWasOn = teleporter.activeSelf;
        CountDoneRecruiter();

        if (!portalWasOn && teleporter.activeSelf)
            PortalRevealCinematic.Play(teleporter);
    }


    public void UpdateTaskStatus()
    {
        if (currentActiveRecruiterTask == null)
        {
            taskStatus.text = "Status: No active task.";
            return;
        }

        if (currentActiveRecruiterTask.CheckTaskStatus())
        {
            taskStatus.text = "Status: Task is Completed.";
        }
        else
        {
            taskStatus.text = "Status: Task is already assigned.";
        }
    }


    public void IncrementSubmittedElements(int amount)
    {
        if (currentActiveRecruiterTask == null)
        {
            ShowMessage("No task is currently assigned.");
            return;
        }

        currentSubmittedElements += amount;
        playerPickItemEffect.Play();

        PickItemSoundVFX.Play();

        // Don't allow the counter to exceed required amount
        currentSubmittedElements = Mathf.Min(
            currentSubmittedElements,
            currentActiveRecruiterTask.requiredNumberOfElements
        );

        ShowMessage(
            $"Collected {amount} elements. " +
            $"Total: {currentSubmittedElements}/" +
            $"{currentActiveRecruiterTask.requiredNumberOfElements}"
        );

        // Automatically enable submit button when enough elements collected
        if (currentSubmittedElements >=
            currentActiveRecruiterTask.requiredNumberOfElements)
        {
            submitTaskButton.gameObject.SetActive(true);
        }
        else
        {
            submitTaskButton.gameObject.SetActive(false);
        }
    }

    #endregion


    #region Universal Message System

    public void ShowMessage(string message, float duration = 2f)
    {
        StartCoroutine(ShowMessageCoroutine(message, duration));
    }

    IEnumerator ShowMessageCoroutine(string message, float duration)
    {
        universalMessageText.text = message;  
        universalMessagePanel.SetActive(true);
        yield return new WaitForSecondsRealtime(duration);
        universalMessagePanel.SetActive(false);
    }

    #endregion

    #region Level Fail

    public void ShowLevelFailPanel()
    {
        LeadershipSessionTracker.ReportPlayerDeath();

        // Dying in the boss area respawns the player at the gate instead of reloading.
        if (BossAreaRespawn.TryHandlePlayerDeath())
            return;

        StartCoroutine(LevelFailHelper());
    }


    IEnumerator LevelFailHelper()
    {
        yield return new WaitForSecondsRealtime(2f);
        gameplayPanel.SetActive(false);
        lvlFailPanell.SetActive(true);
        gameState = GameState.LevelFail;
        Time.timeScale = 0f;
     
        yield return new WaitForSecondsRealtime(3f);
        SceneManager.LoadScene(0);

    }


    #endregion

    #region Observer System

    public void ShowObserverPanel(bool active)
    {

        if (active)
        {
            Time.timeScale = 0f;
            TogglePlayerInputs(false);
        }
        else
        {
            Time.timeScale = 1f;
            TogglePlayerInputs(true);
        }

        observerPanel.SetActive(active);
    }


    #endregion
}