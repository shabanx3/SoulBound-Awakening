// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CivilianSystem.EditorTools
{
    /// <summary>
    /// Generates an editable world-UI canvas prefab (health bar + speech bubble)
    /// with CivilianWorldUIRefs pre-wired. Reskin it freely, then assign it to
    /// CivilianDefinition.uiPrefab.
    /// Menu: Tools > Civilian System > Create World UI Prefab
    /// </summary>
    public static class WorldUIPrefabCreator
    {
        [MenuItem("Tools/Civilian System/Create World UI Prefab")]
        public static void Create()
        {
            string dir = "Assets/Civilians";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Civilians");

            var cGO = new GameObject("CivilianWorldUI");
            var canvas = cGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            cGO.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 90);
            cGO.transform.localScale = Vector3.one * 0.01f;

            Font font = GetFont();

            // ---- health ----
            var healthRoot = new GameObject("Health");
            healthRoot.transform.SetParent(cGO.transform, false);
            var hrRT = healthRoot.AddComponent<RectTransform>();
            hrRT.anchoredPosition = new Vector2(0, -30);
            hrRT.sizeDelta = new Vector2(160, 22);

            var bg = MakeImage(healthRoot.transform, "BG", new Color(0, 0, 0, 0.65f));
            bg.rectTransform.sizeDelta = new Vector2(160, 22);

            var fill = MakeImage(healthRoot.transform, "Fill", Color.green);
            fill.rectTransform.pivot = new Vector2(0, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(-78, 0);
            fill.rectTransform.sizeDelta = new Vector2(156, 18);

            var num = MakeText(healthRoot.transform, "Num", font, 14, Color.white);
            num.rectTransform.sizeDelta = new Vector2(160, 22);
            num.text = "100 / 100";

            // ---- speech ----
            var speechRoot = new GameObject("Speech");
            speechRoot.transform.SetParent(cGO.transform, false);
            var spRT = speechRoot.AddComponent<RectTransform>();
            spRT.anchoredPosition = new Vector2(0, 15);
            spRT.sizeDelta = new Vector2(190, 55);

            var bubble = MakeImage(speechRoot.transform, "Bubble", new Color(1, 1, 1, 0.92f));
            bubble.rectTransform.sizeDelta = new Vector2(190, 55);

            var line = MakeText(speechRoot.transform, "Line", font, 15, new Color(0.1f, 0.1f, 0.1f));
            line.rectTransform.sizeDelta = new Vector2(180, 50);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.text = "Hello there, traveler.";

            // ---- refs wiring ----
            var refs = cGO.AddComponent<CivilianWorldUIRefs>();
            refs.healthRoot = healthRoot;
            refs.healthFill = fill;
            refs.useFillAmount = false;
            refs.healthText = num;
            refs.speechRoot = speechRoot;
            refs.speechText = line;

            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/CivilianWorldUI.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(cGO, path);
            Object.DestroyImmediate(cGO);

            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            EditorUtility.DisplayDialog("World UI Prefab",
                $"Created:\n{path}\n\nReskin freely (sprites, colors, fonts, layout).\n" +
                "Keep CivilianWorldUIRefs wired, then assign the prefab to\n" +
                "CivilianDefinition > World UI > Ui Prefab.", "OK");
        }

        // ---- self-contained helpers (no dependency on runtime class internals) ----

        static Font GetFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            return f;
        }

        static Image MakeImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static Text MakeText(Transform parent, string name, Font font, int size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.fontStyle = FontStyle.Bold;
            return t;
        }
    }
}
