using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace AbyssalWarden
{
    // =========================================================
    // WARDEN PROLOGUE CINEMATIC (New Game, ~52 s)
    // =========================================================
    // The story before the game starts, staged with in-game actors:
    //  1  The castle (home town) at peace
    //  2  The sky darkens; a portal to the dark realm tears open
    //  3  The Abyssal Warden walks out into the castle
    //  4  He raises a skeleton army that takes the town
    //  5  The player fights him and is hurled out of the castle
    //  6  Black: cast out of home
    //  7  The player wakes in the village by the teleporter
    //  8  The vow to take the home town back
    // The Warden on screen is a cosmetic copy of the real boss (the
    // real one stays inactive and untouched); the army is spawned
    // for the scene and removed. Started by MenuManager on New Game;
    // gameplay starts when it ends. Hold SPACE to skip.
    // =========================================================
    public class WardenPrologueCinematic : MonoBehaviour
    {
        public static bool Playing { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Playing = false; // play mode without domain reload

        const string Narrator = "Narrator", Hero = "Knight", Boss = "Abyssal Warden";
        const float SkipHold = .7f;

        /// <summary> Starts the prologue; onFinished runs when it ends (or immediately if it cannot play). </summary>
        public static bool TryPlay(Action onFinished)
        {
            AbyssalWardenBoss real = null;
            foreach (var b in FindObjectsByType<AbyssalWardenBoss>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { real = b; break; }
            var receiver = FindFirstObjectByType<PlayerWardenDamageReceiver>(FindObjectsInactive.Include);
            if (real == null || receiver == null || Playing) return false;

            var go = new GameObject("Prologue Director");
            var director = go.AddComponent<WardenPrologueCinematic>();
            director.StartCoroutine(director.Run(real, receiver.transform, onFinished));
            return true;
        }

        AbyssalWardenBoss realBoss;
        Transform player;
        Action onFinished;
        readonly WardenCinematicRig rig = new();
        readonly List<GameObject> props = new();
        GameObject puppet;
        Vector3 villageSpot, toGate;
        Animator puppetAnim;
        WardenDemonFX puppetFX;

        Light sun;
        float sunIntensity, ambientIntensity;
        bool darkened;

        float clock, letterbox, titleAlpha, fade, lineAlpha, skipHeld;
        string speaker, line, title, subtitle;
        bool playing, skipping;

        // ---------------------------------------------------------------- sequence
        IEnumerator Run(AbyssalWardenBoss boss, Transform playerRoot, Action finished)
        {
            realBoss = boss;
            player = playerRoot;
            onFinished = finished;
            Playing = true;
            Time.timeScale = 1f;

            // Stage: the castle around the Warden's post, the village where the player starts.
            villageSpot = player.position;
            Transform gate = FindByName("Teleport_Gate");
            toGate = gate != null ? Flat(gate.position - villageSpot) : Flat(player.forward);
            Vector3 W = Ground(boss.transform.position);
            Vector3 arrival = ArrivalPoint();
            Vector3 dir = Flat(arrival - W);                       // from the Warden's post toward the castle entrance
            Vector3 heroSpot = Snap(W + dir * 7f);
            Vector3 portalSpot = Ground(W - dir * 14f);

            fade = 1f;                                             // start black while the stage is set
            MovePlayer(heroSpot, Flat(W - heroSpot));
            rig.Begin(player, 1f, W + dir * 26f + Vector3.up * 14f, W);
            playing = true;
            yield return null;

            // 1 (0-6 s): the castle at peace.
            float t0 = clock;
            while (!skipping && E(t0) < 6f)
            {
                fade = 1f - Mathf.Clamp01(E(t0) / 1.2f);
                Say(t0, .8f, 5.8f, Narrator, "The castle. My home town. Peaceful, for as long as I can remember.");
                float k = E(t0) / 6f;
                Vector3 orbit = Quaternion.AngleAxis(Mathf.Lerp(-20f, 20f, k), Vector3.up) * dir;
                rig.Shot(W + orbit * Mathf.Lerp(30f, 24f, k) + Vector3.up * Mathf.Lerp(16f, 12f, k), W + Vector3.up * 2f, 50f);
                yield return null;
            }
            if (skipping) { yield return SkipOut(); yield break; }

            // 2 (6-12 s): the sky darkens and the dark realm tears open.
            Darken(true);
            WardenMusic.Set("prologue", true, realBoss.bossMusic, realBoss.bossMusicVolume);
            bool portalOpen = false;
            while (!skipping && E(t0) < 12f)
            {
                float k = (E(t0) - 6f) / 6f;
                SetDarkness(Mathf.Clamp01(k * 1.5f));
                if (!portalOpen && E(t0) > 7f)
                {
                    portalOpen = true;
                    Prop(realBoss.prologuePortalEffect, portalSpot + Vector3.up * .1f, Quaternion.LookRotation(Flat(W - portalSpot)), 1.6f);
                    Prop(realBoss.summonCircleEffect, portalSpot + Vector3.up * .05f, Quaternion.identity, 2.5f);
                    CameraShake.Shake(.5f);
                }
                Say(t0, 6.4f, 11.8f, Narrator, "Until the night the sky tore open... and something crawled out of the dark realm.");
                Vector3 cam = portalSpot + dir * 15f + Right(dir) * 6f + Vector3.up * 3f;
                rig.Shot(Vector3.Lerp(cam, cam - dir * 3f, Smooth(k)), portalSpot + Vector3.up * 3f, 45f);
                yield return null;
            }
            if (skipping) { yield return SkipOut(); yield break; }

            // 3 (12-18 s): the Warden steps out and walks into the castle.
            SpawnPuppet(portalSpot, Flat(W - portalSpot));
            PuppetPlay("Walk", .1f);
            bool[] cue = new bool[4];
            while (!skipping && E(t0) < 18f)
            {
                float k = (E(t0) - 12f) / 6f;
                if (!cue[0]) WalkPuppet(W, 2.4f);
                if (Once(cue, 0, E(t0) > 16.8f)) { PuppetPlay("Taunt", .2f); PuppetVoice("VocalizationTaunt"); CameraShake.Shake(.5f); }
                Say(t0, 12.3f, 17.8f, Narrator, "The Abyssal Warden. He came for our walls, our people... our home.");
                Vector3 p = puppet.transform.position;
                Vector3 cam = p + Flat(W - portalSpot) * 6f + Right(dir) * 2.5f + Vector3.up * .6f;
                rig.Shot(cam, p + Vector3.up * 2.3f, Mathf.Lerp(48f, 40f, k));
                yield return null;
            }
            if (skipping) { yield return SkipOut(); yield break; }

            // 4 (18-25 s): he raises a skeleton army that takes the town.
            puppet.transform.position = W;
            PuppetPlay("CastStart", .15f);
            bool summoned = false, looping = false;
            var army = new List<Vector3>();
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * 360f;
                Vector3 spot = W + Quaternion.AngleAxis(angle, Vector3.up) * dir * (i % 2 == 0 ? 5f : 8f);
                if (NavMesh.SamplePosition(spot, out NavMeshHit hit, 3f, NavMesh.AllAreas)) army.Add(hit.position);
            }
            foreach (var spot in army) Prop(realBoss.summonCircleEffect, spot + Vector3.up * .05f, Quaternion.identity, 1f, 4f);
            while (!skipping && E(t0) < 25f)
            {
                float k = (E(t0) - 18f) / 7f;
                if (!looping && E(t0) > 18.6f) { looping = true; PuppetPlay("CastLoop", .1f); }
                if (!summoned && E(t0) > 19.6f)
                {
                    summoned = true;
                    SpawnArmy(army);
                    PuppetPlay("CastEnd", .1f);
                    CameraShake.Shake(.7f);
                }
                if (Once(cue, 1, E(t0) > 21.5f)) { PuppetPlay("Taunt", .2f); PuppetVoice("VocalizationTaunt"); }
                FacePuppet(player.position);
                Say(t0, 18.3f, 24.8f, Narrator, "With a roar he raised the dead. An army of bone to take the town.");
                Vector3 cam = W + dir * 14f + Right(dir) * 10f + Vector3.up * 7f;
                rig.Shot(Vector3.Lerp(cam, cam + Vector3.up * 3f + dir * 3f, Smooth(k)), W + Vector3.up * 1.5f, 52f);
                yield return null;
            }
            if (skipping) { yield return SkipOut(); yield break; }

            // 5 (25-34 s): the player stands against him... and is hurled out of the castle.
            bool[] beat = new bool[12];
            while (!skipping && E(t0) < 34f)
            {
                float e = E(t0);
                Vector3 ppos = player.position;
                FacePuppet(ppos);
                FacePlayer(puppet.transform.position);
                if (e < 26.6f) WalkPuppet(ppos - Flat(ppos - W) * 2.4f, 2.2f, true);

                if (Once(beat, 0, e > 25.2f)) PuppetPlay("Idle", .2f);
                if (Once(beat, 1, e > 26.5f)) PlayerMode(1, 1);                                  // punch
                if (Once(beat, 2, e > 27.2f)) { PuppetPlay("Dodge", .1f); PlayerMode(1, 3); }       // he slips the kick
                if (Once(beat, 3, e > 28.4f)) PuppetPlay("Attack1", .1f);
                if (Once(beat, 4, e > 29.0f)) { PlayerHit(1); }
                if (Once(beat, 5, e > 29.8f)) PlayerMode(5, 3);                                  // roll back
                if (Once(beat, 6, e > 30.6f)) PuppetPlay("Attack3", .1f);
                if (Once(beat, 7, e > 31.4f)) { PlayerHit(2); }
                if (Once(beat, 8, e > 32.0f)) PuppetPlay("Attack4", .1f);
                if (Once(beat, 9, e > 33.0f))
                {
                    Vector3 hitAt = Ground(player.position);
                    Prop(realBoss.slamImpactEffect, hitAt, Quaternion.identity, 1f, 4f);
                    Prop(realBoss.roarEffect, hitAt, Quaternion.identity, 1f, 3.5f);
                    PlayerMode(10, 1);                                                             // knocked back
                    CameraShake.Shake(1.6f);
                }
                if (e > 33.1f) fade = Mathf.Clamp01((e - 33.1f) / .8f);

                if (e < 28.3f) Say(t0, 25.3f, 28.2f, Hero, "Not while I still stand!");
                else Say(t0, 31.0f, 33.8f, Boss, "You are nothing before the Abyss.");

                Vector3 mid = (ppos + puppet.transform.position) * .5f;
                Vector3 across = Right(Flat(puppet.transform.position - ppos));
                rig.Shot(mid + across * 7f + Vector3.up * 2.2f, mid + Vector3.up * 1.4f, 50f);
                yield return null;
            }
            if (skipping) { yield return SkipOut(); yield break; }

            // 6 (34-38 s): black. Strike the castle set, move the player to the village.
            fade = 1f;
            WardenMusic.Set("prologue", false);
            ClearStage();
            Darken(false);
            MovePlayer(villageSpot, toGate);
            bool sat = false;
            while (!skipping && E(t0) < 38f)
            {
                if (!sat && E(t0) > 36.5f) { sat = true; PlayerMode(4, 8); }                       // lying / sitting
                Say(t0, 34.4f, 37.8f, Narrator, "He cast me out of my own home, and left me for dead.");
                rig.Shot(villageSpot + toGate * 3f + Vector3.up * 1.2f, villageSpot + Vector3.up * .6f, 45f);
                yield return null;
            }
            if (skipping) { yield return SkipOut(); yield break; }

            // 7 (38-45 s): waking in the village, by the old teleporter.
            bool stood = false;
            while (!skipping && E(t0) < 45f)
            {
                float k = (E(t0) - 38f) / 7f;
                fade = 1f - Mathf.Clamp01((E(t0) - 38f) / 1.5f);
                if (!stood && E(t0) > 41.6f) { stood = true; if (rig.PlayerAnimal != null) rig.PlayerAnimal.Mode_Interrupt(); }
                if (E(t0) < 41.6f) Say(t0, 38.6f, 41.4f, Hero, "...The village. The old teleporter.");
                else Say(t0, 41.8f, 44.8f, Hero, "The castle has fallen. But it is not lost.");
                Vector3 side = Right(toGate);
                Vector3 cam = villageSpot + toGate * 2.6f + side * 1.2f + Vector3.up * Mathf.Lerp(.8f, 1.6f, Smooth(k));
                rig.Shot(cam, villageSpot + Vector3.up * Mathf.Lerp(.7f, 1.5f, Smooth(k)), 42f);
                yield return null;
            }
            if (skipping) { yield return SkipOut(); yield break; }

            // 8 (45-52 s): the vow, facing the teleporter home.
            bool vowed = false;
            while (!skipping && E(t0) < 52f)
            {
                float k = (E(t0) - 45f) / 7f;
                if (!vowed && E(t0) > 45.4f) { vowed = true; PlayerMode(4, 27); }
                Say(t0, 45.3f, 49.6f, Hero, "I swear it. I will gather allies, return through that gate, and take back our home.");
                title = "RECLAIM YOUR HOME";
                subtitle = "Recruit allies, then return through the teleporter";
                titleAlpha = Mathf.Clamp01((E(t0) - 48.2f) / .8f) * (1f - Mathf.Clamp01((E(t0) - 51.2f) / .8f));
                Vector3 behind = villageSpot - toGate * 3.2f + Right(toGate) * 1f + Vector3.up * 1.8f;
                Vector3 target = gate != null ? gate.position + Vector3.up * 2f : villageSpot + toGate * 10f;
                rig.Shot(Vector3.Lerp(behind, behind - toGate * 2.5f + Vector3.up * 1.5f, Smooth(k)), Vector3.Lerp(villageSpot + Vector3.up * 1.6f, target, Smooth(k)), 50f);
                yield return null;
            }
            if (skipping) { yield return SkipOut(); yield break; }

            Finish();
        }

        // ---------------------------------------------------------------- end
        IEnumerator SkipOut()
        {
            yield return WardenCinematicRig.WaitForSkipKeyRelease();
            Finish();
        }

        void Finish()
        {
            if (!playing && !Playing) return;
            ClearStage();
            Darken(false);
            // Skipped mid-scene: the player must still wake up in the village.
            if (player != null && Vector3.Distance(player.position, villageSpot) > 3f) MovePlayer(villageSpot, toGate);
            if (rig.PlayerAnimal != null && rig.PlayerAnimal.IsPlayingMode) rig.PlayerAnimal.Mode_Interrupt();

            if (playing) rig.End(this, restoreControl: false); // MenuManager.CloseStoryCS starts gameplay
            playing = false;
            Playing = false;
            WardenMusic.Set("prologue", false);
            fade = 0f;
            var done = onFinished;
            onFinished = null;
            done?.Invoke();
            Destroy(gameObject, 2f);
        }

        void OnDisable() { if (Playing) Finish(); }

        // ---------------------------------------------------------------- stage helpers
        void SpawnPuppet(Vector3 at, Vector3 facing)
        {
            // A cosmetic copy of the real Warden: same model, animator and demon FX, none of the boss logic.
            bool wasActive = realBoss.gameObject.activeSelf;
            realBoss.gameObject.SetActive(false);
            puppet = Instantiate(realBoss.gameObject, at, Quaternion.LookRotation(facing));
            realBoss.gameObject.SetActive(wasActive);
            puppet.name = "Abyssal Warden (Cinematic)";

            var behaviours = new List<MonoBehaviour>(puppet.GetComponents<MonoBehaviour>());
            foreach (var b in behaviours) if (b != null && !(b is AbyssalWardenBoss)) DestroyImmediate(b);
            foreach (var b in puppet.GetComponents<AbyssalWardenBoss>()) DestroyImmediate(b);
            var agent = puppet.GetComponent<NavMeshAgent>(); if (agent != null) DestroyImmediate(agent);
            foreach (var col in puppet.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            puppet.SetActive(true);

            foreach (var a in puppet.GetComponentsInChildren<Animator>(true))
                if (a.runtimeAnimatorController != null && a.enabled) { puppetAnim = a; break; }
            puppetFX = puppet.GetComponentInChildren<WardenDemonFX>(true);
            Prop(realBoss.teleportOutEffect, at + Vector3.up, Quaternion.identity, 2f, 2.5f);
        }

        void SpawnArmy(List<Vector3> spots)
        {
            var army = realBoss.prologueArmy != null && realBoss.prologueArmy.Length > 0 ? realBoss.prologueArmy : new[] { realBoss.summonPrefab };
            for (int i = 0; i < spots.Count; i++)
            {
                var prefab = army[i % army.Length];
                if (prefab == null) continue;
                Prop(realBoss.summonBurstEffect, spots[i] + Vector3.up * .5f, Quaternion.identity, 2f, 2.5f);
                Vector3 face = Flat(player.position - spots[i]);
                var skeleton = Instantiate(prefab, spots[i], Quaternion.LookRotation(face));
                var death = skeleton.GetComponent<EnemyDeathEffect>();
                if (death != null) death.CountsTowardLeadership = false;
                props.Add(skeleton);
            }
        }

        void ClearStage()
        {
            foreach (var p in props)
            {
                if (p == null) continue;
                if (p.GetComponent<EmeraldAI.EmeraldSystem>() != null) Destroy(p);   // army
                else WardenVFX.StopAndDestroy(p, .5f);                                // effects fade out
            }
            props.Clear();
            if (puppetFX != null) puppetFX.ResetFX();
            if (puppet != null) Destroy(puppet);
            puppet = null;
        }

        void WalkPuppet(Vector3 target, float speed, bool keepAnimation = false)
        {
            if (puppet == null) return;
            Vector3 p = puppet.transform.position;
            Vector3 flat = target - p; flat.y = 0;
            if (flat.magnitude < .2f) { if (!keepAnimation) PuppetPlay("Idle", .3f); return; }
            p += flat.normalized * Mathf.Min(speed * Time.deltaTime, flat.magnitude);
            puppet.transform.position = Ground(p);
            puppet.transform.rotation = Quaternion.RotateTowards(puppet.transform.rotation, Quaternion.LookRotation(flat), 240f * Time.deltaTime);
            if (keepAnimation && puppetAnim != null && !puppetAnim.GetCurrentAnimatorStateInfo(0).IsName("Walk") && !puppetAnim.IsInTransition(0)) PuppetPlay("Walk", .15f);
        }

        void FacePuppet(Vector3 point)
        {
            if (puppet == null) return;
            Vector3 v = Flat(point - puppet.transform.position);
            puppet.transform.rotation = Quaternion.RotateTowards(puppet.transform.rotation, Quaternion.LookRotation(v), 200f * Time.deltaTime);
        }

        void FacePlayer(Vector3 point)
        {
            Vector3 v = Flat(point - player.position);
            player.rotation = Quaternion.RotateTowards(player.rotation, Quaternion.LookRotation(v), 300f * Time.deltaTime);
        }

        void PuppetPlay(string state, float fade) { if (puppetAnim != null) puppetAnim.CrossFadeInFixedTime(state, fade, 0); }
        void PuppetVoice(string group) { if (puppetFX != null) puppetFX.Voice(group); }

        void PlayerMode(int mode, int ability) { if (rig.PlayerAnimal != null) rig.PlayerAnimal.Mode_TryActivate(mode, ability); }

        void PlayerHit(int side)
        {
            PlayerMode(3, side);
            WardenVFX.Spawn(realBoss.hitEffect, player.position + Vector3.up * 1.3f, Quaternion.identity, 2f);
            CameraShake.Shake(.6f);
        }

        void MovePlayer(Vector3 at, Vector3 facing)
        {
            if (rig.PlayerAnimal != null) rig.PlayerAnimal.Teleport(at);
            else
            {
                var animal = player.GetComponent<MalbersAnimations.Controller.MAnimal>();
                if (animal != null) animal.Teleport(at); else player.position = at;
            }
            player.rotation = Quaternion.LookRotation(facing);
        }

        // life <= 0: kept until the stage is cleared.
        void Prop(GameObject prefab, Vector3 pos, Quaternion rot, float scale, float life = 0f)
        {
            var go = WardenVFX.Spawn(prefab, pos, rot, life, null, scale);
            if (go != null && life <= 0f) props.Add(go);
        }

        // Darkness over the castle: dim the sun and ambient light; restored afterwards.
        void Darken(bool on)
        {
            if (on && !darkened)
            {
                sun = RenderSettings.sun;
                if (sun == null) foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional && (sun == null || l.intensity > sun.intensity)) sun = l;
                sunIntensity = sun != null ? sun.intensity : 1f;
                ambientIntensity = RenderSettings.ambientIntensity;
                darkened = true;
            }
            else if (!on && darkened)
            {
                SetDarkness(0f);
                darkened = false;
            }
        }

        void SetDarkness(float k)
        {
            if (!darkened) return;
            if (sun != null) sun.intensity = Mathf.Lerp(sunIntensity, sunIntensity * .3f, k);
            RenderSettings.ambientIntensity = Mathf.Lerp(ambientIntensity, ambientIntensity * .45f, k);
        }

        // ---------------------------------------------------------------- frame / UI
        void Update()
        {
            if (playing && Time.timeScale > 0f) clock += Time.unscaledDeltaTime;
            letterbox = Mathf.MoveTowards(letterbox, playing ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            if (!playing) return;

            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.isPressed) skipHeld += Time.unscaledDeltaTime; else skipHeld = 0f;
            if (skipHeld >= SkipHold && !skipping)
            {
                skipping = true;
                fade = 1f;
                ClearStage();
                Darken(false);
            }
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
            WardenCinematicRig.DrawSubtitle(speaker, line, lineAlpha);
            WardenCinematicRig.DrawTitle(title, subtitle, titleAlpha, new Color(1f, .82f, .4f));
            WardenCinematicRig.DrawSkipHint(skipHeld, SkipHold);
        }

        void Say(float t0, float from, float to, string who, string text)
        {
            float e = E(t0);
            if (e < from || e > to) { if (line == text) lineAlpha = 0f; return; }
            speaker = who;
            line = text;
            lineAlpha = Mathf.Clamp01(Mathf.Min((e - from) / .35f, (to - e) / .35f));
        }

        // ---------------------------------------------------------------- utils
        float E(float t0) => clock - t0;
        static bool Once(bool[] flags, int i, bool condition) { if (flags[i] || !condition) return false; flags[i] = true; return true; }
        static float Smooth(float k) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k));
        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > .0001f ? v.normalized : Vector3.forward; }
        static Vector3 Right(Vector3 f) => Vector3.Cross(Vector3.up, f);

        static Transform FindByName(string name)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t;
            return null;
        }

        // The boss-area arrival point (LeadershipBossAreaEntry.arrivalPoint), used to orient the castle stage.
        Vector3 ArrivalPoint()
        {
            var entry = FindFirstObjectByType<LeadershipBossAreaEntry>(FindObjectsInactive.Include);
            if (entry != null)
            {
                FieldInfo f = typeof(LeadershipBossAreaEntry).GetField("arrivalPoint", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (f != null && f.GetValue(entry) is Transform t && t != null) return t.position;
            }
            return realBoss.transform.position + realBoss.transform.forward * 10f;
        }

        Vector3 Snap(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : Ground(p);

        Vector3 Ground(Vector3 p)
        {
            float best = float.MaxValue; Vector3 point = p;
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * 4f, Vector3.down, 30f, ~(1 << 20), QueryTriggerInteraction.Ignore))
            {
                if (puppet != null && hit.transform.IsChildOf(puppet.transform)) continue;
                if (hit.collider.GetComponentInParent<EmeraldAI.EmeraldSystem>() != null) continue;
                if (hit.distance < best) { best = hit.distance; point = hit.point; }
            }
            return point;
        }
    }
}
