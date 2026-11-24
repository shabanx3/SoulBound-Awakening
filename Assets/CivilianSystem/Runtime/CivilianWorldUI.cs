// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEngine;
using UnityEngine.UI;

namespace CivilianSystem
{
    /// <summary>
    /// World-space UI above the NPC head: health bar with numbers (shows when hit),
    /// speech bubble text (shows when talking).
    ///
    /// TWO MODES:
    ///  1. PREFAB (recommended for custom graphics): assign 'uiPrefab' — a canvas prefab
    ///     carrying a CivilianWorldUIRefs component with wired references. Edit visuals freely.
    ///     Generate a starter: Tools > Civilian System > Create World UI Prefab.
    ///  2. CODE-BUILT fallback: no prefab assigned -> default bar + bubble built in code.
    ///
    /// Billboards to the active camera.
    /// </summary>
    public class CivilianWorldUI : MonoBehaviour
    {
        [Tooltip("Custom canvas prefab with CivilianWorldUIRefs. Empty = default code-built UI. Usually set via CivilianDefinition.")]
        public GameObject uiPrefab;

        [Tooltip("Meters above the civilian root")]
        public float heightOffset = 2.1f;
        public float healthVisibleTime = 4f;

        Transform uiRoot;
        GameObject healthRoot;
        Image healthFill;
        bool useFillAmount;
        Text healthText;
        GameObject speechRoot;
        Text speechText;

        float fillFullWidth = 156f;
        float hideHealthAt = -1f;
        float hideSpeechAt = -1f;
        Camera cam;
        bool built;

        public void Build()
        {
            if (built) return;
            built = true;

            if (uiPrefab != null && BuildFromPrefab()) { HideAllInternal(); return; }
            BuildDefault();
            HideAllInternal();
        }

        // ---------- prefab mode ----------

        bool BuildFromPrefab()
        {
            var go = Instantiate(uiPrefab, transform);
            go.name = "WorldUI";
            go.transform.localPosition = new Vector3(0, heightOffset, 0);
            uiRoot = go.transform;

            var refs = go.GetComponentInChildren<CivilianWorldUIRefs>();
            if (refs == null)
            {
                Debug.LogWarning($"[CivilianWorldUI] Prefab '{uiPrefab.name}' has no CivilianWorldUIRefs — falling back to default UI.", this);
                Destroy(go);
                uiRoot = null;
                return false;
            }

            healthRoot = refs.healthRoot;
            healthFill = refs.healthFill;
            useFillAmount = refs.useFillAmount;
            healthText = refs.healthText;
            speechRoot = refs.speechRoot;
            speechText = refs.speechText;

            if (healthFill != null && !useFillAmount)
                fillFullWidth = healthFill.rectTransform.sizeDelta.x;

            return true;
        }

        // ---------- code-built fallback ----------

        void BuildDefault()
        {
            var cGO = new GameObject("WorldUI");
            cGO.transform.SetParent(transform, false);
            cGO.transform.localPosition = new Vector3(0, heightOffset, 0);
            var canvas = cGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 90);
            cGO.transform.localScale = Vector3.one * 0.01f;
            uiRoot = cGO.transform;

            Font font = GetFont();

            healthRoot = new GameObject("Health");
            healthRoot.transform.SetParent(cGO.transform, false);
            var hrRT = healthRoot.AddComponent<RectTransform>();
            hrRT.anchoredPosition = new Vector2(0, -30);
            hrRT.sizeDelta = new Vector2(160, 22);

            var bg = MakeImage(healthRoot.transform, "BG", new Color(0, 0, 0, 0.65f));
            bg.rectTransform.sizeDelta = new Vector2(160, 22);

            healthFill = MakeImage(healthRoot.transform, "Fill", Color.green);
            healthFill.rectTransform.pivot = new Vector2(0, 0.5f);
            healthFill.rectTransform.anchoredPosition = new Vector2(-78, 0);
            healthFill.rectTransform.sizeDelta = new Vector2(156, 18);
            fillFullWidth = 156f;
            useFillAmount = false;

            healthText = MakeText(healthRoot.transform, "Num", font, 14, Color.white);
            healthText.rectTransform.sizeDelta = new Vector2(160, 22);

            speechRoot = new GameObject("Speech");
            speechRoot.transform.SetParent(cGO.transform, false);
            var spRT = speechRoot.AddComponent<RectTransform>();
            spRT.anchoredPosition = new Vector2(0, 15);
            spRT.sizeDelta = new Vector2(190, 55);

            var bubble = MakeImage(speechRoot.transform, "Bubble", new Color(1, 1, 1, 0.92f));
            bubble.rectTransform.sizeDelta = new Vector2(190, 55);

            speechText = MakeText(speechRoot.transform, "Line", font, 15, new Color(0.1f, 0.1f, 0.1f));
            speechText.rectTransform.sizeDelta = new Vector2(180, 50);
            speechText.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        internal static Font GetFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            return f;
        }

        internal static Image MakeImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        internal static Text MakeText(Transform parent, string name, Font font, int size, Color color)
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

        // ---------- API ----------

        public void ShowHealth(float current, float max)
        {
            if (!built) Build();
            if (healthRoot == null) return;

            healthRoot.SetActive(true);
            float pct = max > 0 ? Mathf.Clamp01(current / max) : 0f;

            if (healthFill != null)
            {
                if (useFillAmount)
                    healthFill.fillAmount = pct;
                else
                    healthFill.rectTransform.sizeDelta =
                        new Vector2(fillFullWidth * pct, healthFill.rectTransform.sizeDelta.y);
                healthFill.color = Color.Lerp(Color.red, Color.green, pct);
            }

            if (healthText != null)
                healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";

            hideHealthAt = Time.time + healthVisibleTime;
        }

        public void ShowSpeech(string line, float duration)
        {
            if (!built) Build();
            if (speechRoot == null) return;

            speechRoot.SetActive(true);
            if (speechText != null) speechText.text = line;
            hideSpeechAt = Time.time + duration;
        }

        public void HideAll()
        {
            if (!built) return;
            HideAllInternal();
        }

        void HideAllInternal()
        {
            if (healthRoot != null) healthRoot.SetActive(false);
            if (speechRoot != null) speechRoot.SetActive(false);
        }

        void LateUpdate()
        {
            if (!built || uiRoot == null) return;

            if (hideHealthAt > 0 && Time.time >= hideHealthAt)
            { if (healthRoot != null) healthRoot.SetActive(false); hideHealthAt = -1; }
            if (hideSpeechAt > 0 && Time.time >= hideSpeechAt)
            { if (speechRoot != null) speechRoot.SetActive(false); hideSpeechAt = -1; }

            bool anyVisible = (healthRoot != null && healthRoot.activeSelf) ||
                              (speechRoot != null && speechRoot.activeSelf);
            if (!anyVisible) return;

            // Camera.main needs the 'MainCamera' tag — fall back to any active camera
            if (cam == null || !cam.isActiveAndEnabled)
            {
                cam = Camera.main;
                if (cam == null && Camera.allCamerasCount > 0) cam = Camera.allCameras[0];
            }
            if (cam != null)
                uiRoot.rotation = Quaternion.LookRotation(uiRoot.position - cam.transform.position);
        }
    }
}
