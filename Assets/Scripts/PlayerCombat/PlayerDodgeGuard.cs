using EmeraldAI;
using MalbersAnimations.Controller;
using UnityEngine;

// =========================================================
// PLAYER DODGE GUARD (perfect dodge)
// =========================================================
// For a short window after a Malbers Dodge (Q) starts, every
// hit is avoided:
// - Warden hits     -> PlayerWardenDamageReceiver asks TryAvoid()
// - Emerald hits    -> the Emerald player bridge is set Immortal
//                      for the window and reports blocked hits
// A dodged hit shows "PERFECT DODGE!" with a short slow motion.
// =========================================================

public class PlayerDodgeGuard : MonoBehaviour
{
    public PlayerSpellConfig config;

    private MAnimal animal;
    private EmeraldGeneralTargetBridge bridge;
    private bool bridgeImmortalByUs;
    private bool wasDodging;
    private float dodgeStart = -10f;

    private float textUntil = -1f, slowMoUntil = -1f;
    private bool slowMoActive;

    public bool IsInvulnerable => animal != null && animal.ActiveModeID == config.dodgeModeID && Time.time - dodgeStart <= config.perfectDodgeWindow;

    private void Awake()
    {
        animal = GetComponent<MAnimal>();
        if (animal == null) animal = GetComponentInChildren<MAnimal>();
        bridge = GetComponent<EmeraldGeneralTargetBridge>();
    }

    private void OnEnable() => EmeraldGeneralTargetBridge.DamageBlocked += OnEmeraldBlocked;

    private void OnDisable()
    {
        EmeraldGeneralTargetBridge.DamageBlocked -= OnEmeraldBlocked;
        if (bridgeImmortalByUs && bridge != null) bridge.Immortal = false;
        bridgeImmortalByUs = false;
        EndSlowMo();
    }

    private void Update()
    {
        bool dodging = animal != null && animal.ActiveModeID == config.dodgeModeID;
        if (dodging && !wasDodging) dodgeStart = Time.time;
        wasDodging = dodging;

        // Emerald damage is blocked at the bridge; only touch Immortal if nothing else turned it on.
        if (bridge != null)
        {
            bool invulnerable = IsInvulnerable;
            if (invulnerable && !bridge.Immortal) { bridge.Immortal = true; bridgeImmortalByUs = true; }
            else if (!invulnerable && bridgeImmortalByUs) { bridge.Immortal = false; bridgeImmortalByUs = false; }
        }

        if (slowMoActive && Time.unscaledTime >= slowMoUntil) EndSlowMo();
    }

    /// <summary> True (and shows the perfect-dodge feedback) if the player is inside the dodge window. </summary>
    public static bool TryAvoid(GameObject player)
    {
        var guard = player != null ? player.GetComponent<PlayerDodgeGuard>() : null;
        if (guard == null || !guard.IsInvulnerable) return false;
        guard.PerfectDodge();
        return true;
    }

    private void OnEmeraldBlocked(EmeraldGeneralTargetBridge source, int damage)
    {
        if (source == bridge && bridgeImmortalByUs) PerfectDodge();
    }

    private void PerfectDodge()
    {
        bool first = Time.unscaledTime >= textUntil;
        textUntil = Time.unscaledTime + 1f;
        if (!first) return; // several hits in one dodge: one celebration

        if (config.perfectDodgeSound != null) AudioSource.PlayClipAtPoint(config.perfectDodgeSound, transform.position);

        if (config.perfectDodgeTimeScale > 0f && config.perfectDodgeTimeScale < 1f && Mathf.Approximately(Time.timeScale, 1f))
        {
            Time.timeScale = config.perfectDodgeTimeScale;
            slowMoActive = true;
            slowMoUntil = Time.unscaledTime + config.perfectDodgeSlowMoSeconds;
        }
    }

    private void EndSlowMo()
    {
        // Restore only the slow motion set here; the pause menu uses timeScale 0.
        if (slowMoActive && Mathf.Approximately(Time.timeScale, config.perfectDodgeTimeScale)) Time.timeScale = 1f;
        slowMoActive = false;
    }

    private void OnGUI()
    {
        if (Time.unscaledTime >= textUntil) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 screen = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.3f);
        if (screen.z <= 0f) return;

        float k = 1f - (textUntil - Time.unscaledTime); // 0 → 1 over the second
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true,
            fontSize = Mathf.RoundToInt(Mathf.Lerp(34f, 28f, k))
        };
        float y = Screen.height - screen.y - 40f * k;
        var rect = new Rect(screen.x - 160f, y - 20f, 320f, 40f);
        GUI.color = new Color(0f, 0f, 0f, .7f * (1f - k));
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), "PERFECT DODGE!", style);
        GUI.color = new Color(.55f, .95f, 1f, 1f - k);
        GUI.Label(rect, "PERFECT DODGE!", style);
        GUI.color = Color.white;
    }
}
