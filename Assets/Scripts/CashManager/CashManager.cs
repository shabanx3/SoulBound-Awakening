using UnityEngine;
using TMPro;

public class CashManager : MonoBehaviour
{
    public static CashManager Instance { get; private set; }

    [Header("Cash Settings")]
    [SerializeField] private int startingCash = 0;

    [Header("UI")]
    [SerializeField] private TMP_Text[] cashTexts;

    private int currentCash;

    public int CurrentCash => currentCash;


    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load saved cash
        currentCash = PlayerPrefs.GetInt("PlayerCash", startingCash);

        UpdateCashUI();
    }


    public void ResetCash()
    {
        currentCash = startingCash;
        SaveCash();
        UpdateCashUI();
    }

    // =========================
    // ADD CASH
    // =========================
    public void AddCash(int amount)
    {
        if (amount <= 0)
            return;

        currentCash += amount;

        SaveCash();
        UpdateCashUI();

        Debug.Log($"<color=green>Cash Added:</color> +{amount} | Total: {currentCash}");
    }


    // =========================
    // REMOVE CASH
    // =========================
    public bool RemoveCash(int amount)
    {
        if (amount <= 0)
            return false;

        if (currentCash < amount)
        {
            Debug.LogWarning(
                $"Not enough cash! Required: {amount} | Available: {currentCash}"
            );

            return false;
        }

        currentCash -= amount;

        SaveCash();
        UpdateCashUI();

        Debug.Log($"<color=yellow>Cash Removed:</color> -{amount} | Total: {currentCash}");

        return true;
    }


    // =========================
    // CHECK CASH
    // =========================
    public bool HasEnoughCash(int amount)
    {
        return currentCash >= amount;
    }


    // =========================
    // SET CASH
    // =========================
    public void SetCash(int amount)
    {
        currentCash = Mathf.Max(0, amount);

        SaveCash();
        UpdateCashUI();
    }


    // =========================
    // GET CASH
    // =========================
    public int GetCash()
    {
        return currentCash;
    }


    // =========================
    // UPDATE UI
    // =========================
    private void UpdateCashUI()
    {
        if (cashTexts == null)
            return;

        foreach (TMP_Text text in cashTexts)
        {
            if (text != null)
            {
                text.text = currentCash.ToString();
            }
        }
    }


    // =========================
    // SAVE
    // =========================
    private void SaveCash()
    {
        PlayerPrefs.SetInt("PlayerCash", currentCash);
        PlayerPrefs.Save();
    }


    // =========================
    // REFRESH UI
    // =========================
    public void RefreshUI()
    {
        UpdateCashUI();
    }
}