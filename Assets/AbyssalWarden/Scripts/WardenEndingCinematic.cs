using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace AbyssalWarden
{
    // =========================================================
    // WARDEN ENDING CINEMATIC (~28 s)
    // =========================================================
    // Plays when the Warden dies, before any end-of-game UI:
    // 1. slow-motion fall            4. the player turns and thanks
    // 2. the darkness lifts (light)     everyone for the castle
    // 3. the player speaks to the    5. crane shot + "THE CASTLE IS
    //    fallen boss                    RECLAIMED"
    // The leadership panel and campaign-complete panel wait for it
    // (RunWhenFinished). Hold SPACE to skip. Lines and effects are
    // set on AbyssalWardenBoss (Ending Cinematic).
    // =========================================================
    public class WardenEndingCinematic : MonoBehaviour
    {
        /// <summary> True from the boss's death until the ending has finished. </summary>
        public static bool Pending { get; private set; }
        static readonly List<Action> whenFinished = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Pending = false; whenFinished.Clear(); } // play mode without domain reload

        /// <summary> Runs now, or once the ending cinematic is over. </summary>
        public static void RunWhenFinished(Action action)
        {
            if (action == null) return;
            if (!Pending) action();
            else whenFinished.Add(action);
        }

        /// <summary> Called by AbyssalWardenBoss when he dies. </summary>
        public static void Play(AbyssalWardenBoss boss)
        {
            if (Pending || boss == null) return;
            Pending = true;
            WardenMusic.Set("ending", true, boss.bossMusic, boss.bossMusicVolume);
            var ending = boss.GetComponent<WardenEndingCinematic>();
            if (ending == null) ending = boss.gameObject.AddComponent<WardenEndingCinematic>();
            ending.StartCoroutine(ending.Run(boss));
        }

        public float skipHoldSeconds = .7f;
        public float blendSeconds = 1.2f;

        AbyssalWardenBoss boss;
        Transform player;
        readonly WardenCinematicRig rig = new();
        readonly List<GameObject> spawned = new();
        bool playing, skipping;
        float letterbox, titleAlpha, skipHeld, fade;
        float clock; // real seconds of cinematic, frozen while the game is paused
        string speakerLine;
        float lineAlpha;

        IEnumerator Run(AbyssalWardenBoss deadBoss)
        {
            boss = deadBoss;
            player = boss.player;
            if (player == null) { var tagged = GameObject.FindGameObjectWithTag(boss.playerTag); if (tagged != null) player = tagged.transform; }
            if (player == null) { Finish(); yield break; }

            // Let any intro / spell coroutines settle for a frame.
            yield return null;
            playing = true;

            Vector3 B = boss.transform.position;
            Vector3 toPlayer = Flat(player.position - B);
            rig.Begin(player, blendSeconds, B + toPlayer * 7f + Right(toPlayer) * 3f + Vector3.up * 1.5f, B + Vector3.up * 1.2f);

            // Shot 1 (0-4 s): the killing blow lands in slow motion.
            float t0 = clock;
            float savedScale = Time.timeScale;
            if (Mathf.Approximately(savedScale, 1f)) Time.timeScale = .3f;
            CameraShake.Shake(.8f);
            while (!skipping && Elapsed(t0) < 4f)
            {
                if (Elapsed(t0) > 1.6f && Time.timeScale < 1f && Time.timeScale > 0f) Time.timeScale = Mathf.MoveTowards(Time.timeScale, 1f, Time.unscaledDeltaTime * 1.5f);
                float k = Elapsed(t0) / 4f;
                Vector3 orbit = Quaternion.AngleAxis(Mathf.Lerp(-25f, 15f, Smooth(k)), Vector3.up) * (toPlayer * .8f + Right(toPlayer) * .6f).normalized;
                rig.Shot(B + orbit * 7f + Vector3.up * 1.4f, B + Vector3.up * 1.1f, 42f);
                yield return null;
            }
            if (Time.timeScale > 0f && Time.timeScale < 1f) Time.timeScale = 1f;

            // Off camera: bring the player beside the body, facing it.
            Vector3 standAt = Ground(B + toPlayer * 2.6f);
            if (NavMesh.SamplePosition(standAt, out NavMeshHit nav, 2f, NavMesh.AllAreas)) standAt = nav.position;
            if (rig.PlayerAnimal != null) rig.PlayerAnimal.Teleport(standAt); else player.position = standAt;
            player.rotation = Quaternion.LookRotation(-toPlayer);

            // Shot 2 (4-8.5 s): above the body, the darkness drains away and light breaks through.
            bool vortex = false, light = false;
            while (!skipping && Elapsed(t0) < 8.5f)
            {
                float k = (Elapsed(t0) - 4f) / 4.5f;
                if (!vortex && Elapsed(t0) > 4.3f) { vortex = true; Spawn(boss.teleportMarkerEffect, Ground(B) + Vector3.up * .05f, Quaternion.identity, 2.5f, 1.6f); }
                if (!light && Elapsed(t0) > 5.6f)
                {
                    light = true;
                    Spawn(boss.endingLightEffect, Ground(B), Quaternion.identity, 0f, 1f);
                    WardenMusic.Set("ending", false); // the darkness lifts: back to the normal music
                    CameraShake.Shake(.3f);
                }
                Vector3 side = Right(toPlayer);
                Vector3 from = B + side * 5f + Vector3.up * 6.5f - toPlayer * 2f;
                Vector3 to = B + side * 4f + Vector3.up * 7.5f - toPlayer * 1f;
                rig.Shot(Vector3.Lerp(from, to, Smooth(k)), B + Vector3.up * .5f, 50f);
                yield return null;
            }

            // Shot 3 (8.5-15.5 s): over the player's shoulder, words for the fallen Warden.
            string[] lines = boss.endingLines != null && boss.endingLines.Length > 0 ? boss.endingLines : DefaultLines;
            while (!skipping && Elapsed(t0) < 15.5f)
            {
                float k = (Elapsed(t0) - 8.5f) / 7f;
                Line(t0, 8.8f, 12.1f, lines, 0);
                Line(t0, 12.3f, 15.4f, lines, 1);
                Vector3 pf = Flat(B - player.position), pr = Right(pf);
                Vector3 shoulder = player.position - pf * 1.7f + pr * .75f + Vector3.up * 1.75f;
                rig.Shot(Vector3.Lerp(shoulder, shoulder + pf * .4f, Smooth(k)), B + Vector3.up * .6f, 45f);
                yield return null;
            }

            // Shot 4 (15.5-23 s): he turns to the castle and thanks everyone who fought for it.
            Vector3 away = Flat(player.position - B);
            bool turned = false, cheered = false;
            while (!skipping && Elapsed(t0) < 23f)
            {
                float k = (Elapsed(t0) - 15.5f) / 7.5f;
                if (!turned) { turned = true; player.rotation = Quaternion.LookRotation(away); }
                if (!cheered && Elapsed(t0) > 16.2f) { cheered = true; if (rig.PlayerAnimal != null) rig.PlayerAnimal.Mode_TryActivate(boss.endingGestureModeID, boss.endingGestureAbility); }
                Line(t0, 15.8f, 19.4f, lines, 2);
                Line(t0, 19.6f, 23f, lines, 3);
                Vector3 front = player.position + away * 3.4f + Right(away) * .8f + Vector3.up * 1.5f;
                rig.Shot(Vector3.Lerp(front, front - away * .6f, Smooth(k)), player.position + Vector3.up * 1.55f, 40f);
                yield return null;
            }
            speakerLine = null;

            // Shot 5 (23-28 s): crane up over the reclaimed castle.
            bool cue = false;
            while (!skipping && Elapsed(t0) < 28f)
            {
                float k = (Elapsed(t0) - 23f) / 5f;
                if (!cue && Elapsed(t0) > 23.4f) { cue = true; if (boss.endingVictorySound != null) AudioSource.PlayClipAtPoint(boss.endingVictorySound, player.position); }
                titleAlpha = Mathf.Clamp01((Elapsed(t0) - 23.6f) / .9f);
                fade = Mathf.Clamp01((Elapsed(t0) - 27f) / 1f) * .6f;
                Vector3 from = player.position - away * 4f + Vector3.up * 3f;
                Vector3 to = player.position - away * 16f + Right(away) * 6f + Vector3.up * 14f;
                rig.Shot(Vector3.Lerp(from, to, Smooth(k)), player.position + Vector3.up * 1.2f, Mathf.Lerp(45f, 55f, k));
                yield return null;
            }

            Finish();
        }

        void Line(float t0, float from, float to, string[] lines, int index)
        {
            float e = Elapsed(t0);
            if (e < from || e > to || index >= lines.Length) return;
            speakerLine = lines[index];
            lineAlpha = Mathf.Clamp01(Mathf.Min((e - from) / .35f, (to - e) / .35f));
        }

        static readonly string[] DefaultLines =
        {
            "It's over, Warden. Your darkness ends here.",
            "These halls were never yours to keep.",
            "To everyone who stood with me... thank you. I could not have done this alone.",
            "The castle is ours again. Our home is free.",
        };

        void Finish()
        {
            if (Time.timeScale > 0f && Time.timeScale < 1f) Time.timeScale = 1f;
            foreach (var go in spawned) if (go != null) WardenVFX.StopAndDestroy(go, 1.5f);
            spawned.Clear();
            if (playing) rig.End(this, restoreControl: false); // the end-of-game UI takes over
            playing = false;
            Pending = false;
            WardenMusic.Set("ending", false);

            var actions = new List<Action>(whenFinished);
            whenFinished.Clear();
            foreach (var action in actions)
            {
                try { action(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        void OnDisable() { if (Pending && playing) Finish(); }

        void LateUpdate()
        {
            letterbox = Mathf.MoveTowards(letterbox, playing ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            if (!playing) return;
            rig.KeepPlayerLocked();

            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.isPressed && Time.timeScale > 0f) skipHeld += Time.unscaledDeltaTime; else skipHeld = 0f;
            if (skipHeld >= skipHoldSeconds) skipping = true;
        }

        void OnGUI()
        {
            if (fade > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, fade);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            WardenCinematicRig.DrawLetterbox(letterbox);
            if (!playing) return;
            WardenCinematicRig.DrawSubtitle(boss.endingSpeakerName, speakerLine, lineAlpha);
            WardenCinematicRig.DrawTitle(boss.endingTitle, boss.endingSubtitle, titleAlpha, new Color(1f, .86f, .45f));
            WardenCinematicRig.DrawSkipHint(skipHeld, skipHoldSeconds);
        }

        // ---------------------------------------------------------------- helpers
        float Elapsed(float t0) => clock - t0;

        void Update()
        {
            if (playing && Time.timeScale > 0f) clock += Time.unscaledDeltaTime;
        }
        static float Smooth(float k) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > .0001f ? v.normalized : Vector3.forward; }
        static Vector3 Right(Vector3 forward) => Vector3.Cross(Vector3.up, forward);

        Vector3 Ground(Vector3 p)
        {
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 20f, ~(1 << 20), QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(boss.transform) && (player == null || !hit.transform.IsChildOf(player))) return hit.point;
            return p;
        }

        void Spawn(GameObject prefab, Vector3 pos, Quaternion rot, float life, float scale)
        {
            var go = WardenVFX.Spawn(prefab, pos, rot, life, null, scale);
            if (go != null && life <= 0f) spawned.Add(go);
        }
    }
}
