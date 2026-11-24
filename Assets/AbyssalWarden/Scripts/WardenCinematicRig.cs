using System.Reflection;
using EmeraldAI;
using MalbersAnimations.Controller;
using Unity.Cinemachine;
using UnityEngine;

namespace AbyssalWarden
{
    // =========================================================
    // WARDEN CINEMATIC RIG
    // =========================================================
    // Shared by the boss intro and ending cinematics:
    // - locks the player (no input / movement / damage)
    // - hides the gameplay HUD
    // - runs a top-priority Cinemachine camera moved by hand
    // - draws letterbox bars, titles, subtitles and the skip hint
    // Everything is restored by End().
    // =========================================================
    public class WardenCinematicRig
    {
        /// <summary> True while any boss cinematic is on screen (HUDs hide themselves). </summary>
        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active = false; // play mode without domain reload

        public CinemachineCamera Camera { get; private set; }
        public Transform Player { get; private set; }
        public MAnimal PlayerAnimal { get; private set; }

        CinemachineBrain brain;
        CinemachineBlendDefinition savedBlend;
        EmeraldGeneralTargetBridge bridge;
        bool bridgeWasImmortal;
        GameObject gameplayPanel;
        bool panelWasActive;
        float blendSeconds;

        public void Begin(Transform player, float blend, Vector3 cameraStart, Vector3 cameraLookAt)
        {
            Active = true;
            Player = player;
            blendSeconds = blend;

            PlayerAnimal = player.GetComponent<MAnimal>();
            if (MenuManager.Instance != null) MenuManager.Instance.TogglePlayerInputs(false);
            if (PlayerAnimal != null) { PlayerAnimal.StopMoving(); PlayerAnimal.LockMovement = true; PlayerAnimal.LockInput = true; }
            bridge = player.GetComponent<EmeraldGeneralTargetBridge>();
            if (bridge != null) { bridgeWasImmortal = bridge.Immortal; bridge.Immortal = true; }

            if (MenuManager.Instance != null)
            {
                FieldInfo f = typeof(MenuManager).GetField("gameplayPanel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                gameplayPanel = f != null ? f.GetValue(MenuManager.Instance) as GameObject : null;
                if (gameplayPanel != null) { panelWasActive = gameplayPanel.activeSelf; gameplayPanel.SetActive(false); }
            }

            brain = Object.FindFirstObjectByType<CinemachineBrain>();
            if (brain != null) { savedBlend = brain.DefaultBlend; brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, blend); }
            var go = new GameObject("Boss Cinematic Camera");
            Camera = go.AddComponent<CinemachineCamera>();
            Camera.Priority = 10000;
            Camera.Lens = LensSettings.Default;
            Camera.Lens.FieldOfView = 45f;
            Vector3 dir = cameraLookAt - cameraStart;
            go.transform.SetPositionAndRotation(cameraStart, Quaternion.LookRotation(dir.sqrMagnitude > .01f ? dir : Vector3.forward));
        }

        /// <summary> Restores the player, HUD and camera. Pass restoreControl=false when a menu takes over next. </summary>
        public void End(MonoBehaviour host, bool restoreControl = true)
        {
            if (!Active) return;
            Active = false;

            if (Camera != null) Object.Destroy(Camera.gameObject, .05f); // the brain blends back to gameplay
            if (brain != null) host.StartCoroutine(RestoreBlend());

            if (PlayerAnimal != null) { PlayerAnimal.LockMovement = false; PlayerAnimal.LockInput = false; }
            if (bridge != null) bridge.Immortal = bridgeWasImmortal;
            if (restoreControl)
            {
                if (MenuManager.Instance != null && MenuManager.Instance.gameState == GameState.Gameplay) MenuManager.Instance.TogglePlayerInputs(true);
                if (gameplayPanel != null) gameplayPanel.SetActive(panelWasActive);
            }
        }

        System.Collections.IEnumerator RestoreBlend()
        {
            yield return new WaitForSecondsRealtime(blendSeconds + .1f);
            if (brain != null) brain.DefaultBlend = savedBlend;
        }

        /// <summary> SPACE skips cinematics and is also jump: hold control back until it is released, so a skip doesn't make the player jump. </summary>
        public static System.Collections.IEnumerator WaitForSkipKeyRelease()
        {
            for (float t = 0f; t < 1.5f; t += Time.unscaledDeltaTime)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard == null || !keyboard.spaceKey.isPressed) yield break;
                yield return null;
            }
        }

        /// <summary> Resuming from the pause menu re-enables player inputs; call every frame to keep them off. </summary>
        public void KeepPlayerLocked()
        {
            if (MenuManager.Instance != null && MenuManager.Instance.gameState == GameState.Gameplay) MenuManager.Instance.TogglePlayerInputs(false);
        }

        /// <summary> Places the camera with a slight hand-held drift. </summary>
        public void Shot(Vector3 position, Vector3 lookAt, float fov)
        {
            if (Camera == null) return;
            float t = Time.unscaledTime * .6f;
            Vector3 drift = new Vector3(Mathf.PerlinNoise(t, 0f) - .5f, Mathf.PerlinNoise(0f, t) - .5f, 0f) * .12f;
            Vector3 dir = lookAt - position;
            Camera.transform.SetPositionAndRotation(position + Camera.transform.rotation * drift, Quaternion.LookRotation(dir.sqrMagnitude > .01f ? dir : Vector3.forward));
            Camera.Lens.FieldOfView = fov;
        }

        // ---------------------------------------------------------------- drawing (call from OnGUI)
        public static void DrawLetterbox(float k)
        {
            if (k <= 0f) return;
            float bar = Screen.height * .12f * Mathf.SmoothStep(0f, 1f, k);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, bar), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - bar, Screen.width, bar), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        public static void DrawTitle(string title, string subtitle, float alpha, Color titleColor)
        {
            if (alpha <= 0f) return;
            var big = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(Screen.height / 14, 28, 80), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var small = new GUIStyle(big) { fontSize = Mathf.Clamp(Screen.height / 34, 14, 34), fontStyle = FontStyle.Italic };
            float y = Screen.height * .58f;
            GUI.color = new Color(0f, 0f, 0f, .75f * alpha);
            GUI.Label(new Rect(3, y + 3, Screen.width, 90), title, big);
            GUI.color = new Color(titleColor.r, titleColor.g, titleColor.b, alpha);
            GUI.Label(new Rect(0, y, Screen.width, 90), title, big);
            if (!string.IsNullOrEmpty(subtitle))
            {
                GUI.color = new Color(0f, 0f, 0f, .85f * alpha);
                GUI.Label(new Rect(2, y + 80, Screen.width, 40), subtitle, small);
                GUI.color = new Color(.95f, .88f, .78f, alpha);
                GUI.Label(new Rect(0, y + 78, Screen.width, 40), subtitle, small);
            }
            GUI.color = Color.white;
        }

        /// <summary> Spoken line in the lower letterbox bar: "Speaker: line". </summary>
        public static void DrawSubtitle(string speaker, string line, float alpha)
        {
            if (alpha <= 0f || string.IsNullOrEmpty(line)) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Clamp(Screen.height / 36, 15, 30), alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
            float h = Screen.height * .12f;
            var rect = new Rect(Screen.width * .1f, Screen.height - h, Screen.width * .8f, h);
            string text = $"<color=#FFB25C><b>{speaker}:</b></color> {line}";
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), $"<b>{speaker}:</b> {line}", style);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
        }

        public static void DrawSkipHint(float held, float needed)
        {
            var hint = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleRight };
            GUI.color = new Color(1f, 1f, 1f, .75f);
            string progress = held > 0f ? $"  {Mathf.RoundToInt(held / needed * 100f)}%" : "";
            GUI.Label(new Rect(Screen.width - 330, Screen.height * .06f - 12, 310, 24), "Hold [SPACE] to skip" + progress, hint);
            GUI.color = Color.white;
        }
    }
}
