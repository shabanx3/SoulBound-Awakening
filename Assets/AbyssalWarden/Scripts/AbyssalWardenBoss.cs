using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AbyssalWarden
{
    [RequireComponent(typeof(Animator))]
    public class AbyssalWardenBoss : MonoBehaviour
    {
        [Header("Target / Navigation")]
        public Transform player;
        public string playerTag = "Player";
        public float aggroRange = 24f;
        [Tooltip("Teleports are kept within about this distance of where the boss started. The boss never drops out of a fight.")]
        public float leashRange = 45f;
        public float meleeRange = 3.1f;
        public float desiredRange = 2.7f;
        public float turnSpeed = 450f;
        public float walkSpeed = 3.2f;
        public LayerMask targetLayers = ~0;
        [Tooltip("Layers the boss stands on when it has to move without the NavMesh; also stops projectiles.")]
        public LayerMask groundLayers = ~(1 << 20);
        [Tooltip("Seconds without progress toward the player before the boss stops relying on the NavMesh path.")]
        public float stuckSeconds = 1.2f;
        [Tooltip("Failsafe: an action running longer than this is cancelled so the boss can never freeze mid fight. Keep above the longest action.")]
        public float maxActionSeconds = 15f;

        [Header("Health / Phase")]
        public string bossName = "THE ABYSSAL WARDEN";
        public float maxHealth = 2400f;
        [Range(.1f,.9f)] public float phaseTwoThreshold = .5f;
        [Range(0f,.5f)] public float enrageThreshold = .25f;
        public float staggerThreshold = 260f;
        public float staggerRecoverySeconds = 4f;
        public float phaseTransitionDuration = 3.1f;
        public float phaseTwoDamageMultiplier = 1.5f;
        public bool invulnerableDuringTransition = true;

        [Header("Combat Tuning")]
        public float baseMeleeDamage = 32f;
        public float cooldownMin = 1.0f;
        public float cooldownMax = 1.7f;
        public float hitRadius = 1.5f;
        public float hitForwardDistance = 2.3f;
        [Tooltip("A swing only lands if the target is within this flat distance of the boss...")]
        public float meleeReach = 3.4f;
        [Tooltip("...and within this angle of where the boss faces.")]
        public float meleeHitAngle = 75f;
        public float spellSpeed = 14f;
        public float spellDamage = 40f;
        public float spellLifetime = 6f;
        public GameObject spellPrefab;
        public Transform castOrigin;
        public GameObject phaseTwoEffect;
        public AudioSource audioSource;
        public AudioClip attackSound, hurtSound, phaseSound, deathSound;

        [Header("Behaviour Mix")]
        [Range(0,1)] public float closeDodgeChance = .15f;
        [Range(0,1)] public float closeTauntChance = .06f;
        [Tooltip("While closing in, how often (seconds) the boss picks a ranged ability or gap closer.")]
        public Vector2 rangedDecisionInterval = new Vector2(2.2f, 3.8f);
        [Range(0,1)] public float reactiveDodgeChance = .15f;
        public float dodgeDistance = 3.2f;
        public float tauntDuration = 2.6f;

        [Header("Hit Feedback")]
        [Tooltip("Spawned where the player's weapon hits the boss.")]
        public GameObject hitEffect;
        public float hitEffectLifetime = 2f;
        [Range(0,1)] public float flinchChance = .3f;
        public float flinchCooldown = 2.5f;

        [Header("Ability: Claw Slashes (melee)")]
        [Tooltip("One per attack (Attack1..Attack4), spawned in front of the boss at the moment of impact.")]
        public GameObject[] slashEffects = new GameObject[4];
        public Vector3 slashOffset = new Vector3(0f, 1.3f, 1.4f);
        public float slashScale = 1.4f;

        [Header("Ability: Infernal Slam (AoE around the boss)")]
        public GameObject slamWarningEffect;
        public GameObject slamImpactEffect;
        public float slamRadius = 5.5f;
        public float slamDamage = 55f;
        public float slamWarningScale = 2.2f;
        public float slamCooldown = 9f;

        [Header("Ability: Hellfire Bolt / Black-Fire Volley")]
        public GameObject boltFlashEffect;
        public GameObject boltImpactEffect;
        public GameObject volleyProjectile;
        public GameObject volleyImpactEffect;
        public int volleyCount = 5;
        public float volleySpread = 14f;
        public float volleyCooldown = 10f;

        [Header("Ability: Meteor Rain")]
        public GameObject meteorWarningEffect;
        public GameObject meteorImpactEffect;
        public int meteorCount = 6;
        public float meteorScatter = 5f;
        public float meteorRadius = 2.6f;
        public float meteorDamage = 45f;
        public float meteorDelay = 1.4f;
        public float meteorWarningScale = 1.2f;
        public float meteorCooldown = 16f;
        [Tooltip("Red danger circle drawn where each meteor will land.")]
        public Color meteorWarningColor = new Color(1f, .08f, .02f);
        [Tooltip("Burning rock that falls onto each circle.")]
        public GameObject meteorFallEffect;
        public float meteorFallEffectScale = 1.5f;
        public float meteorFallHeight = 16f;
        public float meteorFallTime = .7f;
        [Tooltip("Flames left burning where a meteor landed.")]
        public GameObject meteorFireEffect;
        public float meteorBurnSeconds = 3f;
        public float meteorBurnDps = 12f;

        [Header("Ability: Hellfire Breath (cone)")]
        public GameObject breathEffect;
        public float breathRange = 7.5f;
        public float breathAngle = 32f;
        public float breathDps = 38f;
        public float breathDuration = 2.4f;
        public float breathCooldown = 12f;

        [Header("Ability: Infernal Charge (gap closer)")]
        public GameObject chargeTrailEffect;
        public GameObject chargeAuraEffect;
        public float chargeSpeed = 13f;
        public float chargeDamage = 40f;
        public float chargeMaxDistance = 14f;
        public float chargeCooldown = 8f;

        [Header("Ability: Roar of Ruin (phase two)")]
        public GameObject roarEffect;
        public float roarRadius = 6.5f;
        public float roarDamage = 30f;
        public float roarCooldown = 14f;

        [Header("Ability: Summon Skeletons")]
        [Tooltip("Emerald AI enemy prefab (e.g. Prefab/Enemy/Skelton Normal).")]
        public GameObject summonPrefab;
        public GameObject summonCircleEffect;
        public GameObject summonBurstEffect;
        public int summonCount = 2;
        public int summonMaxAlive = 4;
        public float summonRadius = 4f;
        public float summonDelay = 1.2f;
        public float summonCooldown = 22f;

        [Header("Ability: Abyssal Teleport")]
        public GameObject teleportOutEffect;
        [Tooltip("Shown at the destination just before the boss appears there.")]
        public GameObject teleportMarkerEffect;
        public GameObject teleportInEffect;
        public float teleportEffectScale = 2.2f;
        public float teleportBehindDistance = 3f;
        public float teleportHiddenTime = .6f;
        public float teleportCooldown = 11f;

        [Header("Ability: Infernal Ward (shield bubble + heal)")]
        public GameObject shieldEffect;
        public float shieldScale = 2.2f;
        public GameObject healEffect;
        public GameObject healAuraEffect;
        [Tooltip("The ward is cast once each time health drops below one of these fractions.")]
        public float[] healThresholds = {.6f, .3f};
        [Range(0,1)] public float healFraction = .12f;
        public float healDuration = 4f;
        [Range(0,1)] public float shieldDamageReduction = .85f;

        [Header("Phase Two")]
        [Tooltip("Aura attached to the boss for the rest of the fight once phase two starts.")]
        public GameObject phaseTwoAura;

        [Header("Model / Animation Events")]
        [Tooltip("Animator that plays the boss clips. Empty = the Animator on this object.")]
        public Animator animator;
        [Tooltip("Receives the Demons DEMO clip events (swipe trails, cast ball). Optional.")]
        public WardenDemonFX demonFX;

        [Header("Animation Timeline (seconds, tune to your clips)")]
        public float[] attackDurations = {1.45f,1.6f,1.85f,2.0f};
        public float[] attackHitTimes = {.57f,.66f,.88f,1.07f};
        public float[] attackDamageMultipliers = {1f,1.1f,1.35f,1.65f};

        public float Health { get; private set; }
        public float HealthNormalized => maxHealth <= 0f ? 0f : Health/maxHealth;
        public bool PhaseTwo { get; private set; }
        public bool Enraged => PhaseTwo && HealthNormalized <= enrageThreshold;
        public bool IsDead { get; private set; }
        public bool IsEngaged { get; private set; }

        [Header("Intro Cinematic")]
        [Tooltip("Play the 20 s boss intro (WardenIntroCinematic) the first time the player engages.")]
        public bool playIntroCinematic = true;
        /// <summary> Set by the intro cinematic: the boss holds still and cannot be hurt. </summary>
        public bool CinematicHold { get; set; }
        /// <summary> Engaged and past the intro: the real fight (used for the leadership encounter timer). </summary>
        public bool IsFighting => IsEngaged && !CinematicHold;
        public Transform Head => head;

        [Header("Ending Cinematic")]
        [Tooltip("Play the ending (WardenEndingCinematic) when the Warden dies; end-of-game UI waits for it.")]
        public bool playEndingCinematic = true;
        public string endingSpeakerName = "Knight";
        [Tooltip("Shot 3 shows lines 1-2 (to the fallen Warden), shot 4 shows lines 3-4 (gratitude). Empty = built-in lines.")]
        [TextArea] public string[] endingLines = {
            "It's over, Warden. Your darkness ends here.",
            "These halls were never yours to keep.",
            "To everyone who stood with me... thank you. I could not have done this alone.",
            "The castle is ours again. Our home is free.",
        };
        public string endingTitle = "THE CASTLE IS RECLAIMED";
        public string endingSubtitle = "Peace returns to the realm";
        [Tooltip("Light that breaks through as the darkness leaves the body.")]
        public GameObject endingLightEffect;
        public AudioClip endingVictorySound;
        [Tooltip("Malbers mode + ability the player plays while giving thanks (Action 4 / PowerUp 27).")]
        public int endingGestureModeID = 4;
        public int endingGestureAbility = 27;

        [Header("Music")]
        [Tooltip("Plays from the intro through the fight (and in the boss cinematics); the normal BGM is paused meanwhile.")]
        public AudioClip bossMusic;
        [Range(0f, 1f)] public float bossMusicVolume = .8f;

        [Header("Prologue Cinematic (New Game)")]
        [Tooltip("Portal to the dark realm the Warden walks out of.")]
        public GameObject prologuePortalEffect;
        [Tooltip("Enemy prefabs raised to take the town (cycled across the summoning circles).")]
        public GameObject[] prologueArmy;

        Animator anim;
        NavMeshAgent agent;
        Transform head;
        Vector3 home;
        bool busy, transitioning;
        float stagger;
        float lastStaggerTime;
        int attackIndex;
        Coroutine activeAction;
        GameObject channelFx, chargeTrail, chargeAura, wardShield, wardAura;
        bool shielded, hidden;
        int healsUsed;
        readonly List<GameObject> summons = new List<GameObject>();
        readonly Collider[] hits = new Collider[32];
        readonly RaycastHit[] groundHits = new RaycastHit[16];
        readonly Dictionary<string, float> readyAt = new Dictionary<string, float>();

        // Movement: NavMesh when the player is reachable, otherwise direct ground-snapped steering.
        bool directMode;
        float pathCheckTimer, progressTimer, bestDistance = float.MaxValue;
        float nextRangedDecision, nextFlinch, nextHurtVoice, nextReactiveDodge;
        float fallSpeed;
        float actionWatchStart;

        void Awake()
        {
            anim = animator != null ? animator : GetComponent<Animator>();
            if (demonFX == null) demonFX = GetComponentInChildren<WardenDemonFX>();
            agent = GetComponent<NavMeshAgent>();
            Health = maxHealth;
            if (agent != null) { agent.speed = walkSpeed; agent.stoppingDistance = desiredRange; agent.updateRotation = false; agent.autoBraking = true; }
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (anim != null) head = FindDeep(anim.transform, "Head");
            if (playIntroCinematic && GetComponent<WardenIntroCinematic>() == null) gameObject.AddComponent<WardenIntroCinematic>();
        }

        void OnEnable()
        {
            // The Warden is activated when the boss area is entered: put it on the NavMesh (or the ground) right away.
            directMode = !PlaceOnNavMesh(transform.position, 8f);
            if (directMode) SnapToGround(true);
            home = transform.position;
            ScheduleRangedDecision();
            Play("Idle");
        }

        void Update()
        {
            if (IsDead) return;
            WatchForStuckAction();
            WardenMusic.Set("boss", IsEngaged, bossMusic, bossMusicVolume);
            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag(playerTag);
                if (go != null) player = go.transform;
            }
            if (directMode && !busy) SnapToGround(false);
            if (player == null) { StopMoving(); Locomotion(false); return; }
            float d = FlatDistance(transform.position, player.position);
            if (!IsEngaged && d <= aggroRange) IsEngaged = true;
            if (CinematicHold) { StopMoving(); if (anim != null) anim.SetBool("Moving", false); return; }
            if (!IsEngaged) { Locomotion(false); return; }
            if (stagger > 0 && Time.time-lastStaggerTime > staggerRecoverySeconds) stagger = Mathf.Max(0,stagger-Time.deltaTime*staggerThreshold/staggerRecoverySeconds);
            if (busy) { StopMoving(); FacePlayer(); return; }
            FacePlayer();

            if (activeAction == null && NeedsWard) { StopMoving(); activeAction = StartCoroutine(RunAction(InfernalWard(d))); return; }
            if (activeAction == null && d <= meleeRange) { StopMoving(); activeAction = StartCoroutine(CloseAction()); return; }
            if (activeAction == null && Time.time >= nextRangedDecision && d > meleeRange + 1.5f && d < aggroRange)
            {
                ScheduleRangedDecision();
                IEnumerator ranged = PickRangedAction(d);
                if (ranged != null) { StopMoving(); activeAction = StartCoroutine(RunAction(ranged)); return; }
            }

            if (d > desiredRange) { Chase(d); Locomotion(true); }
            else { StopMoving(); Locomotion(false); }
        }

        // An action that dies with an exception never clears busy, which would leave the boss standing still for good.
        void WatchForStuckAction()
        {
            if (!busy && activeAction == null) { actionWatchStart = Time.time; return; }
            if (Time.time - actionWatchStart <= maxActionSeconds) return;
            Debug.LogWarning($"{bossName}: action did not finish within {maxActionSeconds:0}s, cancelling it.", this);
            StopAction();
            ResetProgress();
            actionWatchStart = Time.time;
        }

        // ---------------------------------------------------------------- animation
        // Actions are cross-faded by name and have no exit transitions, so locomotion is driven from here.
        void Locomotion(bool moving)
        {
            if (anim == null) return;
            anim.SetBool("Moving", moving);
            if (busy || anim.IsInTransition(0)) return;
            var state = anim.GetCurrentAnimatorStateInfo(0);
            if (moving) { if (!state.IsName("Walk")) Play("Walk", .2f); }
            else if (!state.IsName("Idle")) Play("Idle", .25f);
        }
        void Play(string state, float fade = .12f) { if(anim!=null) { anim.speed = 1f; anim.CrossFadeInFixedTime(state,fade,0); } }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root) { var found = FindDeep(child, name); if (found != null) return found; }
            return null;
        }

        // ---------------------------------------------------------------- movement
        static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

        bool AgentReady => agent != null && agent.enabled && agent.isOnNavMesh;

        bool PlaceOnNavMesh(Vector3 near, float radius)
        {
            if (agent == null) return false;
            if (!NavMesh.SamplePosition(near, out NavMeshHit hit, radius, NavMesh.AllAreas)) { agent.enabled = false; return false; }
            if (!agent.enabled) agent.enabled = true;
            agent.Warp(hit.position);
            agent.speed = walkSpeed; agent.stoppingDistance = desiredRange; agent.updateRotation = false;
            return agent.isOnNavMesh;
        }

        void Chase(float distance)
        {
            // Track progress: if the boss is not getting closer, the NavMesh path is not reaching the player.
            if (distance < bestDistance - .3f) { bestDistance = distance; progressTimer = 0; }
            else progressTimer += Time.deltaTime;

            pathCheckTimer -= Time.deltaTime;
            if (pathCheckTimer <= 0)
            {
                pathCheckTimer = .5f;
                if (directMode)
                {
                    // Return to the NavMesh once the player can be reached through it again.
                    if (NavMesh.SamplePosition(transform.position, out NavMeshHit selfHit, 1.5f, NavMesh.AllAreas) && PathComplete(selfHit.position, player.position))
                    { directMode = !PlaceOnNavMesh(selfHit.position, 1.5f); ResetProgress(); }
                }
                else if (!AgentReady || (!agent.pathPending && agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete) || progressTimer > stuckSeconds)
                {
                    EnterDirectMode();
                }
            }

            if (!directMode && AgentReady) { agent.isStopped = false; agent.SetDestination(player.position); return; }
            Vector3 target = new Vector3(player.position.x, transform.position.y, player.position.z);
            transform.position = Vector3.MoveTowards(transform.position, target, walkSpeed * Time.deltaTime);
        }

        static bool PathComplete(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        void EnterDirectMode()
        {
            directMode = true;
            if (agent != null && agent.enabled) { if (agent.isOnNavMesh) agent.ResetPath(); agent.enabled = false; }
            ResetProgress();
        }

        void ResetProgress() { progressTimer = 0; bestDistance = float.MaxValue; }

        // Keeps the boss on the ground when it is not driven by the NavMeshAgent (no hovering over slopes or drops).
        void SnapToGround(bool instant)
        {
            if (!GroundBelow(transform.position, out Vector3 ground))
            {
                fallSpeed += 9.81f * Time.deltaTime;
                transform.position += Vector3.down * fallSpeed * Time.deltaTime;
                return;
            }
            fallSpeed = 0;
            Vector3 p = transform.position;
            p.y = instant ? ground.y : Mathf.MoveTowards(p.y, ground.y, 12f * Time.deltaTime);
            transform.position = p;
        }

        bool GroundBelow(Vector3 position, out Vector3 ground)
        {
            int count = Physics.RaycastNonAlloc(position + Vector3.up * 2f, Vector3.down, groundHits, 40f, groundLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; ground = position; bool found = false;
            for (int i = 0; i < count; i++)
            {
                var h = groundHits[i];
                if (h.transform.IsChildOf(transform) || (player != null && h.transform.IsChildOf(player))) continue;
                if (h.distance < best) { best = h.distance; ground = h.point; found = true; }
            }
            return found;
        }

        Vector3 OnGround(Vector3 position) => GroundBelow(position, out Vector3 ground) ? ground : position;

        void Displace(Vector3 delta)
        {
            if (!directMode && AgentReady) agent.Move(delta);
            else { transform.position += delta; SnapToGround(false); }
        }

        void FacePlayer(float speedMultiplier = 1f)
        {
            if(player == null) return;
            Vector3 v = player.position-transform.position; v.y=0;
            if(v.sqrMagnitude>.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(v),turnSpeed*speedMultiplier*Time.deltaTime);
        }
        void StopMoving() { if(AgentReady) { agent.isStopped=true; agent.ResetPath(); } }
        void Sound(AudioClip clip)
        {
            // The demo clips voice themselves through animation events; avoid doubling them up.
            if(demonFX!=null && demonFX.handlesAudio) return;
            if(audioSource!=null && clip!=null) audioSource.PlayOneShot(clip);
        }
        void ScheduleRangedDecision() => nextRangedDecision = Time.time + Random.Range(rangedDecisionInterval.x, rangedDecisionInterval.y) * (Enraged ? .7f : 1f);

        bool Ready(string ability) => !readyAt.TryGetValue(ability, out float t) || Time.time >= t;
        void Cooldown(string ability, float seconds) => readyAt[ability] = Time.time + seconds * (Enraged ? .65f : PhaseTwo ? .85f : 1f);
        float Dmg(float value) => value * (PhaseTwo ? phaseTwoDamageMultiplier : 1f);

        // ---------------------------------------------------------------- action selection
        IEnumerator RunAction(IEnumerator action)
        {
            busy = true; StopMoving();
            yield return action;
            yield return new WaitForSeconds(Random.Range(cooldownMin,cooldownMax)*(PhaseTwo?.6f:1f)*.5f);
            EndAction();
        }

        void EndAction() { busy = false; activeAction = null; ResetProgress(); if (anim != null) anim.speed = 1f; }

        IEnumerator CloseAction()
        {
            busy = true; StopMoving();
            var options = new List<(float weight, System.Func<IEnumerator> run)>
            {
                (5f, MeleeCombo),
                (closeDodgeChance * 10f * (PhaseTwo ? 1.4f : 1f), () => Evade(false)),
            };
            if (!PhaseTwo) options.Add((closeTauntChance * 10f, () => Taunt(tauntDuration)));
            if (Ready("slam")) options.Add((2.5f, InfernalSlam));
            if (Ready("breath")) options.Add((1.4f, HellfireBreath));
            if (PhaseTwo && Ready("roar")) options.Add((1.6f, RoarOfRuin));
            if (PhaseTwo) options.Add((.8f, CastSpell));
            if (Ready("teleport")) options.Add((1.2f, () => AbyssalTeleport(true)));
            if (CanSummon) options.Add((1.5f, SummonSkeletons));
            yield return Pick(options)();
            yield return new WaitForSeconds(Random.Range(cooldownMin,cooldownMax)*(Enraged?.5f:PhaseTwo?.7f:1f));
            EndAction();
        }

        IEnumerator PickRangedAction(float d)
        {
            var options = new List<(float weight, System.Func<IEnumerator> run)> { (3f, null) }; // null = keep walking
            if (Ready("charge") && d >= 5f && d <= chargeMaxDistance + 2f) options.Add((3f, InfernalCharge));
            if (Ready("bolt")) options.Add((2f, CastSpell));
            if (PhaseTwo && Ready("volley")) options.Add((2.5f, BlackFireVolley));
            if (Ready("meteor") && (PhaseTwo || HealthNormalized < .8f)) options.Add((2f, MeteorRain));
            if (Ready("breath") && d < breathRange) options.Add((1.5f, HellfireBreath));
            if (Ready("teleport") && d > 6f) options.Add((2.2f, () => AbyssalTeleport(true)));
            if (CanSummon) options.Add((2f, SummonSkeletons));
            options.Add((PhaseTwo ? .5f : 1f, () => Taunt(tauntDuration)));
            var run = Pick(options);
            return run?.Invoke();
        }

        static System.Func<IEnumerator> Pick(List<(float weight, System.Func<IEnumerator> run)> options)
        {
            float total = 0; foreach (var o in options) total += Mathf.Max(0, o.weight);
            float roll = Random.value * total;
            foreach (var o in options) { roll -= Mathf.Max(0, o.weight); if (roll <= 0) return o.run; }
            return options[options.Count - 1].run;
        }

        // ---------------------------------------------------------------- melee
        IEnumerator MeleeCombo()
        {
            int index = attackIndex++ % 4;
            if(PhaseTwo && Random.value < .4f) index = Random.Range(2,4);
            yield return Melee(index);
            // Combo follow-ups while the player stays in front of him.
            int extra = Enraged ? 2 : 1;
            for (int i = 0; i < extra; i++)
            {
                if(player==null || FlatDistance(transform.position,player.position)>=meleeRange || Random.value >= (PhaseTwo ? .5f : .25f)) break;
                index = (index+1)%4;
                yield return Melee(index);
            }
            // Back off after a heavy swing now and then.
            if(index >= 2 && Random.value < .3f) yield return Evade(true);
        }

        IEnumerator Melee(int index)
        {
            index=Mathf.Clamp(index,0,3);
            if(demonFX!=null) demonFX.HideTrails();
            Play("Attack"+(index+1)); Sound(attackSound);
            float hit = Value(attackHitTimes,index,.6f), dur = Mathf.Max(hit+.08f, Value(attackDurations,index,1.5f));
            // Keep tracking the player during the wind-up so the swing lands where he is, not where he was.
            for (float t = 0; t < hit; t += Time.deltaTime) { FacePlayer(); yield return null; }
            SpawnSlash(index);
            Vector3 center = transform.position+transform.forward*Mathf.Min(hitForwardDistance, meleeReach*.6f)+Vector3.up*1.15f;
            WardenVFX.AreaDamage(transform, center, hitRadius, Dmg(baseMeleeDamage*Value(attackDamageMultipliers,index,1f)), targetLayers, hits, InSwingArc);
            yield return new WaitForSeconds(Mathf.Max(0,dur-hit));
        }

        void SpawnSlash(int index)
        {
            if (slashEffects == null || slashEffects.Length == 0) return;
            var prefab = slashEffects[Mathf.Min(index, slashEffects.Length - 1)];
            Vector3 pos = transform.TransformPoint(slashOffset);
            // Alternate the slash direction so consecutive swings read as different strikes.
            Quaternion rot = transform.rotation * Quaternion.Euler(0, 0, index % 2 == 0 ? 0f : 180f);
            WardenVFX.Spawn(prefab, pos, rot, 1.5f, null, slashScale);
        }

        // Only hits that are actually in front of the boss and within arm's reach count.
        bool InSwingArc(Collider target)
        {
            Vector3 closest = target.ClosestPoint(transform.position + Vector3.up * 1.15f);
            Vector3 flat = closest - transform.position; flat.y = 0;
            if (flat.magnitude > meleeReach) return false;
            return flat.sqrMagnitude < .25f || Vector3.Angle(transform.forward, flat) <= meleeHitAngle;
        }

        static float Value(float[] values,int i,float fallback) => values!=null && i<values.Length ? values[i] : fallback;

        // ---------------------------------------------------------------- abilities
        // Infernal Slam: a warning circle under the boss, then a blast that hits everything around him.
        IEnumerator InfernalSlam()
        {
            Cooldown("slam", slamCooldown);
            Vector3 ground = OnGround(transform.position);
            var warning = WardenVFX.Spawn(slamWarningEffect, ground + Vector3.up * .05f, Quaternion.identity, 0f, null, slamWarningScale);
            Play("Attack4"); Sound(attackSound);
            float hit = Value(attackHitTimes, 3, 1.07f);
            var marker = WardenGroundMarker.Create(ground, slamRadius, hit, meteorWarningColor);
            Destroy(marker.gameObject, hit + .1f);
            for (float t = 0; t < hit; t += Time.deltaTime) { FacePlayer(.5f); yield return null; }
            WardenVFX.StopAndDestroy(warning, .8f);
            WardenVFX.Spawn(slamImpactEffect, ground, Quaternion.identity, 4f);
            if (demonFX != null) demonFX.Voice("FootstepThump");
            WardenVFX.AreaDamage(transform, ground + Vector3.up, slamRadius, Dmg(slamDamage), targetLayers, hits);
            yield return new WaitForSeconds(Mathf.Max(.2f, Value(attackDurations, 3, 2f) - hit));
        }

        // Hellfire Breath: a cone of fire from the head that burns while the player stays in front.
        IEnumerator HellfireBreath()
        {
            Cooldown("breath", breathCooldown);
            Play("CastStart");
            for (float t = 0; t < .55f; t += Time.deltaTime) { FacePlayer(); yield return null; }
            Play("CastLoop");
            Transform mouth = head != null ? head : castOrigin != null ? castOrigin : transform;
            channelFx = WardenVFX.Spawn(breathEffect, mouth.position + transform.forward * .4f, transform.rotation, 0f, transform);
            float tick = 0;
            for (float t = 0; t < breathDuration; t += Time.deltaTime)
            {
                FacePlayer(.35f); // slow sweep: the player can dodge sideways
                if (channelFx != null) { channelFx.transform.position = mouth.position + transform.forward * .4f; channelFx.transform.rotation = transform.rotation; }
                tick -= Time.deltaTime;
                if (tick <= 0)
                {
                    tick = .35f;
                    Vector3 origin = transform.position + Vector3.up * 1.2f;
                    WardenVFX.AreaDamage(transform, origin + transform.forward * breathRange * .5f, breathRange * .55f, Dmg(breathDps * .35f), targetLayers, hits, col =>
                    {
                        Vector3 to = col.ClosestPoint(origin) - transform.position; to.y = 0;
                        return to.magnitude <= breathRange && Vector3.Angle(transform.forward, to) <= breathAngle;
                    });
                }
                yield return null;
            }
            WardenVFX.StopAndDestroy(channelFx); channelFx = null;
            if (demonFX != null) demonFX.ResetFX();
            Play("CastEnd");
            yield return new WaitForSeconds(.5f);
        }

        // Infernal Charge: a burning dash at the player that ends in a swing.
        IEnumerator InfernalCharge()
        {
            Cooldown("charge", chargeCooldown);
            Play("Taunt", .1f);
            for (float t = 0; t < .45f; t += Time.deltaTime) { FacePlayer(2f); yield return null; }
            chargeAura = WardenVFX.Spawn(chargeAuraEffect, transform.position, transform.rotation, 0f, transform);
            chargeTrail = WardenVFX.Spawn(chargeTrailEffect, transform.position + Vector3.up * 1.2f, transform.rotation, 0f, transform);
            Play("Walk", .08f);
            anim.speed = 2.4f;
            bool struck = false;
            float travelled = 0, maxTime = chargeMaxDistance / chargeSpeed + .3f;
            for (float t = 0; t < maxTime && player != null; t += Time.deltaTime)
            {
                FacePlayer(.6f);
                float d = FlatDistance(transform.position, player.position);
                if (d <= 1.8f || travelled >= chargeMaxDistance) break;
                float step = chargeSpeed * Time.deltaTime;
                Displace(transform.forward * step); travelled += step;
                if (!struck && WardenVFX.AreaDamage(transform, transform.position + transform.forward + Vector3.up, 1.3f, Dmg(chargeDamage), targetLayers, hits) > 0) struck = true;
                yield return null;
            }
            anim.speed = 1f;
            ClearChargeFx();
            if (player != null && FlatDistance(transform.position, player.position) <= meleeRange + .5f) yield return Melee(1);
        }

        // Meteor Rain: burning circles appear around the player, then fire crashes down on each.
        IEnumerator MeteorRain()
        {
            Cooldown("meteor", meteorCooldown);
            Play("CastStart");
            for (float t = 0; t < .55f; t += Time.deltaTime) { FacePlayer(); yield return null; }
            Play("CastLoop");
            int count = meteorCount + (Enraged ? 3 : 0);
            for (int i = 0; i < count && player != null; i++)
            {
                // First one on the player, the rest scattered around (and ahead of) them.
                Vector3 spot = player.position;
                if (i > 0) { Vector2 r = Random.insideUnitCircle * meteorScatter; spot += new Vector3(r.x, 0, r.y); }
                StartCoroutine(MeteorStrike(OnGround(spot)));
                FacePlayer();
                yield return new WaitForSeconds(.22f);
            }
            if (demonFX != null) demonFX.ResetFX();
            Play("CastEnd");
            yield return new WaitForSeconds(.6f);
        }

        // One meteor: red circle fills up, a burning rock falls onto it, explodes, and leaves fire behind.
        IEnumerator MeteorStrike(Vector3 spot)
        {
            var marker = WardenGroundMarker.Create(spot, meteorRadius, meteorDelay, meteorWarningColor);
            var warning = WardenVFX.Spawn(meteorWarningEffect, spot + Vector3.up * .05f, Quaternion.identity, 0f, null, meteorWarningScale);
            float fall = Mathf.Clamp(meteorFallTime, .1f, meteorDelay);
            yield return new WaitForSeconds(meteorDelay - fall);

            Vector3 side = Vector3.ProjectOnPlane(spot - transform.position, Vector3.up).normalized;
            Vector3 start = spot + Vector3.up * meteorFallHeight - side * meteorFallHeight * .35f;
            var rock = WardenVFX.Spawn(meteorFallEffect, start, Quaternion.LookRotation(spot - start), 0f, null, meteorFallEffectScale);
            if (rock != null) WardenVFX.StripMovers(rock);
            for (float t = 0; t < fall; t += Time.deltaTime)
            {
                if (rock != null) rock.transform.position = Vector3.Lerp(start, spot, t / fall);
                yield return null;
            }
            WardenVFX.StopAndDestroy(rock, .4f);
            WardenVFX.StopAndDestroy(warning, .3f);
            if (marker != null) Destroy(marker.gameObject);
            if (IsDead) yield break;

            WardenVFX.Spawn(meteorImpactEffect, spot, Quaternion.identity, 4f);
            WardenVFX.AreaDamage(transform, spot + Vector3.up * .8f, meteorRadius, Dmg(meteorDamage), targetLayers, hits);
            if (meteorBurnSeconds > 0f) StartCoroutine(BurnArea(spot, meteorRadius * .75f, meteorBurnSeconds, meteorBurnDps));
        }

        IEnumerator BurnArea(Vector3 spot, float radius, float seconds, float dps)
        {
            var fire = WardenVFX.Spawn(meteorFireEffect, spot, Quaternion.identity, 0f);
            for (float t = 0; t < seconds && !IsDead; t += .5f)
            {
                WardenVFX.AreaDamage(transform, spot + Vector3.up * .8f, radius, Dmg(dps * .5f), targetLayers, hits);
                yield return new WaitForSeconds(.5f);
            }
            WardenVFX.StopAndDestroy(fire, 1.2f);
        }

        // ---------------------------------------------------------------- summon / teleport / ward
        bool CanSummon
        {
            get
            {
                summons.RemoveAll(x => x == null);
                return summonPrefab != null && Ready("summon") && summons.Count < summonMaxAlive;
            }
        }

        // Summon Skeletons: pentagrams open around the boss and skeleton warriors climb out.
        IEnumerator SummonSkeletons()
        {
            Cooldown("summon", summonCooldown);
            Play("Taunt"); Sound(phaseSound);
            int count = Mathf.Min(summonCount + (Enraged ? 1 : 0), summonMaxAlive - summons.Count);
            Vector3 toward = player != null ? Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up).normalized : transform.forward;
            if (toward.sqrMagnitude < .01f) toward = transform.forward;
            var spots = new List<Vector3>();
            for (int i = 0; i < count; i++)
            {
                float angle = (i - (count - 1) * .5f) * 50f;
                Vector3 spot = transform.position + Quaternion.AngleAxis(angle, Vector3.up) * toward * summonRadius;
                if (!NavMesh.SamplePosition(spot, out NavMeshHit hit, 3f, NavMesh.AllAreas)) continue;
                spots.Add(hit.position);
                WardenVFX.Spawn(summonCircleEffect, hit.position + Vector3.up * .05f, Quaternion.identity, summonDelay + 1.5f);
            }
            for (float t = 0; t < summonDelay; t += Time.deltaTime) { FacePlayer(); yield return null; }
            foreach (var spot in spots)
            {
                WardenVFX.Spawn(summonBurstEffect, spot + Vector3.up * .5f, Quaternion.identity, 2.5f, null, 2f);
                Vector3 face = player != null ? Vector3.ProjectOnPlane(player.position - spot, Vector3.up) : transform.forward;
                var skeleton = Instantiate(summonPrefab, spot, face.sqrMagnitude > .01f ? Quaternion.LookRotation(face) : transform.rotation);
                var death = skeleton.GetComponent<EnemyDeathEffect>();
                if (death != null) death.CountsTowardLeadership = false;
                summons.Add(skeleton);
            }
            yield return new WaitForSeconds(Mathf.Max(.3f, tauntDuration - summonDelay));
        }

        // Abyssal Teleport: vanishes in black fire and reappears behind the player (aggressive) or away from them.
        IEnumerator AbyssalTeleport(bool aggressive)
        {
            Cooldown("teleport", teleportCooldown);
            if (player == null) yield break;
            Play("Dodge");
            yield return new WaitForSeconds(.2f);

            Vector3 dest;
            if (aggressive) dest = player.position - Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized * teleportBehindDistance;
            else { Vector2 r = Random.insideUnitCircle.normalized * Random.Range(8f, 11f); dest = player.position + new Vector3(r.x, 0, r.y); }
            if (FlatDistance(dest, home) > leashRange * .8f) dest = Vector3.Lerp(home, dest, .5f);
            dest = NavMesh.SamplePosition(dest, out NavMeshHit navHit, 4f, NavMesh.AllAreas) ? navHit.position : OnGround(dest);

            WardenVFX.Spawn(teleportOutEffect, transform.position + Vector3.up, transform.rotation, 2f, null, teleportEffectScale);
            var marker = WardenVFX.Spawn(teleportMarkerEffect, dest + Vector3.up * .05f, Quaternion.identity, 0f);
            SetVisible(false);
            yield return new WaitForSeconds(teleportHiddenTime);

            directMode = !PlaceOnNavMesh(dest, 2f);
            if (directMode) { transform.position = dest; SnapToGround(true); }
            Vector3 look = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up);
            if (look.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(look);
            WardenVFX.StopAndDestroy(marker, .5f);
            WardenVFX.Spawn(teleportInEffect, transform.position + Vector3.up, transform.rotation, 2.5f, null, teleportEffectScale);
            SetVisible(true);

            if (aggressive && FlatDistance(transform.position, player.position) <= meleeRange + 1f)
                yield return Ready("slam") && Random.value < .4f ? InfernalSlam() : Melee(Random.Range(1, 4));
            else if (aggressive) yield return CastSpell();
        }

        void SetVisible(bool visible)
        {
            hidden = !visible;
            if (anim != null && anim.gameObject != gameObject) anim.gameObject.SetActive(visible);
            if (visible) Play("Idle", .05f);
        }

        bool NeedsWard => healThresholds != null && healsUsed < healThresholds.Length && HealthNormalized <= healThresholds[healsUsed] && !transitioning;

        // Infernal Ward: a fiery bubble absorbs most damage while the boss channels a heal.
        IEnumerator InfernalWard(float distance)
        {
            healsUsed++;
            if (distance < 6f) yield return AbyssalTeleport(false); // step away from the player first
            shielded = true;
            wardShield = WardenVFX.Spawn(shieldEffect, transform.position + Vector3.up * 1.3f, transform.rotation, 0f, transform, shieldScale);
            wardAura = WardenVFX.Spawn(healAuraEffect, transform.position, transform.rotation, 0f, transform);
            WardenVFX.Spawn(healEffect, transform.position, transform.rotation, 4f, transform);
            Play("CastStart");
            yield return new WaitForSeconds(.55f);
            Play("CastLoop");
            float healPerSecond = healFraction * maxHealth / Mathf.Max(.1f, healDuration);
            for (float t = 0; t < healDuration && !IsDead; t += Time.deltaTime)
            {
                Health = Mathf.Min(maxHealth, Health + healPerSecond * Time.deltaTime);
                FacePlayer(.5f);
                yield return null;
            }
            if (demonFX != null) demonFX.ResetFX();
            Play("CastEnd");
            ClearWard();
            yield return new WaitForSeconds(.5f);
        }

        void ClearWard()
        {
            shielded = false;
            WardenVFX.StopAndDestroy(wardShield); WardenVFX.StopAndDestroy(wardAura);
            wardShield = null; wardAura = null;
        }

        // Black-Fire Volley: a fan of projectiles thrown from the hand.
        IEnumerator BlackFireVolley()
        {
            Cooldown("volley", volleyCooldown);
            Play("CastStart"); yield return new WaitForSeconds(.55f);
            Play("CastLoop");
            for (float t = 0; t < .5f; t += Time.deltaTime) { FacePlayer(); yield return null; }
            Play("CastEnd");
            float t2 = 0;
            while (demonFX != null && !demonFX.CastReleased && t2 < .75f) { t2 += Time.deltaTime; FacePlayer(); yield return null; }
            if (demonFX != null) demonFX.ResetFX();
            if (player != null)
            {
                Vector3 origin = castOrigin != null ? castOrigin.position : transform.position + Vector3.up * 1.7f + transform.forward;
                Vector3 aim = (player.position + Vector3.up * 1.1f - origin).normalized;
                int count = volleyCount + (Enraged ? 2 : 0);
                WardenVFX.Spawn(boltFlashEffect, origin, Quaternion.LookRotation(aim), 1.5f);
                for (int i = 0; i < count; i++)
                {
                    float yaw = count == 1 ? 0 : Mathf.Lerp(-volleySpread, volleySpread, i / (float)(count - 1));
                    Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * aim;
                    FireProjectile(volleyProjectile, origin, dir, spellSpeed * .9f, spellDamage * .6f, volleyImpactEffect);
                }
            }
            yield return new WaitForSeconds(.45f);
        }

        // Roar of Ruin: the taunt becomes a fire nova around the boss.
        IEnumerator RoarOfRuin()
        {
            Cooldown("roar", roarCooldown);
            Play("Taunt"); Sound(phaseSound);
            for (float t = 0; t < .9f; t += Time.deltaTime) { FacePlayer(); yield return null; }
            Vector3 ground = OnGround(transform.position);
            WardenVFX.Spawn(roarEffect, ground, Quaternion.identity, 3.5f);
            WardenVFX.AreaDamage(transform, ground + Vector3.up, roarRadius, Dmg(roarDamage), targetLayers, hits);
            yield return new WaitForSeconds(Mathf.Max(.3f, tauntDuration - .9f));
        }

        IEnumerator Evade(bool backOnly)
        {
            // Back-step or side-step, whichever way is open.
            Vector3 dir = -transform.forward;
            string state = "JumpBack";
            if (!backOnly && Random.value < .6f) { dir = (Random.value < .5f ? transform.right : -transform.right); state = "Dodge"; }
            else if (Random.value < .5f) state = "Dodge";
            if (Physics.Raycast(transform.position + Vector3.up, dir, dodgeDistance + .5f, groundLayers, QueryTriggerInteraction.Ignore)) dir = -dir;
            Play(state);
            float duration=.6f, speed=dodgeDistance/duration;
            for (float t=0; t<duration; t+=Time.deltaTime) { Displace(dir*speed*Time.deltaTime); yield return null; }
            yield return new WaitForSeconds(.25f);
        }

        IEnumerator Taunt(float duration)
        {
            Play("Taunt"); Sound(phaseSound);
            for (float t = 0; t < duration; t += Time.deltaTime) { FacePlayer(); yield return null; }
        }

        // Hellfire Bolt: the demon's hand-held ball (or spellPrefab) thrown at the player.
        IEnumerator CastSpell()
        {
            Cooldown("bolt", 4f);
            Play("CastStart"); yield return new WaitForSeconds(.55f);
            Play("CastLoop");
            for (float t = 0; t < .8f; t += Time.deltaTime) { FacePlayer(); yield return null; }
            if(demonFX==null)
            {
                LaunchSpell(null);
                Play("CastEnd"); yield return new WaitForSeconds(.6f);
                yield break;
            }
            // Demon model: release the hand-held ball on the clip's ThrowCast event.
            Play("CastEnd");
            float t2=0;
            while(!demonFX.CastReleased && t2<.75f) { t2+=Time.deltaTime; FacePlayer(); yield return null; }
            LaunchSpell(demonFX.TakeCast());
            yield return new WaitForSeconds(.45f);
        }
        void LaunchSpell(GameObject heldProjectile)
        {
            if(player==null) { if(heldProjectile!=null) Destroy(heldProjectile); return; }
            Vector3 origin=heldProjectile!=null?heldProjectile.transform.position:castOrigin!=null?castOrigin.position:transform.position+Vector3.up*1.7f+transform.forward;
            Vector3 direction=(player.position+Vector3.up*1.1f-origin).normalized;
            WardenVFX.Spawn(boltFlashEffect, origin, Quaternion.LookRotation(direction), 1.5f);
            GameObject projectile=heldProjectile;
            if(projectile!=null) projectile.transform.rotation=Quaternion.LookRotation(direction);
            else if(spellPrefab!=null) projectile=Instantiate(spellPrefab,origin,Quaternion.LookRotation(direction));
            else { projectile=GameObject.CreatePrimitive(PrimitiveType.Sphere); projectile.transform.position=origin; projectile.transform.localScale=Vector3.one*.45f; var renderer=projectile.GetComponent<Renderer>(); if(renderer) renderer.material.color=new Color(.8f,.1f,.08f); }
            var spell=projectile.GetComponent<WardenProjectile>(); if(spell==null) spell=projectile.AddComponent<WardenProjectile>();
            spell.Initialize(transform,direction,spellSpeed,Dmg(spellDamage),spellLifetime,targetLayers,groundLayers,boltImpactEffect);
        }

        void FireProjectile(GameObject prefab, Vector3 origin, Vector3 direction, float speed, float damage, GameObject impact)
        {
            GameObject projectile = prefab != null ? Instantiate(prefab, origin, Quaternion.LookRotation(direction)) : null;
            if (projectile == null) { projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere); projectile.transform.position = origin; projectile.transform.localScale = Vector3.one * .35f; Destroy(projectile.GetComponent<Collider>()); }
            var spell = projectile.GetComponent<WardenProjectile>(); if (spell == null) spell = projectile.AddComponent<WardenProjectile>();
            spell.Initialize(transform, direction, speed, Dmg(damage), spellLifetime, targetLayers, groundLayers, impact);
        }

        // ---------------------------------------------------------------- damage
        public void ReceiveDamage(float amount, Transform attacker) => TakeDamage(amount,amount*.45f);
        public void TakeDamage(float damage,float staggerDamage=0f) => TakeDamage(damage, staggerDamage, null);
        public void TakeDamage(float damage,float staggerDamage,Vector3? hitPoint)
        {
            if(IsDead || hidden || CinematicHold || (transitioning&&invulnerableDuringTransition)) return;
            if(shielded) damage *= 1f - shieldDamageReduction;
            Health=Mathf.Max(0,Health-Mathf.Max(0,damage));
            HitFeedback(hitPoint);
            if(Health<=0) { Die(); return; }
            if(!IsEngaged) IsEngaged=true;
            if(shielded) return; // the ward cannot be interrupted
            if(!PhaseTwo && HealthNormalized<=phaseTwoThreshold) { StopAction(); activeAction=StartCoroutine(PhaseTransition()); return; }
            stagger+=staggerDamage; lastStaggerTime=Time.time;
            if(stagger>=staggerThreshold && !transitioning) { stagger=0; StopAction(); activeAction=StartCoroutine(Stagger(1.15f)); return; }
            if(transitioning) return;
            // Light reactions: a quick flinch or, now and then, a dodge away from the attacker.
            if(!busy && Time.time>=nextReactiveDodge && Random.value<reactiveDodgeChance)
            {
                nextReactiveDodge=Time.time+4f; StopAction(); activeAction=StartCoroutine(RunAction(Evade(false)));
            }
            else if(!busy && Time.time>=nextFlinch && Random.value<flinchChance)
            {
                nextFlinch=Time.time+flinchCooldown; StopAction(); activeAction=StartCoroutine(Stagger(.6f));
            }
        }

        void HitFeedback(Vector3? hitPoint)
        {
            Vector3 point = hitPoint ?? transform.position + Vector3.up * 1.4f;
            if (hitEffect != null)
            {
                Vector3 axis = transform.position; axis.y = point.y;
                Vector3 outward = point - axis;
                WardenVFX.Spawn(hitEffect, point, outward.sqrMagnitude > .001f ? Quaternion.LookRotation(outward) : transform.rotation, hitEffectLifetime);
            }
            if (demonFX != null)
            {
                demonFX.HitFlash();
                if (Time.time >= nextHurtVoice) { nextHurtVoice = Time.time + 1.2f; demonFX.Voice("VocalizationGotHit"); }
            }
            else if (Time.time >= nextHurtVoice) { nextHurtVoice = Time.time + 1.2f; if (audioSource != null && hurtSound != null) audioSource.PlayOneShot(hurtSound); }
        }

        /// <summary> Player shockwave: interrupts the boss and leaves him dazed. False when he is immune (ward, phase change, teleport). </summary>
        public bool Stun(float seconds)
        {
            if (IsDead || hidden || shielded || transitioning || seconds <= 0f) return false;
            StopAction();
            activeAction = StartCoroutine(Stunned(seconds));
            return true;
        }

        IEnumerator Stunned(float seconds)
        {
            busy = true; StopMoving(); Play("HitReaction");
            yield return new WaitForSeconds(Mathf.Min(.6f, seconds));
            if (anim != null) anim.speed = .25f; // dazed: hold the end of the reaction
            yield return new WaitForSeconds(Mathf.Max(0f, seconds - .6f));
            EndAction();
        }

        IEnumerator Stagger(float duration) { busy=true; StopMoving(); Play("HitReaction"); Sound(hurtSound); yield return new WaitForSeconds(duration); EndAction(); }
        IEnumerator PhaseTransition()
        {
            busy=true; transitioning=true; PhaseTwo=true; StopMoving(); Play("Taunt"); Sound(phaseSound);
            if(phaseTwoEffect!=null) Instantiate(phaseTwoEffect,transform.position,transform.rotation,transform);
            if(phaseTwoAura!=null) WardenVFX.Spawn(phaseTwoAura,transform.position,transform.rotation,0f,transform);
            yield return new WaitForSeconds(.9f);
            // The phase change itself erupts as a Roar of Ruin.
            Vector3 ground = OnGround(transform.position);
            WardenVFX.Spawn(roarEffect, ground, Quaternion.identity, 3.5f);
            WardenVFX.AreaDamage(transform, ground + Vector3.up, roarRadius, roarDamage, targetLayers, hits);
            yield return new WaitForSeconds(Mathf.Max(0, phaseTransitionDuration - .9f));
            transitioning=false; EndAction();
        }
        void StopAction()
        {
            if(activeAction!=null) StopCoroutine(activeAction);
            activeAction=null; busy=false; transitioning=false;
            if(anim!=null) anim.speed=1f;
            if(channelFx!=null) { WardenVFX.StopAndDestroy(channelFx); channelFx=null; }
            ClearChargeFx();
            if(shielded || wardShield!=null) ClearWard();
            if(hidden) SetVisible(true);
            if(demonFX!=null) demonFX.ResetFX();
        }
        // ---------------------------------------------------------------- cinematic hooks
        /// <summary> Cross-fades to an animator state (Idle, Taunt, CastStart, CastLoop, CastEnd, IdleBreak...). </summary>
        public void CinematicPlay(string state, float fade = .2f) => Play(state, fade);
        /// <summary> Plays one of the demon's voice groups (e.g. "VocalizationTaunt"). </summary>
        public void CinematicVoice(string group) { if (demonFX != null) demonFX.Voice(group); }
        public void CinematicFace(Vector3 point, float speed = 1f)
        {
            Vector3 v = point - transform.position; v.y = 0;
            if (v.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(v), turnSpeed * speed * Time.deltaTime);
        }
        public void CinematicEnd() { if (demonFX != null) demonFX.ResetFX(); Play("Idle", .3f); ResetProgress(); }

        void ClearChargeFx()
        {
            WardenVFX.StopAndDestroy(chargeTrail); WardenVFX.StopAndDestroy(chargeAura);
            chargeTrail = null; chargeAura = null;
        }
        void Die()
        {
            IsDead=true; IsEngaged=false; StopAction();
            if(playEndingCinematic) WardenEndingCinematic.Play(this);
            WardenMusic.Set("boss", false); // the ending (if any) keeps the music going a little longer
            StopMoving(); if(anim!=null) anim.SetBool("Moving",false); Play("Death"); Sound(deathSound);
            if(directMode) SnapToGround(true);
            if(agent!=null) agent.enabled=false;
            foreach(var col in GetComponentsInChildren<Collider>()) col.enabled=false;
            // Reward: a few heal orbs.
            for (int i = 0; i < PlayerCombatBootstrap.Config.bossHealDrops; i++) HealDrop.Spawn(transform.position + Vector3.up);
            // His summons crumble with him.
            foreach(var summon in summons) if(summon!=null) { WardenVFX.Spawn(summonBurstEffect, summon.transform.position + Vector3.up * .5f, Quaternion.identity, 2.5f, null, 2f); Destroy(summon); }
            summons.Clear();
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.yellow; Gizmos.DrawWireSphere(transform.position,aggroRange);
            Gizmos.color=Color.red; Gizmos.DrawWireSphere(transform.position+transform.forward*Mathf.Min(hitForwardDistance, meleeReach*.6f)+Vector3.up*1.15f,hitRadius);
            Gizmos.color=new Color(1,.5f,0); Gizmos.DrawWireSphere(transform.position,meleeReach);
            Gizmos.color=new Color(1,.2f,0,.5f); Gizmos.DrawWireSphere(transform.position,slamRadius);
        }
    }
    public interface IWardenDamageable { void ReceiveDamage(float amount,Transform attacker); }
}
