using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// PLAYER COMBAT BOOTSTRAP
// =========================================================
// Attaches PlayerTargeting + PlayerSpellCaster to the player
// (the object carrying PlayerWardenDamageReceiver) whenever a
// scene loads, so the large Player prefab does not need to be
// edited. Settings: Resources/PlayerSpellConfig.
// =========================================================

public static class PlayerCombatBootstrap
{
    private static PlayerSpellConfig config;

    public static PlayerSpellConfig Config
    {
        get
        {
            if (config == null)
            {
                config = Resources.Load<PlayerSpellConfig>("PlayerSpellConfig");
                if (config == null)
                {
                    Debug.LogWarning("[PlayerCombat] Resources/PlayerSpellConfig not found; using defaults without VFX/SFX.");
                    config = ScriptableObject.CreateInstance<PlayerSpellConfig>();
                }
            }
            return config;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Attach();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

    private static void Attach()
    {
        PauseKeybindings.Install(Config);

        var receiver = Object.FindFirstObjectByType<PlayerWardenDamageReceiver>(FindObjectsInactive.Include);
        if (receiver == null) return;

        GameObject player = receiver.gameObject;
        var targeting = player.GetComponent<PlayerTargeting>();
        if (targeting == null) { targeting = player.AddComponent<PlayerTargeting>(); }
        targeting.config = Config;

        var caster = player.GetComponent<PlayerSpellCaster>();
        if (caster == null) caster = player.AddComponent<PlayerSpellCaster>();
        caster.config = Config;

        var dodge = player.GetComponent<PlayerDodgeGuard>();
        if (dodge == null) dodge = player.AddComponent<PlayerDodgeGuard>();
        dodge.config = Config;

        if (player.GetComponent<PlayerHealthBarFeedback>() == null) player.AddComponent<PlayerHealthBarFeedback>();

        var bars = player.GetComponent<EnemyHealthBarVisibility>();
        if (bars == null) bars = player.AddComponent<EnemyHealthBarVisibility>();
        bars.config = Config;
    }
}
