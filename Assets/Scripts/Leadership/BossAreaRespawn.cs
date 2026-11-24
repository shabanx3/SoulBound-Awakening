using System.Collections;
using MalbersAnimations;
using MalbersAnimations.Controller;
using UnityEngine;

// =========================================================
// BOSS AREA RESPAWN
// =========================================================
// Put on the spawn point near the boss-area teleport.
// When the player dies inside the boss area, instead of the
// Level Fail reload the player is revived here with full
// health and refilled heal flasks. The death still counts
// (MenuManager.ShowLevelFailPanel reports it first).
// The revive mirrors Malbers' MRespawner for a scene player.
// =========================================================

public class BossAreaRespawn : MonoBehaviour
{
    public static BossAreaRespawn Instance { get; private set; }

    [Tooltip("Leave empty to use this object's position and rotation.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Real seconds to let the death animation play before the loading panel.")]
    [SerializeField] private float deathAnimationDelay = 2.5f;

    [SerializeField] private float loadingDuration = 2.5f;

    [SerializeField] private bool refillHealFlasks = true;

    [SerializeField] private string respawnMessage = "You have fallen... rise again at the gate.";

    private bool respawning;

    private void Awake()
    {
        Instance = this;

        if (spawnPoint == null)
            spawnPoint = transform;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary> Returns true when the death is handled here (player is in the boss area). </summary>
    public static bool TryHandlePlayerDeath()
    {
        if (Instance == null || !LeadershipBossAreaEntry.PlayerInBossArea)
            return false;

        if (!Instance.respawning)
            Instance.StartCoroutine(Instance.RespawnRoutine());

        return true;
    }

    private IEnumerator RespawnRoutine()
    {
        respawning = true;

        MenuManager menu = MenuManager.Instance;
        menu.ShowMessage(respawnMessage, deathAnimationDelay);

        yield return new WaitForSecondsRealtime(deathAnimationDelay);

        menu.ShowTransitionLoading(loadingDuration, RevivePlayer);

        yield return new WaitForSecondsRealtime(loadingDuration);

        respawning = false;
    }

    private void RevivePlayer()
    {
        GameObject player = MenuManager.Instance.player;
        MAnimal animal = player.GetComponent<MAnimal>();

        if (animal != null)
        {
            // Start in the default (Idle) state instead of the Death state the animal saved.
            animal.OverrideStartState = null;
            animal.TeleportRot(spawnPoint);

            if (!animal.InputSource.IsUnityRefNull())
                animal.InputSource.Enable(true);

            if (animal.MainCollider)
                animal.MainCollider.enabled = true;

            animal.SetMainPlayer();
            animal.Anim.Rebind();

            foreach (IRestart restart in player.GetComponentsInChildren<IRestart>())
                restart.Restart();

            if (!animal.enabled)
                animal.enabled = true;
            else
                animal.ResetController();
        }
        else
        {
            player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }

        // Full health on both the Malbers stats and the Emerald bridge.
        CustomEmeraldDamage damage = player.GetComponent<CustomEmeraldDamage>();
        if (damage != null)
            damage.RegenerateHealthToMax();

        if (refillHealFlasks)
        {
            Player_Heal_Manager heal = player.GetComponent<Player_Heal_Manager>();
            if (heal != null)
                heal.ResetHealPreferences();
        }

        LeadershipSessionTracker.ResetDeathGuard();
    }
}
