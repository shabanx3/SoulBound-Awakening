#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace AbyssalWarden.Editor
{
    // Swaps the Warden's look for the configured demon from the InfinityPBR Demons demo scene and wires up
    // the demo's animation events: SFB_AudioManager sounds, swipe trails, hand-held cast ball, head fire, body smoke.
    public static class WardenDemonModelSetup
    {
        const string BossPrefabPath = "Assets/Prefab/Boss/The Abyssal Warden.prefab";
        const string DemoPrefabPath = "Assets/InfinityPBR/_InfinityPBR - Demons/_Prefabs/Demons DEMO.prefab";
        const string DemoScenePath = "Assets/InfinityPBR/_Demo Scenes/InfinityPBR - Demons.unity";
        const string DemoFbxPath = "Assets/InfinityPBR/_InfinityPBR - Demons/Models/Demons DEMO.fbx";
        const string FallbackClipFolder = "Assets/AbyssalWarden/Animations/";
        const string ControllerPath = "Assets/AbyssalWarden/AbyssalWarden Demon.controller";
        const string VisualName = "Demon Model";

        [MenuItem("Tools/Abyssal Warden/Apply Demons Demo Model + FX")]
        public static void Apply()
        {
            var demoPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DemoPrefabPath);
            if (demoPrefab == null) { EditorUtility.DisplayDialog("Missing prefab", DemoPrefabPath, "OK"); return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath) == null) { EditorUtility.DisplayDialog("Missing prefab", BossPrefabPath, "OK"); return; }

            var controller = BuildController();
            if (controller == null) return;

            var mods = ReadDemoSceneModifications(demoPrefab);

            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                var boss = root.GetComponent<AbyssalWardenBoss>();

                // Re-run: replace the model added last time (older runs left it named "Demons DEMO").
                foreach (var oldFx in root.GetComponentsInChildren<WardenDemonFX>(true))
                    Object.DestroyImmediate(oldFx.gameObject);

                // Hide the old Demons Humanoid mesh/bones and stop its Animator (it is kept only for RequireComponent).
                foreach (Transform child in root.transform)
                    if (child.name != "CastOrigin") child.gameObject.SetActive(false);
                var rootAnimator = root.GetComponent<Animator>();
                if (rootAnimator != null) { rootAnimator.runtimeAnimatorController = null; rootAnimator.enabled = false; }

                var visual = (GameObject)PrefabUtility.InstantiatePrefab(demoPrefab, root.transform);
                if (mods.Count > 0) PrefabUtility.SetPropertyModifications(visual, mods.ToArray());
                visual.name = VisualName;
                // Unpacked so the demo script can be removed (it would double-handle ToggleSwipe/CastStart events).
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.layer;

                // Move the demo's FX references over to the Warden receiver and drop the UI-driven demo script.
                var demo = visual.GetComponent<SFB_DemoDemon>();
                var fx = visual.GetComponent<WardenDemonFX>();
                if (fx == null) fx = visual.AddComponent<WardenDemonFX>();
                if (demo != null)
                {
                    fx.swipeTrails = demo.swipeTrails;
                    fx.spawnPos = demo.spawnPos != null ? demo.spawnPos.transform : null;
                    fx.castPrefab = demo.castPrefab;
                    Object.DestroyImmediate(demo);
                }
                fx.handlesAudio = visual.GetComponent<SFB_AudioManager>() != null;

                var animator = visual.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // Demo audio sources are 2D; make them positional so the head fire and cast warm-up fade with distance.
                foreach (var src in visual.GetComponentsInChildren<AudioSource>(true))
                {
                    src.spatialBlend = 1f;
                    src.rolloffMode = AudioRolloffMode.Linear;
                    src.minDistance = 2f;
                    src.maxDistance = 30f;
                }

                boss.animator = animator;
                boss.demonFX = fx;
                if (fx.spawnPos != null) boss.castOrigin = fx.spawnPos;

                PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            Debug.Log($"[Abyssal Warden] Demon model applied ({mods.Count} demo-scene overrides copied). Controller: {ControllerPath}");
        }

        // Same state layout as WardenSetup, but the motions are the Demons DEMO.fbx clips, which carry the audio/VFX events.
        static AnimatorController BuildController()
        {
            var fbxClips = AssetDatabase.LoadAllAssetsAtPath(DemoFbxPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name, c => c);
            if (fbxClips.Count == 0) { EditorUtility.DisplayDialog("Missing clips", "No clips found in " + DemoFbxPath, "OK"); return null; }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);

            string[] names = {"Idle","Walk","Attack1","Attack2","Attack3","Attack4","CastStart","CastLoop","CastEnd","Death","Dodge","HitReaction","IdleBreak","JumpBack","Taunt","WalkBackwards"};
            string[] clips = {"idle","walk","attack1","attack2","attack3","attack4","cast start","cast loop","cast end","death","dodge","hit reation","idle break","Jump Back","tuant","walk backwards"};
            AnimatorState idle = null, walk = null;
            for (int i = 0; i < names.Length; i++)
            {
                if (!fbxClips.TryGetValue(clips[i], out var clip))
                {
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(FallbackClipFolder + clips[i] + ".anim");
                    if (clip == null) Debug.LogWarning("[Abyssal Warden] Missing clip: " + clips[i]);
                }
                var state = machine.AddState(names[i]);
                state.motion = clip;
                if (i == 0) idle = state;
                if (i == 1) walk = state;
            }
            machine.defaultState = idle;
            var toWalk = idle.AddTransition(walk); toWalk.hasExitTime = false; toWalk.duration = .15f; toWalk.AddCondition(AnimatorConditionMode.If, 0, "Moving");
            var toIdle = walk.AddTransition(idle); toIdle.hasExitTime = false; toIdle.duration = .15f; toIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        // Copies the look (blend shapes, emissive colours, materials) of the demon placed in the demo scene.
        static List<PropertyModification> ReadDemoSceneModifications(GameObject demoPrefab)
        {
            var result = new List<PropertyModification>();
            var scene = SceneManager.GetSceneByPath(DemoScenePath);
            bool openedHere = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(DemoScenePath, OpenSceneMode.Additive);
                openedHere = true;
            }
            try
            {
                var source = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SFB_DemoDemon>(true))
                    .Select(d => d.gameObject)
                    .FirstOrDefault(g => PrefabUtility.GetCorrespondingObjectFromSource(g) == demoPrefab);
                if (source == null) { Debug.LogWarning("[Abyssal Warden] Demon not found in demo scene; using the default Demons DEMO look."); return result; }

                foreach (var mod in PrefabUtility.GetPropertyModifications(source) ?? new PropertyModification[0])
                {
                    if (mod.target == null) continue;
                    if (mod.propertyPath == "m_Name") continue;
                    if (mod.target is Transform t && t.parent == null) continue; // demo placement, not looks
                    if (mod.objectReference != null && !EditorUtility.IsPersistent(mod.objectReference)) continue; // demo UI sliders etc.
                    result.Add(mod);
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
            return result;
        }
    }
}
#endif
