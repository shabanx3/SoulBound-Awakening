// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace CivilianSystem.EditorTools
{
    /// <summary>
    /// New Character Setup Tool.
    /// Menu: Tools > Civilian System > Character Setup
    /// Takes any model (humanoid or generic), adds the required components,
    /// creates the CivilianDefinition asset and saves a ready prefab.
    /// </summary>
    public class CivilianSetupWindow : EditorWindow
    {
        GameObject model;
        RuntimeAnimatorController animatorController;
        string civilianName = "NewCivilian";
        RigType rigType = RigType.Humanoid;
        BehaviorType behavior = BehaviorType.Passive;
        float maxHealth = 100f;
        float walkSpeed = 1.8f;
        float runSpeed = 4.5f;
        LootTable lootTable;
        string savePath = "Assets/Civilians";

        [MenuItem("Tools/Civilian System/Character Setup")]
        public static void Open() => GetWindow<CivilianSetupWindow>("Civilian Setup");

        void OnGUI()
        {
            EditorGUILayout.LabelField("New Civilian Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            model = (GameObject)EditorGUILayout.ObjectField("Character Model", model, typeof(GameObject), true);
            animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
                "Animator Controller", animatorController, typeof(RuntimeAnimatorController), false);

            EditorGUILayout.HelpBox(
                "Animator controller needs parameters:\n" +
                "  float 'Speed', triggers 'Attack', 'Hit', 'Die'.\n" +
                "Works with Humanoid or Generic rigs.", MessageType.Info);

            civilianName = EditorGUILayout.TextField("Name", civilianName);
            rigType = (RigType)EditorGUILayout.EnumPopup("Rig Type", rigType);
            behavior = (BehaviorType)EditorGUILayout.EnumPopup("Behavior", behavior);
            maxHealth = EditorGUILayout.FloatField("Max Health", maxHealth);
            walkSpeed = EditorGUILayout.FloatField("Walk Speed", walkSpeed);
            runSpeed = EditorGUILayout.FloatField("Run Speed", runSpeed);
            lootTable = (LootTable)EditorGUILayout.ObjectField("Loot Table (optional)", lootTable, typeof(LootTable), false);
            savePath = EditorGUILayout.TextField("Save Folder", savePath);

            EditorGUILayout.Space(8);
            GUI.enabled = model != null;
            if (GUILayout.Button("Build Civilian Prefab + Definition", GUILayout.Height(32)))
                Build();
            GUI.enabled = true;
        }

        void Build()
        {
            // harden save path
            if (string.IsNullOrWhiteSpace(savePath)) savePath = "Assets/Civilians";
            savePath = savePath.Trim().TrimEnd('/', '\\').Replace('\\', '/');
            if (!savePath.StartsWith("Assets")) savePath = "Assets/" + savePath;

            if (!AssetDatabase.IsValidFolder(savePath))
            {
                string parent = System.IO.Path.GetDirectoryName(savePath);
                parent = string.IsNullOrEmpty(parent) ? "Assets" : parent.Replace('\\', '/');
                string leaf = System.IO.Path.GetFileName(savePath);
                if (!AssetDatabase.IsValidFolder(parent)) parent = "Assets";
                AssetDatabase.CreateFolder(parent, leaf);
            }

            // --- root object ---
            GameObject root = new GameObject(civilianName);

            // Model can be: project asset (prefab/FBX) -> InstantiatePrefab keeps link.
            // Or scene object dragged from Hierarchy -> InstantiatePrefab returns null, must clone.
            GameObject visual = null;
            if (EditorUtility.IsPersistent(model))
                visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (visual == null)
                visual = Instantiate(model);

            if (visual == null)
            {
                DestroyImmediate(root);
                EditorUtility.DisplayDialog("Civilian Setup",
                    "Could not instantiate the character model.\n" +
                    "Drag a prefab/FBX from the Project window, or a GameObject from the scene.", "OK");
                return;
            }

            visual.SetActive(true);
            visual.name = "Model";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            // --- animator ---
            Animator anim = visual.GetComponentInChildren<Animator>();
            if (anim == null) anim = visual.AddComponent<Animator>();
            if (animatorController != null) anim.runtimeAnimatorController = animatorController;
            anim.applyRootMotion = false; // NavMeshAgent drives position

            // --- agent + collider ---
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.speed = walkSpeed;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.35f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0, 0.9f, 0);

            // --- system components ---
            root.AddComponent<AudioSource>().playOnAwake = false;
            root.AddComponent<CivilianHealth>();
            root.AddComponent<CivilianMovement>();
            root.AddComponent<CivilianTaskScheduler>();
            root.AddComponent<CivilianAudioAnim>();
            var civ = root.AddComponent<Civilian>();

            // --- definition asset ---
            var def = ScriptableObject.CreateInstance<CivilianDefinition>();
            def.civilianName = civilianName;
            def.rigType = rigType;
            def.behavior = behavior;
            def.maxHealth = maxHealth;
            def.walkSpeed = walkSpeed;
            def.runSpeed = runSpeed;
            def.lootTable = lootTable;

            string defPath = AssetDatabase.GenerateUniqueAssetPath($"{savePath}/{civilianName}_Definition.asset");
            AssetDatabase.CreateAsset(def, defPath);
            civ.definition = def;

            // --- prefab ---
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath($"{savePath}/{civilianName}.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            def.prefab = prefab;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();

            DestroyImmediate(root);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log($"[Civilian Setup] Built '{civilianName}': prefab {prefabPath}, definition {defPath}");
        }
    }
}
