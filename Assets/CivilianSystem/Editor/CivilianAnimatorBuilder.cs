// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CivilianSystem.EditorTools
{
    /// <summary>
    /// Builds a complete civilian AnimatorController from the animation clips.
    /// Menu: Tools > Civilian System > Animator Builder
    ///
    /// States: Idle / Walk / Sprint (blend tree on 'Speed'), Attack, Hit, Die, Talk, Task.
    /// Parameters created: Speed (float), Attack, Hit, Die, Talk, Task (triggers).
    /// Talk + Task fire from the task system: TaskType.PlayAnimation with animTrigger "Talk"/"Task".
    /// Works with Humanoid and Generic clips.
    /// </summary>
    public class CivilianAnimatorBuilder : EditorWindow
    {
        AnimationClip idle, walk, sprint, hit, attack, die, talk, task;
        float walkSpeedThreshold = 1.8f;
        float sprintSpeedThreshold = 4.5f;
        string savePath = "Assets/Civilians/CivilianAnimator.controller";

        [MenuItem("Tools/Civilian System/Animator Builder")]
        public static void Open() => GetWindow<CivilianAnimatorBuilder>("Animator Builder");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Civilian Animator Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drag animation clips (Mixamo etc). Empty slots are skipped.\n" +
                "Locomotion = blend tree driven by 'Speed' (NavMeshAgent velocity).\n" +
                "Thresholds should match CivilianDefinition walk/run speeds.", MessageType.Info);

            idle   = Clip("1. Idle", idle);
            walk   = Clip("2. Walk", walk);
            sprint = Clip("3. Sprint / Run", sprint);
            hit    = Clip("4. Hit React", hit);
            attack = Clip("5. Attack", attack);
            die    = Clip("6. Die", die);
            talk   = Clip("7. Talk", talk);
            task   = Clip("8. Task / Perform", task);

            EditorGUILayout.Space(4);
            walkSpeedThreshold = EditorGUILayout.FloatField("Walk Speed Threshold", walkSpeedThreshold);
            sprintSpeedThreshold = EditorGUILayout.FloatField("Sprint Speed Threshold", sprintSpeedThreshold);
            savePath = EditorGUILayout.TextField("Save Path", savePath);

            EditorGUILayout.Space(8);
            GUI.enabled = idle != null;
            if (GUILayout.Button("Build Animator Controller", GUILayout.Height(32)))
                Build();
            GUI.enabled = true;
            if (idle == null)
                EditorGUILayout.HelpBox("Idle clip is required (minimum).", MessageType.Warning);
        }

        AnimationClip Clip(string label, AnimationClip c) =>
            (AnimationClip)EditorGUILayout.ObjectField(label, c, typeof(AnimationClip), false);

        void Build()
        {
            string dir = System.IO.Path.GetDirectoryName(savePath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string parent = System.IO.Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent)) parent = "Assets";
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(dir));
            }

            var old = AssetDatabase.LoadAssetAtPath<AnimatorController>(savePath);
            if (old != null) AssetDatabase.DeleteAsset(savePath);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(savePath);
            ctrl.AddParameter("Speed",  AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit",    AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die",    AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Talk",   AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Task",   AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            // ---- Locomotion blend tree: Idle -> Walk -> Sprint on Speed ----
            var locomotion = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;

            tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, walkSpeedThreshold);
            if (sprint != null) tree.AddChild(sprint, sprintSpeedThreshold);
            sm.defaultState = locomotion;

            // ---- one-shot states via AnyState triggers, auto-return ----
            AddOneShot(sm, locomotion, attack, "Attack", "Attack", returnToLocomotion: true);
            AddOneShot(sm, locomotion, hit,    "Hit",    "Hit",    returnToLocomotion: true);
            AddOneShot(sm, locomotion, talk,   "Talk",   "Talk",   returnToLocomotion: true);
            AddOneShot(sm, locomotion, task,   "Task",   "Task",   returnToLocomotion: true);
            AddOneShot(sm, locomotion, die,    "Die",    "Die",    returnToLocomotion: false); // corpse stays

            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(ctrl);
            EditorUtility.DisplayDialog("Animator Builder",
                $"Controller built:\n{savePath}\n\nAssign it to your civilian's Animator " +
                "(Model child) or via the Character Setup tool.", "OK");
        }

        static void AddOneShot(AnimatorStateMachine sm, AnimatorState locomotion,
                               AnimationClip clip, string stateName, string trigger, bool returnToLocomotion)
        {
            var state = sm.AddState(stateName);
            if (clip != null) state.motion = clip;

            var to = sm.AddAnyStateTransition(state);
            to.AddCondition(AnimatorConditionMode.If, 0, trigger);
            to.duration = 0.1f;
            to.canTransitionToSelf = false;

            if (returnToLocomotion)
            {
                var back = state.AddTransition(locomotion);
                back.hasExitTime = true;
                back.exitTime = 0.95f;
                back.duration = 0.15f;
            }
        }
    }
}
