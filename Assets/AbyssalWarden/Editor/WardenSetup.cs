#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.AI;

namespace AbyssalWarden.Editor
{
    public static class WardenSetup
    {
        const string Root="Assets/AbyssalWarden";
        [MenuItem("Tools/Abyssal Warden/Build Boss Prefab + Animator")]
        public static void Build()
        {
            string modelPath=Root+"/Models/Demons Humanoid.fbx";
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if(model==null) {EditorUtility.DisplayDialog("Missing FBX","Place Demons Humanoid.fbx at "+modelPath,"OK");return;}
            string controllerPath=Root+"/AbyssalWarden.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.parameters=new AnimatorControllerParameter[0];
            controller.AddParameter("Moving",AnimatorControllerParameterType.Bool);
            var machine=controller.layers[0].stateMachine;
            foreach(var state in machine.states) machine.RemoveState(state.state);
            string[] names={"Idle","Walk","Attack1","Attack2","Attack3","Attack4","CastStart","CastLoop","CastEnd","Death","Dodge","HitReaction","IdleBreak","JumpBack","Taunt","WalkBackwards"};
            string[] files={"idle.anim","walk.anim","attack1.anim","attack2.anim","attack3.anim","attack4.anim","cast start.anim","cast loop.anim","cast end.anim","death.anim","dodge.anim","hit reation.anim","idle break.anim","Jump Back.anim","tuant.anim","walk backwards.anim"};
            AnimatorState idle=null, walk=null;
            for(int i=0;i<names.Length;i++)
            {
                AnimationClip clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Animations/"+files[i]);
                var state=machine.AddState(names[i]); state.motion=clip;
                if(clip==null) Debug.LogWarning("Missing clip: "+files[i]);
                if(i==0) idle=state;
                if(i==1) walk=state;
            }
            machine.defaultState=idle;
            var toWalk=idle.AddTransition(walk); toWalk.hasExitTime=false; toWalk.duration=.15f; toWalk.AddCondition(AnimatorConditionMode.If,0,"Moving");
            var toIdle=walk.AddTransition(idle); toIdle.hasExitTime=false; toIdle.duration=.15f; toIdle.AddCondition(AnimatorConditionMode.IfNot,0,"Moving");
            // Non-locomotion states are played directly by name via CrossFadeInFixedTime.
            GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name="The Abyssal Warden";
            var animator=instance.GetComponent<Animator>(); if(animator==null) animator=instance.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller; animator.applyRootMotion=false;
            var agent=instance.GetComponent<NavMeshAgent>(); if(agent==null) agent=instance.AddComponent<NavMeshAgent>();
            agent.radius=.7f; agent.height=2.7f; agent.speed=3.2f; agent.angularSpeed=450f;
            var collider=instance.GetComponent<CapsuleCollider>(); if(collider==null) collider=instance.AddComponent<CapsuleCollider>();
            collider.height=2.7f; collider.radius=.7f; collider.center=new Vector3(0,1.35f,0);
            var brain=instance.GetComponent<AbyssalWardenBoss>(); if(brain==null) brain=instance.AddComponent<AbyssalWardenBoss>();
            var src=instance.GetComponent<AudioSource>(); if(src==null) src=instance.AddComponent<AudioSource>();
            src.spatialBlend=1f; brain.audioSource=src;
            var origin=new GameObject("CastOrigin"); origin.transform.SetParent(instance.transform,false); origin.transform.localPosition=new Vector3(0,1.8f,1.1f);
            brain.castOrigin=origin.transform;
            var hud=instance.GetComponent<WardenBossHUD>(); if(hud==null) hud=instance.AddComponent<WardenBossHUD>(); hud.boss=brain;
            string prefabPath=Root+"/The Abyssal Warden.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance,prefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Boss created","Prefab and Animator are ready! Drag 'The Abyssal Warden.prefab' into a NavMesh-baked scene. Assign Player and VFX in the Inspector. See README.","OK");
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }
    }
}
#endif
