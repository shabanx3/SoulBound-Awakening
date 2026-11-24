using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AbyssalWarden
{
    // =========================================================
    // WARDEN INTRO CINEMATIC (20 s)
    // =========================================================
    // Plays once, the first time the player engages the Warden.
    // Five shots while the boss roars, gathers dark power and
    // bursts into flame; letterbox bars and a title card frame it.
    // Player and boss are frozen and protected (WardenCinematicRig).
    // Hold SPACE to skip. Added by AbyssalWardenBoss (playIntroCinematic).
    // =========================================================
    public class WardenIntroCinematic : MonoBehaviour
    {
        public static bool IsPlaying { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => IsPlaying = false; // play mode without domain reload

        public float duration = 20f;
        public string title = "THE ABYSSAL WARDEN";
        public string subtitle = "Keeper of the Abyss";
        public float skipHoldSeconds = .7f;
        public float blendSeconds = 1.4f;

        AbyssalWardenBoss boss;
        Transform player;
        readonly WardenCinematicRig rig = new();
        readonly List<GameObject> spawned = new();
        bool played, skipping;
        float letterbox, titleAlpha, skipHeld;

        void Awake() => boss = GetComponent<AbyssalWardenBoss>();

        void Update()
        {
            if (played || boss == null || boss.IsDead || !boss.IsEngaged) return;
            played = true;
            boss.CinematicHold = true;           // freeze the boss on the very frame he engages
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            player = boss.player;
            if (player == null) { var tagged = GameObject.FindGameObjectWithTag(boss.playerTag); if (tagged != null) player = tagged.transform; }
            if (player == null) { boss.CinematicHold = false; yield break; }

            IsPlaying = true;
            Vector3 look = boss.transform.position - player.position; look.y = 0;
            if (look.sqrMagnitude > .01f) player.rotation = Quaternion.LookRotation(look);
            rig.Begin(player, blendSeconds, player.position - PlayerForward() * 5f + Vector3.up * 3f, Chest());

            float t0 = Time.time;
            Vector3 B = boss.transform.position;

            // Shot 1 (0-5 s): establishing orbit; dark energy gathers under him.
            boss.CinematicPlay("Idle");
            Spawn(boss.teleportMarkerEffect, Ground(B) + Vector3.up * .05f, Quaternion.identity, 2f);
            bool brokeIdle = false;
            while (!skipping && Elapsed(t0) < 5f)
            {
                float k = Elapsed(t0) / 5f;
                if (!brokeIdle && Elapsed(t0) > 1.5f) { brokeIdle = true; boss.CinematicPlay("IdleBreak"); }
                Vector3 orbit = Quaternion.AngleAxis(Mathf.Lerp(150f, 215f, Smooth(k)), Vector3.up) * Forward();
                rig.Shot(B + orbit * 13f + Vector3.up * 4.5f, Chest(), 45f);
                yield return null;
            }

            // Shot 2 (5-9 s): low angle, he roars and erupts in flame.
            bool roared = false, erupted = false;
            while (!skipping && Elapsed(t0) < 9f)
            {
                float k = (Elapsed(t0) - 5f) / 4f;
                if (!roared && Elapsed(t0) > 5.4f) { roared = true; boss.CinematicPlay("Taunt", .15f); boss.CinematicVoice("VocalizationTaunt"); }
                if (!erupted && Elapsed(t0) > 6.3f)
                {
                    erupted = true;
                    Spawn(boss.roarEffect, Ground(B), Quaternion.identity, 3.5f);
                    Spawn(boss.phaseTwoAura, B, boss.transform.rotation, 0f, boss.transform);
                    CameraShake.Shake(.9f);
                }
                Vector3 from = B + Forward() * 6f + Right() * 2f + Vector3.up * .4f;
                Vector3 to = B + Forward() * 4.5f + Right() * 1.5f + Vector3.up * .8f;
                rig.Shot(Vector3.Lerp(from, to, Smooth(k)), B + Vector3.up * 2.4f, 50f);
                yield return null;
            }

            // Shot 3 (9-13 s): from behind the player as he opens a summoning circle.
            bool cast = false, looping = false, burst = false;
            while (!skipping && Elapsed(t0) < 13f)
            {
                float k = (Elapsed(t0) - 9f) / 4f;
                if (!cast && Elapsed(t0) > 9.3f)
                {
                    cast = true;
                    boss.CinematicPlay("CastStart");
                    Spawn(boss.summonCircleEffect, Ground(B) + Vector3.up * .05f, Quaternion.identity, 4f, null, 2f);
                }
                if (cast && !looping && Elapsed(t0) > 9.9f) { looping = true; boss.CinematicPlay("CastLoop"); }
                if (!burst && Elapsed(t0) > 12.4f)
                {
                    burst = true;
                    boss.CinematicPlay("CastEnd");
                    Spawn(boss.slamImpactEffect, Ground(B), Quaternion.identity, 4f);
                    CameraShake.Shake(.6f);
                }
                Vector3 pf = PlayerForward(), pr = Vector3.Cross(Vector3.up, pf);
                Vector3 behind = player.position - pf * 2.6f + pr * .9f + Vector3.up * 1.9f;
                rig.Shot(Vector3.Lerp(behind, behind + pf * .8f, Smooth(k)), Chest(), 48f);
                yield return null;
            }

            // Shot 4 (13-17.5 s): close on his face; title card.
            bool finalRoar = false;
            while (!skipping && Elapsed(t0) < 17.5f)
            {
                float k = (Elapsed(t0) - 13f) / 4.5f;
                if (!finalRoar && Elapsed(t0) > 14f) { finalRoar = true; boss.CinematicPlay("Taunt", .2f); boss.CinematicVoice("Vocalization"); }
                titleAlpha = Mathf.Clamp01((Elapsed(t0) - 13.4f) / .8f);
                Vector3 face = boss.Head != null ? boss.Head.position : B + Vector3.up * 2.4f;
                Vector3 from = B + Forward() * 2.8f + Right() * .7f + Vector3.up * 2.2f;
                Vector3 to = B + Forward() * 2.1f + Right() * .5f + Vector3.up * 2.3f;
                rig.Shot(Vector3.Lerp(from, to, Smooth(k)), face, 35f);
                yield return null;
            }

            // Shot 5 (17.5-20 s): pull back to the player, ready to fight.
            while (!skipping && Elapsed(t0) < duration)
            {
                float k = Mathf.Clamp01((Elapsed(t0) - 17.5f) / (duration - 17.5f));
                titleAlpha = 1f - Mathf.Clamp01(k * 2f);
                Vector3 from = B + Forward() * 9f + Vector3.up * 5f;
                Vector3 to = player.position - PlayerForward() * 5f + Vector3.up * 3f;
                rig.Shot(Vector3.Lerp(from, to, Smooth(k)), Chest(), Mathf.Lerp(45f, 55f, k));
                yield return null;
            }

            if (skipping) yield return WardenCinematicRig.WaitForSkipKeyRelease();
            Finish();
        }

        void Finish()
        {
            if (!IsPlaying) return;
            IsPlaying = false;
            titleAlpha = 0f;
            foreach (var go in spawned) if (go != null) WardenVFX.StopAndDestroy(go, 1f);
            spawned.Clear();
            rig.End(this);
            boss.CinematicEnd();
            boss.CinematicHold = false;
        }

        void OnDisable() { if (IsPlaying) Finish(); }

        void LateUpdate()
        {
            letterbox = Mathf.MoveTowards(letterbox, IsPlaying ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            if (!IsPlaying) return;
            boss.CinematicFace(player.position, .5f);
            rig.KeepPlayerLocked();

            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.isPressed && Time.timeScale > 0f) skipHeld += Time.unscaledDeltaTime; else skipHeld = 0f;
            if (skipHeld >= skipHoldSeconds) skipping = true;
        }

        void OnGUI()
        {
            WardenCinematicRig.DrawLetterbox(letterbox);
            if (!IsPlaying) return;
            WardenCinematicRig.DrawTitle(title, subtitle, titleAlpha, new Color(1f, .45f, .2f));
            WardenCinematicRig.DrawSkipHint(skipHeld, skipHoldSeconds);
        }

        // ---------------------------------------------------------------- helpers
        float Elapsed(float t0) => Time.time - t0;
        static float Smooth(float k) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
        Vector3 Chest() => boss.transform.position + Vector3.up * 1.8f;
        Vector3 Forward() { Vector3 f = player.position - boss.transform.position; f.y = 0; return f.sqrMagnitude > .01f ? f.normalized : boss.transform.forward; }
        Vector3 Right() => Vector3.Cross(Vector3.up, Forward());
        Vector3 PlayerForward() { Vector3 f = boss.transform.position - player.position; f.y = 0; return f.sqrMagnitude > .01f ? f.normalized : player.forward; }

        Vector3 Ground(Vector3 p)
        {
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 20f, ~(1 << 20), QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(boss.transform)) return hit.point;
            return p;
        }

        void Spawn(GameObject prefab, Vector3 pos, Quaternion rot, float life, Transform parent = null, float scale = 1f)
        {
            var go = WardenVFX.Spawn(prefab, pos, rot, life, parent, scale);
            if (go != null && life <= 0f) spawned.Add(go);
        }
    }
}
