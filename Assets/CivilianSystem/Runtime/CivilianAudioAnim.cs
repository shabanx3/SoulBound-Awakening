// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEngine;

namespace CivilianSystem
{
    /// <summary>
    /// Drives Animator + AudioSource. Works with Humanoid or Generic rigs —
    /// only depends on Animator parameters:
    ///   float "Speed", trigger "Attack", trigger "Hit", trigger "Die", trigger custom.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CivilianAudioAnim : MonoBehaviour
    {
        public static readonly int SpeedHash = Animator.StringToHash("Speed");
        public static readonly int AttackHash = Animator.StringToHash("Attack");
        public static readonly int HitHash = Animator.StringToHash("Hit");
        public static readonly int DieHash = Animator.StringToHash("Die");

        Animator animator;
        AudioSource source;
        CivilianDefinition def;
        float nextIdleSound;
        float footstepTimer;

        // model transform cache — die anims can displace the model child; restore on respawn
        Transform modelT;
        Vector3 modelLocalPos;
        Quaternion modelLocalRot;
        bool modelCached;

        static readonly int HitStateHash = Animator.StringToHash("Hit");
        static readonly int AttackStateHash = Animator.StringToHash("Attack");
        static readonly int TalkStateHash = Animator.StringToHash("Talk");
        static readonly int TaskStateHash = Animator.StringToHash("Task");

        [Tooltip("Seconds between footstep sounds at run speed")]
        public float footstepIntervalRun = 0.32f;
        public float footstepIntervalWalk = 0.55f;
        public Vector2 idleSoundInterval = new Vector2(8f, 20f);

        public Animator Animator => animator;

        public void Init(CivilianDefinition definition, Animator anim)
        {
            def = definition;
            animator = anim;
            source = GetComponent<AudioSource>();
            source.spatialBlend = 1f;   // 3D

            if (animator != null && !modelCached)
            {
                modelT = animator.transform;
                modelLocalPos = modelT.localPosition;
                modelLocalRot = modelT.localRotation;
                modelCached = true;
            }

            ScheduleIdleSound();
        }

        /// <summary>True while the Hit animation is playing or being transitioned into.</summary>
        public bool IsInHitAnim => InAnimState(HitStateHash);

        /// <summary>True while ANY one-shot anim (Hit/Attack/Talk/Task) plays or transitions in.
        /// States must finish before the brain moves on — prevents anim snapping.</summary>
        public bool IsInOneShotAnim =>
            InAnimState(HitStateHash) || InAnimState(AttackStateHash) ||
            InAnimState(TalkStateHash) || InAnimState(TaskStateHash);

        bool InAnimState(int hash)
        {
            if (animator == null || !animator.enabled) return false;
            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash) return true;
            return animator.IsInTransition(0) &&
                   animator.GetNextAnimatorStateInfo(0).shortNameHash == hash;
        }

        public void SetAnimatorEnabled(bool on)
        {
            if (animator != null) animator.enabled = on;
        }

        public void UpdateLocomotion(float speed, float runSpeed)
        {
            if (animator != null && animator.enabled)
                animator.SetFloat(SpeedHash, speed);

            // footsteps
            if (speed > 0.2f)
            {
                float interval = Mathf.Lerp(footstepIntervalWalk, footstepIntervalRun,
                    Mathf.InverseLerp(0f, runSpeed, speed));
                footstepTimer -= Time.deltaTime;
                if (footstepTimer <= 0f)
                {
                    PlayRandom(def.footstepClips, 0.5f);
                    footstepTimer = interval;
                }
            }

            // idle chatter
            if (speed < 0.2f && Time.time >= nextIdleSound)
            {
                PlayRandom(def.idleClips);
                ScheduleIdleSound();
            }
        }

        void ScheduleIdleSound() =>
            nextIdleSound = Time.time + Random.Range(idleSoundInterval.x, idleSoundInterval.y);

        public void TriggerAttack() { SetTrigger(AttackHash); PlayRandom(def.attackClips); }
        public void TriggerHit()    { SetTrigger(HitHash);    PlayRandom(def.hurtClips); }

        public void TriggerDie()
        {
            if (animator != null && animator.enabled)
            {
                // clear every other queued trigger — Die must win, nothing may follow it
                foreach (var p in animator.parameters)
                    if (p.type == AnimatorControllerParameterType.Trigger)
                        animator.ResetTrigger(p.nameHash);
                animator.SetTrigger(DieHash);
            }
            PlayRandom(def.deathClips);
        }

        /// <summary>Force Speed to 0 (death / frozen states) so locomotion doesn't blend.</summary>
        public void ResetLocomotion()
        {
            if (animator != null && animator.enabled) animator.SetFloat(SpeedHash, 0f);
        }

        /// <summary>
        /// Full reset for pooled respawn. Without this the animator stays stuck in the
        /// Die state (which intentionally has no exit) and old queued triggers fire.
        /// </summary>
        public void ResetForSpawn()
        {
            if (animator == null) return;
            animator.enabled = true;
            animator.Rebind();      // back to default state (Locomotion), all params default
            animator.Update(0f);

            // undo any displacement the die animation left on the model child
            if (modelCached && modelT != null)
            {
                modelT.localPosition = modelLocalPos;
                modelT.localRotation = modelLocalRot;
            }

            // clear any triggers queued during the previous life
            foreach (var p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger)
                    animator.ResetTrigger(p.nameHash);
        }

        static readonly int LocomotionStateHash = Animator.StringToHash("Locomotion");

        /// <summary>Hard-exit any one-shot state back to Locomotion (short crossfade) and
        /// flush all queued triggers. Interrupted conversations otherwise leave the partner
        /// finishing a full Talk clip at nobody — the ghost talker.</summary>
        public void ForceLocomotion(float fade = 0.15f)
        {
            if (animator == null || !animator.enabled) return;

            foreach (var p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger)
                    animator.ResetTrigger(p.nameHash);

            if (animator.HasState(0, LocomotionStateHash))
                animator.CrossFadeInFixedTime(LocomotionStateHash, fade, 0);
        }

        /// <summary>Remove a QUEUED trigger that hasn't fired yet. Triggers set while their
        /// state is already playing stay queued and fire later at random — ghost animations.</summary>
        public void ClearTrigger(string trigger)
        {
            if (animator == null || !animator.enabled || string.IsNullOrEmpty(trigger)) return;
            foreach (var p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == trigger)
                { animator.ResetTrigger(trigger); return; }
        }

        public void TriggerCustom(string trigger)
        {
            if (animator == null || !animator.enabled || string.IsNullOrEmpty(trigger)) return;

            // silent-fail guard: old controllers (built before Talk/Task existed) miss
            // these params — the NPC then looks like it never joins the conversation
            foreach (var p in animator.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == trigger)
                {
                    animator.SetTrigger(trigger);
                    return;
                }
            }
            Debug.LogWarning($"[CivilianAudioAnim] Animator on '{name}' has no trigger '{trigger}'. " +
                "Rebuild the controller with Tools > Civilian System > Animator Builder.", this);
        }

        void SetTrigger(int hash)
        {
            if (animator != null && animator.enabled) animator.SetTrigger(hash);
        }

        public void PlayClip(AudioClip clip, float volume = 1f)
        {
            if (clip != null && source != null) source.PlayOneShot(clip, volume);
        }

        void PlayRandom(AudioClip[] clips, float volume = 1f)
        {
            if (clips == null || clips.Length == 0) return;
            PlayClip(clips[Random.Range(0, clips.Length)], volume);
        }
    }
}
