// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace CivilianSystem.EditorTools
{
    /// <summary>
    /// One-click demo builder.
    /// Menu: Tools > Civilian System > Build Demo Scene
    /// Creates: materials, loot prefabs + table, animator controller,
    /// 3 civilian prefabs (Passive/Aggressive/Coward) + definitions,
    /// full demo scene (ground, obstacles, patrol path, population manager, player),
    /// bakes NavMesh, saves scene.
    /// </summary>
    public static class CivilianDemoBuilder
    {
        const string Root = "Assets/CivilianSystemDemo";
        const string PrefabDir = Root + "/Prefabs";
        const string DefDir = Root + "/Definitions";
        const string MatDir = Root + "/Materials";

        [MenuItem("Tools/Civilian System/Build Demo Scene")]
        public static void Build()
        {
            EnsureFolders();

            // ---------- assets ----------
            var matGround   = MakeMat("Ground",   new Color(0.35f, 0.45f, 0.30f));
            var matObstacle = MakeMat("Obstacle", new Color(0.45f, 0.42f, 0.40f));
            var matPassive  = MakeMat("Passive",  new Color(0.30f, 0.75f, 0.35f));
            var matAggro    = MakeMat("Aggro",    new Color(0.85f, 0.25f, 0.25f));
            var matCoward   = MakeMat("Coward",   new Color(0.95f, 0.80f, 0.25f));
            var matCoin     = MakeMat("Coin",     new Color(1.00f, 0.85f, 0.10f));
            var matGem      = MakeMat("Gem",      new Color(0.30f, 0.60f, 1.00f));
            var matPlayer   = MakeMat("Player",   new Color(0.25f, 0.30f, 0.85f));

            var coinPrefab = MakeLootPrefab("Coin", PrimitiveType.Cylinder, matCoin, new Vector3(0.3f, 0.05f, 0.3f));
            var gemPrefab  = MakeLootPrefab("Gem",  PrimitiveType.Sphere,   matGem,  Vector3.one * 0.25f);

            var lootTable = MakeLootTable(coinPrefab, gemPrefab);
            var animCtrl = MakeAnimatorController();

            var villagerPrefab = MakeCivilianPrefab("Villager", matPassive, animCtrl);
            var guardPrefab    = MakeCivilianPrefab("Guard",    matAggro,   animCtrl);
            var peasantPrefab  = MakeCivilianPrefab("Peasant",  matCoward,  animCtrl);

            var villagerDef = MakeDefinition("Villager", villagerPrefab, BehaviorType.Passive,    lootTable);
            var guardDef    = MakeDefinition("Guard",    guardPrefab,    BehaviorType.Aggressive, lootTable);
            var peasantDef  = MakeDefinition("Peasant",  peasantPrefab,  BehaviorType.Coward,     lootTable);

            AssetDatabase.SaveAssets();

            // ---------- scene ----------
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ground
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(8, 1, 8); // 80x80
            ground.GetComponent<Renderer>().sharedMaterial = matGround;
            GameObjectUtility.SetStaticEditorFlags(ground, StaticEditorFlags.NavigationStatic);

            // obstacles
            var obstacleRoot = new GameObject("Obstacles");
            var rng = new System.Random(42);
            for (int i = 0; i < 14; i++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Obstacle_{i}";
                box.transform.SetParent(obstacleRoot.transform);
                float x = Mathf.Lerp(-30f, 30f, (float)rng.NextDouble());
                float z = Mathf.Lerp(-30f, 30f, (float)rng.NextDouble());
                if (Mathf.Abs(x) < 6 && Mathf.Abs(z) < 6) { x += 10; } // keep spawn clear
                float sx = Mathf.Lerp(1.5f, 5f, (float)rng.NextDouble());
                float sz = Mathf.Lerp(1.5f, 5f, (float)rng.NextDouble());
                box.transform.position = new Vector3(x, 1f, z);
                box.transform.localScale = new Vector3(sx, 2f, sz);
                box.GetComponent<Renderer>().sharedMaterial = matObstacle;
                GameObjectUtility.SetStaticEditorFlags(box, StaticEditorFlags.NavigationStatic);
            }

            // patrol path (square route)
            var pathGO = new GameObject("PatrolPath");
            var path = pathGO.AddComponent<WaypointPath>();
            Vector3[] pts = { new(-15, 0, -15), new(15, 0, -15), new(15, 0, 15), new(-15, 0, 15) };
            var wps = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var wp = new GameObject($"WP_{i}").transform;
                wp.SetParent(pathGO.transform);
                wp.position = pts[i];
                wps[i] = wp;
            }
            path.waypoints = wps;
            path.mode = PathMode.Loop;

            // player
            var player = new GameObject("Player");
            player.tag = "Player"; // population manager auto-finds by this tag
            player.transform.position = new Vector3(0, 1.1f, -25);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.4f; cc.center = new Vector3(0, 0.9f, 0);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(player.transform, false);
            body.transform.localPosition = new Vector3(0, 0.9f, 0);
            body.GetComponent<Renderer>().sharedMaterial = matPlayer;
            Object.DestroyImmediate(body.GetComponent<Collider>());
            var camGO = new GameObject("PlayerCamera");
            camGO.tag = "MainCamera"; // world UI billboards + Camera.main users need this
            camGO.transform.SetParent(player.transform, false);
            camGO.transform.localPosition = new Vector3(0, 1.6f, 0);
            var cam = camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
            var demoPlayer = player.AddComponent<CivilianSystem.Demo.DemoPlayer>();
            demoPlayer.cam = cam;

            // remove default main camera (player has own)
            var defaultCam = GameObject.Find("Main Camera");
            if (defaultCam != null) Object.DestroyImmediate(defaultCam);

            // population manager
            var mgrGO = new GameObject("CivilianPopulationManager");
            var mgr = mgrGO.AddComponent<CivilianPopulationManager>();
            mgr.player = player.transform;
            mgr.spawnRadius = 25f;
            mgr.aiCullDistance = 60f;
            mgr.fullCullDistance = 120f;
            mgr.populations.Add(new CivilianPopulationManager.PopulationEntry
            { definition = villagerDef, targetCount = 12, respawnDelay = 10f });
            mgr.populations.Add(new CivilianPopulationManager.PopulationEntry
            { definition = guardDef, targetCount = 3, respawnDelay = 20f, assignPath = path });
            mgr.populations.Add(new CivilianPopulationManager.PopulationEntry
            { definition = peasantDef, targetCount = 8, respawnDelay = 10f });

            // navmesh bake (legacy pipeline — works without AI Navigation package)
#pragma warning disable 618
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore 618

            // save
            string scenePath = Root + "/DemoScene.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Civilian System",
                $"Demo built!\n\nScene: {scenePath}\nPrefabs: {PrefabDir}\nDefinitions: {DefDir}\n\nPress Play. WASD move, LMB attack.\nGreen=Passive, Red=Aggressive(patrols), Yellow=Coward.", "OK");
            Debug.Log($"[CivilianDemoBuilder] Demo scene saved: {scenePath}");
        }

        // ---------- helpers ----------

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets", "CivilianSystemDemo");
            foreach (var dir in new[] { PrefabDir, DefDir, MatDir })
                if (!AssetDatabase.IsValidFolder(dir))
                    AssetDatabase.CreateFolder(Root, System.IO.Path.GetFileName(dir));
        }

        static Material MakeMat(string name, Color color)
        {
            string p = $"{MatDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (existing != null) { existing.color = color; return existing; }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, p);
            return m;
        }

        static GameObject MakeLootPrefab(string name, PrimitiveType shape, Material mat, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.GetComponent<Collider>().isTrigger = true;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true; // trigger pickup, no physics tumble in demo
            var item = go.AddComponent<CivilianSystem.Demo.DemoLootItem>();
            item.itemName = name;

            string p = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, p);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static LootTable MakeLootTable(GameObject coin, GameObject gem)
        {
            string p = $"{DefDir}/DemoLootTable.asset";
            var lt = AssetDatabase.LoadAssetAtPath<LootTable>(p);
            if (lt == null)
            {
                lt = ScriptableObject.CreateInstance<LootTable>();
                AssetDatabase.CreateAsset(lt, p);
            }
            lt.entries = new[]
            {
                new LootTable.LootEntry { itemPrefab = coin, dropChance = 0.9f, minCount = 1, maxCount = 3 },
                new LootTable.LootEntry { itemPrefab = gem,  dropChance = 0.25f, minCount = 1, maxCount = 1 },
            };
            lt.dropUpwardForce = 0f; // kinematic demo items
            EditorUtility.SetDirty(lt);
            return lt;
        }

        static AnimatorController MakeAnimatorController()
        {
            string p = $"{DefDir}/CivilianAnimator.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(p);
            if (existing != null) return existing;

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(p);
            ctrl.AddParameter("Speed",  AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit",    AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die",    AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var idle   = sm.AddState("Locomotion"); // empty states — swap in real clips later
            var attack = sm.AddState("Attack");
            var hit    = sm.AddState("Hit");
            var die    = sm.AddState("Die");
            sm.defaultState = idle;

            var toAttack = sm.AddAnyStateTransition(attack);
            toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            toAttack.duration = 0.1f; toAttack.canTransitionToSelf = false;

            var toHit = sm.AddAnyStateTransition(hit);
            toHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            toHit.duration = 0.1f; toHit.canTransitionToSelf = false;

            var toDie = sm.AddAnyStateTransition(die);
            toDie.AddCondition(AnimatorConditionMode.If, 0, "Die");
            toDie.duration = 0.1f; toDie.canTransitionToSelf = false;

            var attackBack = attack.AddTransition(idle);
            attackBack.hasExitTime = true; attackBack.exitTime = 1f; attackBack.duration = 0.1f;
            var hitBack = hit.AddTransition(idle);
            hitBack.hasExitTime = true; hitBack.exitTime = 1f; hitBack.duration = 0.1f;
            // Die: no exit — corpse stays

            return ctrl;
        }

        static GameObject MakeCivilianPrefab(string name, Material mat, RuntimeAnimatorController animCtrl)
        {
            var root = new GameObject(name);

            // visual: capsule body + nose cube (shows facing)
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            var anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = animCtrl;
            anim.applyRootMotion = false;

            var bodyGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyGO.name = "Body";
            bodyGO.transform.SetParent(model.transform, false);
            bodyGO.transform.localPosition = new Vector3(0, 0.9f, 0);
            bodyGO.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(bodyGO.GetComponent<Collider>());

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            nose.transform.SetParent(model.transform, false);
            nose.transform.localPosition = new Vector3(0, 1.5f, 0.35f);
            nose.transform.localScale = new Vector3(0.15f, 0.15f, 0.3f);
            nose.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(nose.GetComponent<Collider>());

            // agent + collider
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f; agent.height = 1.8f;
            agent.angularSpeed = 360f; agent.acceleration = 12f;

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.radius = 0.35f; capsule.height = 1.8f;
            capsule.center = new Vector3(0, 0.9f, 0);

            // system components
            var src = root.AddComponent<AudioSource>();
            src.playOnAwake = false;
            root.AddComponent<CivilianHealth>();
            root.AddComponent<CivilianMovement>();
            root.AddComponent<CivilianTaskScheduler>();
            root.AddComponent<CivilianAudioAnim>();
            root.AddComponent<Civilian>();

            string p = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, p);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static CivilianDefinition MakeDefinition(string name, GameObject prefab, BehaviorType behavior, LootTable loot)
        {
            string p = $"{DefDir}/{name}_Definition.asset";
            var def = AssetDatabase.LoadAssetAtPath<CivilianDefinition>(p);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<CivilianDefinition>();
                AssetDatabase.CreateAsset(def, p);
            }

            def.civilianName = name;
            def.prefab = prefab;
            def.behavior = behavior;
            def.lootTable = loot;
            def.maxHealth = behavior == BehaviorType.Aggressive ? 150f : 80f;
            def.walkSpeed = 1.8f;
            def.runSpeed = behavior == BehaviorType.Coward ? 5.5f : 4.5f;
            def.perceptionRadius = behavior == BehaviorType.Aggressive ? 14f : 10f;
            def.fleeOrChaseDistance = 22f;
            def.attackDamage = 8f;
            def.attackRange = 1.8f;
            def.attackCooldown = 1.4f;
            def.wanderRadius = 12f;
            def.corpseLifetime = 6f;

            var civ = prefab.GetComponent<Civilian>();
            civ.definition = def;
            EditorUtility.SetDirty(prefab);
            EditorUtility.SetDirty(def);
            return def;
        }
    }
}
