using TMPro;
using UnityEngine;

public class Player_Heal_Manager : MonoBehaviour
{


    public int healFlaskAmount = 5;
    public TextMeshProUGUI healFlaskAmountText;
    public bool unlimitedHeal = false;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && MenuManager.Instance.gameState == GameState.Gameplay)
        {

            HealPlayer();
            Debug.Log("Player pressed R to heal.");
        }
    }

    private void Awake()
    {
        LoadHealData();
    }

    public void ResetHealPreferences()
    {
        
        PlayerPrefs.SetInt("HealFlaskAmount", 10);
        LoadHealData();
    }
    private void LoadHealData()
    {
        if (!PlayerPrefs.HasKey("HealFlaskAmount"))
        {
            PlayerPrefs.SetInt("HealFlaskAmount", healFlaskAmount);
        }
        else
        {
            healFlaskAmount = PlayerPrefs.GetInt("HealFlaskAmount");
        }


        UpdateHealFlaskUI();
    }

    public void HealPlayer()
    {
        if (healFlaskAmount > 0 || unlimitedHeal)
        {
            // Heal the player here
            Debug.Log("Player healed!");


            GetComponent<CustomEmeraldDamage>()?.RegenerateHealthToMax();

            MenuManager.Instance.playerHealEffect.Play();



            if (!unlimitedHeal)
            {
                healFlaskAmount--;

                PlayerPrefs.SetInt("HealFlaskAmount", healFlaskAmount);

                UpdateHealFlaskUI();
            }
        }
        else
        {
            Debug.Log("No heal flasks left!");
        }
    }


    public void UpdateHealFlaskUI()
    {
        healFlaskAmountText.text = healFlaskAmount.ToString();
    }

}
