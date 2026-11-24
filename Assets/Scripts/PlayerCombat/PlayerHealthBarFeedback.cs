using System.Collections;
using MalbersAnimations;
using TMPro;
using UnityEngine;

// =========================================================
// PLAYER HEALTH BAR FEEDBACK
// =========================================================
// Watches the player's Malbers Health stat (Stats[0]) and
// animates the "Health Bar" UI whenever it changes:
// - hurt: the bar shakes, and a red "-25" floats off it
// - heal: the bar pulses, and a green "+25" floats off it
// Every source is covered (boss, enemies, heal orbs, flask).
// =========================================================

public class PlayerHealthBarFeedback : MonoBehaviour
{
    private const string BarName = "Health Bar";

    private static readonly Color HurtColor = new(1f, .32f, .25f);
    private static readonly Color HealColor = new(.45f, 1f, .5f);

    private Stats stats;
    private RectTransform bar;
    private TMP_FontAsset font;
    private float lastValue = float.NaN;
    private Vector2 barHome;
    private Vector3 barScale;
    private Coroutine barAnim;
    private float pendingDelta, pendingSince;

    private void Awake()
    {
        stats = GetComponent<Stats>();
    }

    private void Update()
    {
        if (stats == null || stats.stats == null || stats.stats.Count == 0) return;
        if (bar == null && !FindBar()) return;

        float value = stats.stats[0].Value;
        if (float.IsNaN(lastValue)) { lastValue = value; return; }

        float delta = value - lastValue;
        lastValue = value;
        if (Mathf.Abs(delta) >= .5f)
        {
            // Hits that land in the same instant (multi-hit, damage + regen) show as one number.
            if (pendingDelta != 0f && Mathf.Sign(pendingDelta) != Mathf.Sign(delta)) Flush();
            if (pendingDelta == 0f) pendingSince = Time.unscaledTime;
            pendingDelta += delta;
        }

        if (pendingDelta != 0f && Time.unscaledTime - pendingSince >= .05f) Flush();
    }

    private void Flush()
    {
        float delta = pendingDelta;
        pendingDelta = 0f;
        // Health can change while the HUD is hidden (heal orbs during a cinematic); TMP can't initialise there.
        if (Mathf.Abs(delta) < .5f || bar == null || !bar.gameObject.activeInHierarchy) return;

        bool hurt = delta < 0f;
        SpawnNumber(hurt ? $"-{Mathf.RoundToInt(-delta)}" : $"+{Mathf.RoundToInt(delta)}", hurt ? HurtColor : HealColor);
        if (barAnim != null) { StopCoroutine(barAnim); ResetBar(); }
        barAnim = StartCoroutine(hurt ? Shake(Mathf.Clamp(-delta / 10f, 4f, 14f)) : Pulse());
    }

    private bool FindBar()
    {
        foreach (RectTransform rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rt.name != BarName || rt.GetComponentInParent<Canvas>(true) == null) continue;
            bar = rt;
            barHome = bar.anchoredPosition;
            barScale = bar.localScale;
            var canvas = bar.GetComponentInParent<Canvas>(true).rootCanvas;
            var sample = canvas.GetComponentInChildren<TMP_Text>(true);
            font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
            return true;
        }
        return false;
    }

    private void SpawnNumber(string text, Color color)
    {
        var go = new GameObject("Health Change", typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(bar.parent, false);
        // Start just past the right end of the bar.
        rect.anchorMin = bar.anchorMin; rect.anchorMax = bar.anchorMax; rect.pivot = new Vector2(0f, .5f);
        rect.sizeDelta = new Vector2(160f, 50f);
        rect.anchoredPosition = barHome + new Vector2(bar.rect.width * (1f - bar.pivot.x) + 14f, 0f);

        var label = go.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = 34f;
        label.fontStyle = FontStyles.Bold;
        label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        label.text = text;
        label.outlineWidth = .2f;
        label.outlineColor = new Color32(0, 0, 0, 200);

        StartCoroutine(Float(rect, label));
    }

    private static IEnumerator Float(RectTransform rect, TextMeshProUGUI label)
    {
        Vector2 start = rect.anchoredPosition;
        Color c = label.color;
        for (float t = 0f; t < 1.1f; t += Time.unscaledDeltaTime)
        {
            float k = t / 1.1f;
            rect.anchoredPosition = start + new Vector2(0f, 45f * k);
            rect.localScale = Vector3.one * (k < .12f ? Mathf.Lerp(.6f, 1.25f, k / .12f) : Mathf.Lerp(1.25f, 1f, (k - .12f) / .3f));
            label.color = new Color(c.r, c.g, c.b, k < .6f ? 1f : 1f - (k - .6f) / .4f);
            yield return null;
        }
        Destroy(rect.gameObject);
    }

    private IEnumerator Shake(float strength)
    {
        for (float t = 0f; t < .35f; t += Time.unscaledDeltaTime)
        {
            float k = 1f - t / .35f;
            bar.anchoredPosition = barHome + Random.insideUnitCircle * strength * k;
            bar.localScale = barScale * (1f + .05f * k);
            yield return null;
        }
        ResetBar();
        barAnim = null;
    }

    private IEnumerator Pulse()
    {
        for (float t = 0f; t < .45f; t += Time.unscaledDeltaTime)
        {
            bar.localScale = barScale * (1f + .08f * Mathf.Sin(t / .45f * Mathf.PI));
            yield return null;
        }
        ResetBar();
        barAnim = null;
    }

    private void ResetBar()
    {
        if (bar == null) return;
        bar.anchoredPosition = barHome;
        bar.localScale = barScale;
    }

    private void OnDisable() => ResetBar();
}
