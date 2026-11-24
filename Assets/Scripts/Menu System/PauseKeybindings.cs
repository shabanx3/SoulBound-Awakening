using System.Reflection;
using TMPro;
using UnityEngine;

// =========================================================
// PAUSE KEYBINDINGS
// =========================================================
// Rebuilds the KEYBINDINGS card in the pause panel as a compact
// list of movement and combat controls. Spell keys are read
// from Resources/PlayerSpellConfig, so the list stays correct
// if they are rebound. Uses the card's own font and style.
// Installed on scene load by PlayerCombatBootstrap.
// =========================================================

public static class PauseKeybindings
{
    private const string CardTitle = "KEYBINDINGS";
    private const string ListName = "Keybindings List";

    private static readonly Color HeaderColor = new(1f, .78f, .35f);
    private static readonly Color KeyColor = new(1f, .93f, .75f);

    public static void Install(PlayerSpellConfig config)
    {
        var menu = Object.FindFirstObjectByType<MenuManager>(FindObjectsInactive.Include);
        if (menu == null) return;

        FieldInfo field = typeof(MenuManager).GetField("pausePanel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var pausePanel = field != null ? field.GetValue(menu) as GameObject : null;
        if (pausePanel == null) return;

        TextMeshProUGUI title = null;
        foreach (var t in pausePanel.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (t.text.Trim() == CardTitle) { title = t; break; }
        if (title == null) return;

        RectTransform card = title.transform.parent as RectTransform;
        if (card == null || card.Find(ListName) != null) return;

        // Hide the old widely spaced rows; the title stays.
        foreach (Transform child in card)
            if (child != title.transform) child.gameObject.SetActive(false);

        string[,] rows =
        {
            { "#", "MOVEMENT" },
            { "Move", "W / A / S / D" },
            { "Sprint", "Left Shift" },
            { "Jump", "Space" },
            { "Dodge Roll", "Q" },
            { "Crouch", "C" },
            { "#", "COMBAT" },
            { "Attack", "Mouse Left" },
            { "Draw / Store Weapon", "4  /  Mouse Scroll" },
            { "Lock Target", "Middle Mouse" },
            { config.fireBolt.name, KeyName(config.fireBolt) },
            { config.thunder.name, KeyName(config.thunder) },
            { config.frostNova.name, KeyName(config.frostNova) },
            { config.shockwave.name, KeyName(config.shockwave) },
            { "Heal Flask", "R" },
        };

        var list = new GameObject(ListName, typeof(RectTransform)).GetComponent<RectTransform>();
        list.SetParent(card, false);
        list.anchorMin = Vector2.zero;
        list.anchorMax = Vector2.one;
        list.offsetMin = new Vector2(70f, 30f);
        list.offsetMax = new Vector2(-70f, -75f); // below the title

        int count = rows.GetLength(0);
        float height = card.rect.height > 1f ? card.rect.height - 105f : 700f;
        float rowHeight = Mathf.Min(48f, height / count);
        float fontSize = Mathf.Clamp(rowHeight * .66f, 18f, 32f);

        for (int i = 0; i < count; i++)
        {
            float top = -i * rowHeight;
            if (rows[i, 0] == "#")
            {
                var header = Label(list, title, rows[i, 1], top, rowHeight, fontSize, TextAlignmentOptions.MidlineLeft, 0f, 1f);
                header.color = HeaderColor;
                header.fontStyle = FontStyles.Bold;
                continue;
            }
            Label(list, title, rows[i, 0], top, rowHeight, fontSize, TextAlignmentOptions.MidlineLeft, .04f, .6f);
            var key = Label(list, title, rows[i, 1], top, rowHeight, fontSize, TextAlignmentOptions.MidlineRight, .6f, 1f);
            key.color = KeyColor;
        }
    }

    private static string KeyName(PlayerSpellConfig.Spell spell) => spell.key.ToString();

    private static TextMeshProUGUI Label(RectTransform parent, TextMeshProUGUI style, string text, float top, float height,
        float size, TextAlignmentOptions align, float xMin, float xMax)
    {
        var go = new GameObject(text, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(xMin, 1f);
        rect.anchorMax = new Vector2(xMax, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, top);
        rect.sizeDelta = new Vector2(0f, height);

        var label = go.AddComponent<TextMeshProUGUI>();
        label.font = style.font;
        label.fontSharedMaterial = style.fontSharedMaterial;
        label.color = style.color;
        label.fontSize = size;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.alignment = align;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }
}
