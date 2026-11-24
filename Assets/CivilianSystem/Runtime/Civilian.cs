// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEngine;
using UnityEngine.AI;

namespace CivilianSystem
{
    public enum CivilianState { Idle, Wander, Patrol, Task, Flee, Chase, Attack, Stagger, Recover, GoToPoint, PerformPoint, Converse, Search, Arrest, Dead }

    /// <summary>
    /// Central brain. Composes movement, health, tasks, audio/anim.
    /// Behaviors:
    ///   Passive    — ignores threats, keeps doing its thing, plays hit react only.
    ///   Coward     — flees from anything that damages it or from armed player in range.
    ///   Aggressive — chases + attacks its threat.
    /// </summary>
    [RequireComponent(typeof(CivilianMovement))]
    [RequireComponent(typeof(CivilianHealth))]
    [RequireComponent(typeof(CivilianTaskScheduler))]
    [RequireComponent(typeof(CivilianAudioAnim))]
    public class Civilian : MonoBehaviour
    {
        public CivilianDefinition definition;
        [Tooltip("Optional patrol path. If set, civilian patrols instead of wandering.")]
        public WaypointPath path;

        [HideInInspector] public CivilianPopulationManager population; // set by manager

        public CivilianState State { get; private set; } = CivilianState.Idle;
        public bool AIEnabled { get; private set; } = true;

        CivilianMovement movement;
        CivilianHealth health;
        CivilianTaskScheduler tasks;
        CivilianAudioAnim av;

        // runtime
        GameObject threat;
        Vector3 homePosition;
        float stateTimer;
        float nextAttackTime;
        int pathIndex, pathDirection = 1;
        CivilianTask currentTask;

        // ---------- global events (see CivilianSystemListener for inspector wiring) ----------
        public static event System.Action<Civilian, float> OnAnyDamaged;
        public static event System.Action<Civilian> OnAnyDeath;
        public static event System.Action<Civilian> OnPlayerTalk;

        // social
        public static readonly System.Collections.Generic.List<Civilian> AllCivilians = new();
        TaskPoint claimedPoint;
        Civilian chatPartner;
        Transform playerChat;          // player conversation target
        bool chatMyTurn;               // turn-based chit-chat
        float chatTurnTimer;
        float socialCheckTimer;
        float nextConversationTime;
        CivilianWorldUI ui;

        // police
        float arrestTimer;
        float searchTimer;
        float yieldTimer;      // polite stop when player bumps in
        float fleeRepathTimer; // commit-to-direction timer while fleeing

        void OnEnable() => AllCivilians.Add(this);
        void OnDisable() => AllCivilians.Remove(this);

        /// <summary>Free for a chat / task point? Only during routine states.</summary>
        public bool IsSociallyAvailable =>
            AIEnabled && !health.IsDead && Time.time >= nextConversationTime &&
            (State == CivilianState.Idle || State == CivilianState.Wander || State == CivilianState.Patrol);

        public CivilianHealth Health => health;
        public CivilianTaskScheduler Tasks => tasks;

        bool refsReady;
        bool eventsHooked;

        void Awake() => EnsureRefs();

        /// <summary>Safe to call any time. Pool activation order makes Awake timing unreliable.</summary>
        void EnsureRefs()
        {
            if (refsReady) return;

            // self-heal: broken prefabs (dead script GUIDs after re-import) get components re-added
            movement = GetComponent<CivilianMovement>();      if (movement == null) movement = gameObject.AddComponent<CivilianMovement>();
            health   = GetComponent<CivilianHealth>();        if (health == null)   health   = gameObject.AddComponent<CivilianHealth>();
            tasks    = GetComponent<CivilianTaskScheduler>(); if (tasks == null)    tasks    = gameObject.AddComponent<CivilianTaskScheduler>();
            av       = GetComponent<CivilianAudioAnim>();     if (av == null)       av       = gameObject.AddComponent<CivilianAudioAnim>();
            ui       = GetComponent<CivilianWorldUI>();       if (ui == null)       ui       = gameObject.AddComponent<CivilianWorldUI>();
            if (GetComponent<AudioSource>() == null) gameObject.AddComponent<AudioSource>().playOnAwake = false;

            refsReady = true;

            if (!eventsHooked)
            {
                health.OnDamaged += HandleDamaged;
                health.OnDeath += HandleDeath;
                eventsHooked = true;
            }
        }

        /// <summary>Called by pool/population manager on spawn (also works standalone via Start).</summary>
        public void Spawn(Vector3 position)
        {
            EnsureRefs();
            if (!refsReady) return;

            if (definition == null)
            {
                Debug.LogError($"[Civilian] '{name}' has no CivilianDefinition assigned.", this);
                return;
            }

            // snap to the NavMesh — spawn coordinates often float above or beside the
            // baked surface, which throws "Failed to create agent because it is not
            // close enough to the NavMesh". Generous 10 m vertical/lateral search.
            if (NavMesh.SamplePosition(position, out var navHit, 10f, NavMesh.AllAreas))
                position = navHit.position;
            else
                Debug.LogWarning($"[Civilian] Spawn position {position} is nowhere near the NavMesh. " +
                    "Move the PopulationManager / spawn points onto the baked (blue) area.", this);

            transform.position = position;
            homePosition = position;

            movement.Init(definition);
            movement.SetEnabled(true);
            movement.Warp(position); // proper agent placement, resets internal path state
            health.Init(definition.maxHealth);
            av.Init(definition, GetComponentInChildren<Animator>());
            av.ResetForSpawn(); // pooled respawn: un-stick from Die state, clear stale triggers

            tasks.ResetToInitial();
            threat = null;
            currentTask = null;
            claimedPoint = null;
            chatPartner = null;
            playerChat = null;
            nextConversationTime = 0f;
            socialCheckTimer = Random.Range(0f, 1f);
            ui.uiPrefab = definition.uiPrefab; // custom canvas prefab if assigned
            ui.Build();
            ui.HideAll();
            if (path != null && path.Count > 0)
                pathIndex = Random.Range(0, path.Count); // spread patrol group along route
            SetState(tasks.HasTasks ? CivilianState.Task
                   : path != null && path.Count > 0 ? CivilianState.Patrol
                   : CivilianState.Idle);
        }

        void Start()
        {
            // standalone support (placed in scene without population manager)
            EnsureRefs();
            if (refsReady && population == null && health.MaxHealth <= 0f)
                Spawn(transform.position);
        }

        void OnDestroy()
        {
            if (eventsHooked && health != null)
            {
                health.OnDamaged -= HandleDamaged;
                health.OnDeath -= HandleDeath;
            }
        }

        /// <summary>Culling system toggles this. Off = no AI tick, agent stopped.</summary>
        public void SetAIEnabled(bool on)
        {
            if (AIEnabled == on) return;
            EnsureRefs();
            if (!refsReady) return;
            AIEnabled = on;
            movement.SetEnabled(on && State != CivilianState.Dead);
            av.SetAnimatorEnabled(on);
        }

        void Update()
        {
            if (!refsReady || definition == null) return;
            if (!AIEnabled || State == CivilianState.Dead) return;

            av.UpdateLocomotion(movement.CurrentSpeed, definition.runSpeed);
            PerceiveThreats();
            TrySocial();

            // don't start walking while a one-shot anim (talk/task/hit/attack) is still
            // playing — the body glides across the ground looking like it floats.
            // Hold until the animator is back in locomotion, then the tick resumes.
            if (IsMovingState() && av.IsInOneShotAnim)
            {
                movement.HoldStill();
                return;
            }

            // polite yield: player bumps into a casually-walking NPC -> NPC stops,
            // waits out a cooldown, then continues (destination survives)
            if (definition.playerYieldDistance > 0f && IsCasualMovingState())
            {
                var pT = CivilianPopulationManager.Player;
                if (pT != null && (pT.position - transform.position).sqrMagnitude <
                    definition.playerYieldDistance * definition.playerYieldDistance)
                    yieldTimer = definition.playerYieldCooldown;
            }
            if (yieldTimer > 0f)
            {
                // ONLY casual states honor the yield — leftover timer must never
                // freeze Stagger/Flee/Chase/Attack after the player turns hostile
                if (IsCasualMovingState() || State == CivilianState.Idle)
                {
                    yieldTimer -= Time.deltaTime;
                    movement.HoldStill();
                    return;
                }
                yieldTimer = 0f; // combat/threat state: discard, react at full speed
            }

            switch (State)
            {
                case CivilianState.Idle:   TickIdle();   break;
                case CivilianState.Wander: TickWander(); break;
                case CivilianState.Patrol: TickPatrol(); break;
                case CivilianState.Task:   TickTask();   break;
                case CivilianState.Flee:    TickFlee();    break;
                case CivilianState.Chase:   TickChase();   break;
                case CivilianState.Attack:  TickAttack();  break;
                case CivilianState.Stagger: TickStagger(); break;
                case CivilianState.Recover: TickRecover(); break;
                case CivilianState.GoToPoint:    TickGoToPoint();    break;
                case CivilianState.PerformPoint: TickPerformPoint(); break;
                case CivilianState.Converse:     TickConverse();     break;
                case CivilianState.Search:       TickSearch();       break;
                case CivilianState.Arrest:       TickArrest();       break;
            }
        }

        void SetState(CivilianState s)
        {
            State = s;
            stateTimer = 0f;

            switch (s)
            {
                case CivilianState.Idle:
                    movement.Stop();
                    stateTimer = Random.Range(definition.idleTimeRange.x, definition.idleTimeRange.y);
                    break;
                case CivilianState.Wander:
                    movement.Walk();
                    if (movement.TryGetWanderPoint(homePosition, definition.wanderRadius, out var p))
                        movement.MoveTo(p);
                    else SetState(CivilianState.Idle);
                    break;
                case CivilianState.Patrol:
                    movement.Walk();
                    movement.MoveTo(ScatterPoint(path.GetPoint(pathIndex)));
                    break;
                case CivilianState.Flee:
                    movement.Run();
                    fleeRepathTimer = 0.75f;
                    if (threat != null &&
                        movement.TryGetFleePoint(threat.transform.position, definition.fleeOrChaseDistance * 0.6f, out var fleeP))
                        movement.MoveTo(fleeP); // sprint starts THIS frame, no standing gap
                    break;
                case CivilianState.Chase:
                    movement.Run();
                    break;
                case CivilianState.Stagger:
                    movement.Stop();
                    av.ResetLocomotion();
                    stateTimer = definition.hitStaggerTime;
                    break;
                case CivilianState.Recover:
                    movement.Stop();
                    av.ResetLocomotion();
                    stateTimer = definition.fleeCooldown;
                    break;
                case CivilianState.GoToPoint:
                    movement.Walk();
                    if (claimedPoint != null) movement.MoveTo(claimedPoint.transform.position);
                    break;
                case CivilianState.PerformPoint:
                    if (claimedPoint != null)
                    {
                        // snap exactly onto the point — arrival tolerance leaves NPCs
                        // standing up to ~1 m off the spot otherwise
                        Vector3 snap = claimedPoint.transform.position;
                        if (NavMesh.SamplePosition(snap, out var snapHit, 2f, NavMesh.AllAreas))
                            snap = snapHit.position;
                        movement.Warp(snap);
                    }
                    movement.Stop();
                    av.ResetLocomotion();
                    if (claimedPoint != null)
                    {
                        // face the point's forward (bench direction etc.)
                        Vector3 fwd = claimedPoint.transform.forward; fwd.y = 0;
                        if (fwd.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(fwd);
                        av.TriggerCustom(claimedPoint.animTrigger);
                        stateTimer = claimedPoint.RollDuration();
                    }
                    break;
                case CivilianState.Converse:
                    movement.Stop();
                    av.ResetLocomotion();
                    break;
                case CivilianState.Search:
                    movement.Run();
                    searchTimer = definition.searchDuration;
                    movement.MoveTo(WantedSystem.LastKnownPosition);
                    break;
                case CivilianState.Arrest:
                    movement.Stop();
                    av.ResetLocomotion();
                    arrestTimer = definition.arrestTime;
                    ui.ShowSpeech("Stop! You're under arrest!", 2.5f);
                    break;
            }
        }

        // ---------- Perception ----------

        void PerceiveThreats()
        {
            if (definition.behavior == BehaviorType.Passive || definition.behavior == BehaviorType.Coward) return;

            var player = CivilianPopulationManager.Player;

            // ----- Police -----
            if (definition.behavior == BehaviorType.Police)
            {
                WantedSystem.Tick();
                if (!WantedSystem.IsWanted)
                {
                    // heat gone mid-pursuit — stand down
                    if (State == CivilianState.Chase || State == CivilianState.Search || State == CivilianState.Arrest)
                        ResumeRoutine();
                    threat = null;
                    return;
                }

                if (player == null) return;
                float sqr = (player.position - transform.position).sqrMagnitude;
                bool canSee = sqr < definition.perceptionRadius * definition.perceptionRadius;

                if (canSee)
                {
                    WantedSystem.UpdateSighting(player.position); // refresh trail for all police
                    threat = player.gameObject;
                    if (State != CivilianState.Chase && State != CivilianState.Arrest &&
                        State != CivilianState.Stagger)
                        SetState(CivilianState.Chase);
                }
                else if (IsRoutineState() || State == CivilianState.Converse)
                {
                    // wanted player out of sight — go investigate last known position
                    AbortSocial();
                    SetState(CivilianState.Search);
                }
                return;
            }

            // ----- Aggressive -----
            if (threat != null) return;
            if (player != null &&
                (player.position - transform.position).sqrMagnitude <
                 definition.perceptionRadius * definition.perceptionRadius)
            {
                threat = player.gameObject;
                SetState(CivilianState.Chase);
            }
        }

        bool IsRoutineState() =>
            State == CivilianState.Idle || State == CivilianState.Wander ||
            State == CivilianState.Patrol || State == CivilianState.Task ||
            State == CivilianState.GoToPoint || State == CivilianState.PerformPoint;

        bool IsMovingState() =>
            State == CivilianState.Wander || State == CivilianState.Patrol ||
            State == CivilianState.GoToPoint || State == CivilianState.Flee ||
            State == CivilianState.Chase || State == CivilianState.Search ||
            (State == CivilianState.Task && currentTask != null && currentTask.type == TaskType.MoveTo);

        /// <summary>Casual walking only — Flee/Chase/Search never yield to the player.</summary>
        bool IsCasualMovingState() =>
            State == CivilianState.Wander || State == CivilianState.Patrol ||
            State == CivilianState.GoToPoint ||
            (State == CivilianState.Task && currentTask != null && currentTask.type == TaskType.MoveTo);

        // ---------- State ticks ----------

        void TickIdle()
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                if (tasks.HasTasks) SetState(CivilianState.Task);
                else if (path != null && path.Count > 0) SetState(CivilianState.Patrol);
                else SetState(CivilianState.Wander);
            }
        }

        void TickWander()
        {
            if (movement.HasArrived) SetState(CivilianState.Idle);
        }

        void TickPatrol()
        {
            if (!movement.HasArrived) return;
            pathIndex = path.NextIndex(pathIndex, ref pathDirection);
            if (pathIndex < 0) { SetState(CivilianState.Idle); return; } // 'Once' path done
            movement.MoveTo(ScatterPoint(path.GetPoint(pathIndex)));
        }

        /// <summary>Offset target so multiple civilians never fight for the exact same spot.</summary>
        Vector3 ScatterPoint(Vector3 p)
        {
            Vector2 r = Random.insideUnitCircle * 1.2f;
            return p + new Vector3(r.x, 0, r.y);
        }

        void TickTask()
        {
            if (currentTask == null)
            {
                currentTask = tasks.Dequeue();
                if (currentTask == null) { SetState(CivilianState.Idle); return; }
                BeginTask(currentTask);
                return;
            }

            switch (currentTask.type)
            {
                case TaskType.MoveTo:
                    if (movement.HasArrived) FinishTask();
                    break;
                case TaskType.Wait:
                case TaskType.PlayAnimation:
                    stateTimer -= Time.deltaTime;
                    if (stateTimer <= 0f) FinishTask();
                    break;
                default:
                    FinishTask(); // instant tasks
                    break;
            }
        }

        void BeginTask(CivilianTask t)
        {
            switch (t.type)
            {
                case TaskType.MoveTo:
                    movement.Walk();
                    movement.MoveTo(t.GetPosition());
                    break;
                case TaskType.Wait:
                    movement.Stop();
                    stateTimer = t.duration;
                    break;
                case TaskType.PlayAnimation:
                    movement.Stop();
                    av.TriggerCustom(t.animTrigger);
                    stateTimer = t.duration;
                    break;
                case TaskType.PlaySound:
                    av.PlayClip(t.soundClip);
                    break;
                case TaskType.FacePoint:
                    Vector3 dir = t.GetPosition() - transform.position; dir.y = 0;
                    if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);
                    break;
                case TaskType.Custom:
                    t.onExecute?.Invoke();
                    break;
            }
        }

        void FinishTask() => currentTask = null;

        void TickFlee()
        {
            if (threat == null) { SetState(CivilianState.Recover); return; }

            float dist = Vector3.Distance(transform.position, threat.transform.position);
            if (dist >= definition.fleeOrChaseDistance)
            {
                threat = null;
                SetState(CivilianState.Recover); // catch breath, then back to routine
                return;
            }

            // repath on a TIMER, never on velocity — a freshly started agent sits at
            // speed ~0 for a few frames, and picking a new flee point every frame
            // resets the path forever: the coward vibrates in place instead of running
            fleeRepathTimer -= Time.deltaTime;
            if (fleeRepathTimer <= 0f || movement.HasArrived)
            {
                if (movement.TryGetFleePoint(threat.transform.position, definition.fleeOrChaseDistance * 0.6f, out var p))
                    movement.MoveTo(p);
                fleeRepathTimer = 0.75f; // commit to the direction for a moment
            }
        }

        /// <summary>Frozen after a hit. Waits for hit animation to finish, then reacts by behavior.</summary>
        void TickStagger()
        {
            movement.HoldStill(); // crowd can't shove us mid hit-react
            stateTimer -= Time.deltaTime;

            // wait until hit anim done playing (2s safety cap so a looping clip can't trap us)
            if (stateTimer > -2f && av.IsInHitAnim) return;
            if (stateTimer > 0f) return;

            switch (definition.behavior)
            {
                case BehaviorType.Coward:
                    if (threat != null) { SetState(CivilianState.Flee); return; }
                    break;
                case BehaviorType.Aggressive:
                case BehaviorType.Police:
                    if (threat != null) { SetState(CivilianState.Chase); return; }
                    break;
            }
            // Passive, or threat gone
            SetState(CivilianState.Recover);
        }

        /// <summary>Cooldown after running / being hit. Timer up -> resume normal routine.</summary>
        void TickRecover()
        {
            movement.HoldStill();
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f) ResumeRoutine();
        }

        /// <summary>Back to whatever this civilian normally does.</summary>
        void ResumeRoutine()
        {
            threat = null;
            if (tasks.HasTasks || currentTask != null)
            {
                SetState(CivilianState.Task);
                if (currentTask != null) BeginTask(currentTask); // re-issue interrupted task
            }
            else if (path != null && path.Count > 0) SetState(CivilianState.Patrol);
            else SetState(CivilianState.Idle);
        }

        // ---------- Task points & conversations ----------

        /// <summary>Checked ~once per second during routine states.</summary>
        void TrySocial()
        {
            // police with an active wanted player: on duty, zero chit-chat
            if (definition.behavior == BehaviorType.Police && WantedSystem.IsWanted) return;
            if (!IsSociallyAvailable) return;

            socialCheckTimer -= Time.deltaTime;
            if (socialCheckTimer > 0f) return;
            socialCheckTimer = 1f + Random.Range(0f, 0.4f); // desync so crowds don't check same frame

            // 1) task point: roll chance, closest FREE point only — occupied points ignored
            if (claimedPoint == null && Random.value < definition.taskPointChance)
            {
                var tp = TaskPoint.FindFree(transform.position, definition.taskPointSearchRadius);
                if (tp != null && tp.TryClaim(this))
                {
                    claimedPoint = tp;
                    SetState(CivilianState.GoToPoint);
                    return;
                }
            }

            // 2) conversation: same behavior type, both available, probability roll
            if (Random.value < definition.conversationChance)
            {
                var other = FindChatPartner();
                if (other != null) StartConversation(other);
            }
        }

        Civilian FindChatPartner()
        {
            float rangeSqr = definition.conversationRange * definition.conversationRange;
            for (int i = 0; i < AllCivilians.Count; i++)
            {
                var o = AllCivilians[i];
                if (o == this || o == null) continue;
                if (o.definition == null || o.definition.behavior != definition.behavior) continue; // same type only
                if (o.definition.behavior == BehaviorType.Police && WantedSystem.IsWanted) continue; // on duty
                if (!o.IsSociallyAvailable) continue;
                if ((o.transform.position - transform.position).sqrMagnitude > rangeSqr) continue;
                return o;
            }
            return null;
        }

        void StartConversation(Civilian other)
        {
            float duration = Random.Range(definition.conversationDuration.x, definition.conversationDuration.y);

            // partner must ACTUALLY enter — if it refuses (state changed this frame,
            // culled, etc.) the civilian must not be left talking to itself
            if (!other.EnterConverse(this, duration, myTurn: false)) return;
            if (!EnterConverse(other, duration, myTurn: true))
            {
                other.EndConversation(); // entry failed — release the partner
                return;
            }
        }

        bool EnterConverse(Civilian partner, float duration, bool myTurn)
        {
            // hard guard: only join from a free, AI-active routine state
            if (!AIEnabled || health.IsDead) return false;
            if (!(State == CivilianState.Idle || State == CivilianState.Wander ||
                  State == CivilianState.Patrol || State == CivilianState.Converse)) return false;

            // stamp cooldown IMMEDIATELY — even if this chat aborts a frame later,
            // neither side can instantly re-pair and chain into solo-talk loops
            nextConversationTime = Time.time + definition.conversationCooldown;

            chatPartner = partner;
            playerChat = null;
            chatMyTurn = myTurn;
            chatTurnTimer = myTurn ? 0.3f : 999f; // speaker starts almost immediately; listener waits for flip
            SetState(CivilianState.Converse);
            stateTimer = duration;
            FaceTowards(partner.transform.position);
            return true;
        }

        /// <summary>Player pressed talk key on this NPC.</summary>
        public void TalkToPlayer(Transform player, float duration)
        {
            if (!CanTalkToPlayer) return;

            // interrupt order matters: FIRST close the NPC-to-NPC chat properly
            // (partner released with cooldown, ghost trigger flushed), THEN begin
            // the player conversation as a clean fresh state.
            if (chatPartner != null) EndConversation();
            ReleasePoint();
            av.ClearTrigger("Talk");

            playerChat = player;
            chatPartner = null;
            SetState(CivilianState.Converse);
            stateTimer = duration;
            FaceTowards(player.position);
            av.TriggerCustom("Talk");
            OnPlayerTalk?.Invoke(this);
            if (definition.playerTalkLines != null && definition.playerTalkLines.Length > 0)
                ui.ShowSpeech(definition.playerTalkLines[Random.Range(0, definition.playerTalkLines.Length)],
                              Mathf.Min(duration, 4f));
        }

        public bool CanTalkToPlayer
        {
            get
            {
                if (definition == null || health == null || health.IsDead || !AIEnabled) return false;
                // police hunting a wanted player won't stop for a chat
                if (definition.behavior == BehaviorType.Police && WantedSystem.IsWanted) return false;
                return State == CivilianState.Idle || State == CivilianState.Wander ||
                       State == CivilianState.Patrol || State == CivilianState.Converse;
            }
        }

        void TickConverse()
        {
            movement.HoldStill(); // planted while talking — no floating

            // ---- talking with the player ----
            if (playerChat != null)
            {
                FaceTowards(playerChat.position);
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f && !av.IsInOneShotAnim)
                {
                    playerChat = null;
                    av.ClearTrigger("Talk");
                    nextConversationTime = Time.time + 3f; // short cooldown after player chat
                    ResumeRoutine();
                }
                return;
            }

            // ---- NPC-to-NPC ----
            // partner died / fled / got culled (AI frozen) / vanished — nobody talks alone
            if (chatPartner == null || !chatPartner.AIEnabled ||
                chatPartner.State != CivilianState.Converse)
            {
                EndConversation();
                return;
            }

            FaceTowards(chatPartner.transform.position);

            // turn-based chit-chat: speak, pass turn to partner, wait, repeat
            chatTurnTimer -= Time.deltaTime;
            if (chatTurnTimer <= 0f && chatMyTurn && !av.IsInOneShotAnim)
            {
                SpeakChitChat();
                chatMyTurn = false;
                if (chatPartner != null) chatPartner.ReceiveTurn(); // hand the turn over
            }

            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f && !av.IsInOneShotAnim) EndConversation();
        }

        /// <summary>Partner finished their line — this civilian replies after a beat.</summary>
        public void ReceiveTurn()
        {
            chatMyTurn = true;
            chatTurnTimer = Random.Range(2.2f, 3.2f); // listen, then answer
        }

        void SpeakChitChat()
        {
            // NPC-to-NPC: talk animation only, no dialogue bubble.
            // Bubbles are reserved for player-facing dialogue (TalkToPlayer, police shouts).
            av.TriggerCustom("Talk");
        }

        public void EndConversation()
        {
            if (State != CivilianState.Converse && chatPartner == null) return;

            // detach FIRST (prevents recursion), then pull the partner out too —
            // both sides end the same frame, both carry the cooldown. Nobody
            // is ever left standing in Converse talking to air.
            var partner = chatPartner;
            chatPartner = null;
            nextConversationTime = Time.time + definition.conversationCooldown;
            av.ForceLocomotion(); // snap out of Talk NOW — no finishing the clip at thin air

            if (partner != null && partner.chatPartner == this)
                partner.EndConversation();

            if (State == CivilianState.Converse) ResumeRoutine();
        }

        void TickGoToPoint()
        {
            if (claimedPoint == null) { ResumeRoutine(); return; }
            if (!movement.HasArrived) return;

            SetState(CivilianState.PerformPoint);
        }

        void TickPerformPoint()
        {
            if (claimedPoint == null) { ResumeRoutine(); return; }

            movement.HoldStill();
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f && !av.IsInOneShotAnim) // finish the task anim before walking off
            {
                ReleasePoint();
                ResumeRoutine();
            }
        }

        void ReleasePoint()
        {
            if (claimedPoint != null)
            {
                av.ClearTrigger(claimedPoint.animTrigger); // no ghost task anim later
                claimedPoint.Release(this);
                claimedPoint = null;
            }
        }

        /// <summary>Interrupted mid-social (hit, death) — clean up point claim + partner.</summary>
        void AbortSocial()
        {
            ReleasePoint();
            if (chatPartner != null)
            {
                var partner = chatPartner;
                chatPartner = null;
                nextConversationTime = Time.time + definition.conversationCooldown;
                av.ForceLocomotion();
                if (partner != null && partner.chatPartner == this)
                    partner.EndConversation(); // partner walks off instantly, no waiting to notice
            }
        }

        void FaceTowards(Vector3 point)
        {
            Vector3 dir = point - transform.position; dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        }

        void TickChase()
        {
            if (threat == null) { SetState(CivilianState.Idle); return; }

            float dist = Vector3.Distance(transform.position, threat.transform.position);
            bool police = definition.behavior == BehaviorType.Police;

            if (dist > definition.fleeOrChaseDistance)  // give up / lost sight
            {
                threat = null;
                if (police) SetState(CivilianState.Search);
                else SetState(CivilianState.Idle);
                return;
            }

            float closeRange = police ? definition.arrestRange : definition.attackRange;
            if (dist <= closeRange)
            {
                SetState(police ? CivilianState.Arrest : CivilianState.Attack);
                return;
            }
            movement.MoveTo(threat.transform.position);
        }

        /// <summary>Police: investigate last known position, look around, give up after a while.</summary>
        void TickSearch()
        {
            if (!WantedSystem.IsWanted) { ResumeRoutine(); return; }

            searchTimer -= Time.deltaTime;
            if (searchTimer <= 0f) { ResumeRoutine(); return; } // cold trail

            // sweep around the last known position
            if (movement.HasArrived)
            {
                if (movement.TryGetWanderPoint(WantedSystem.LastKnownPosition, 6f, out var p))
                    movement.MoveTo(p);
            }
            // PerceiveThreats flips to Chase automatically when player spotted
        }

        /// <summary>Police standing on the player — hold to complete the arrest. Deals NO damage.</summary>
        void TickArrest()
        {
            movement.HoldStill();
            var player = CivilianPopulationManager.Player;
            if (player == null || !WantedSystem.IsWanted) { ResumeRoutine(); return; }

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist > definition.arrestRange * 1.5f)   // player broke away
            {
                threat = player.gameObject;
                SetState(CivilianState.Chase);
                return;
            }

            FaceTowards(player.position);

            arrestTimer -= Time.deltaTime;
            if (arrestTimer <= 0f)
            {
                ui.ShowSpeech("You're coming with me.", 3f);
                WantedSystem.Arrest();
                threat = null;
                SetState(CivilianState.Recover);
            }
        }

        void TickAttack()
        {
            // Safety: police never attack, they arrest. Redirect if this state is reached.
            if (definition.behavior == BehaviorType.Police) { SetState(CivilianState.Arrest); return; }

            if (threat == null) { SetState(CivilianState.Idle); return; }

            // never leave mid-swing — current attack anim must finish first
            if (av.IsInOneShotAnim) { FaceTowards(threat.transform.position); return; }

            float dist = Vector3.Distance(transform.position, threat.transform.position);
            if (dist > definition.attackRange * 1.25f) { SetState(CivilianState.Chase); return; }

            movement.Stop();
            FaceTowards(threat.transform.position);

            if (Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + definition.attackCooldown;
                av.TriggerAttack();
                if (threat.TryGetComponent<IDamageable>(out var dmg))
                    dmg.TakeDamage(definition.attackDamage, gameObject);
            }
        }

        // ---------- Damage / Death ----------

        void HandleDamaged(float amount, GameObject source)
        {
            // killing blow: skip hit react + stagger entirely — HandleDeath fires right after
            // and Die must be the only animation. Hit+Die same frame = animator plays Hit first.
            if (health.IsDead) return;

            ui.ShowHealth(health.CurrentHealth, health.MaxHealth); // floating bar + numbers
            OnAnyDamaged?.Invoke(this, amount);
            yieldTimer = 0f; // polite pause ends the instant violence starts

            ReportCrimeIfPlayerAttack(source);

            AbortSocial(); // drop task point claim / end conversation
            playerChat = null;
            av.TriggerHit();
            threat = source;

            if (State != CivilianState.Stagger && State != CivilianState.Dead)
                SetState(CivilianState.Stagger);
        }

        /// <summary>Player hurt this NPC — any police within their perception radius witnesses it.</summary>
        void ReportCrimeIfPlayerAttack(GameObject source)
        {
            var playerT = CivilianPopulationManager.Player;
            if (playerT == null || source == null || source != playerT.gameObject) return;

            Vector3 crimePos = transform.position;
            for (int i = 0; i < AllCivilians.Count; i++)
            {
                var o = AllCivilians[i];
                if (o == null || o.definition == null) continue;
                if (o.definition.behavior != BehaviorType.Police) continue;
                if (o.health != null && o.health.IsDead) continue;

                float r = o.definition.perceptionRadius;
                if ((o.transform.position - crimePos).sqrMagnitude <= r * r)
                {
                    WantedSystem.ReportCrime(playerT.position);
                    o.ui.ShowSpeech("Hey! Stop right there!", 2.5f);
                    break; // one witness is enough
                }
            }
        }

        void HandleDeath()
        {
            OnAnyDeath?.Invoke(this);
            AbortSocial(); // release point / drop partner before dying
            playerChat = null;
            ui.HideAll();  // no health bar / bubble floating over a corpse
            SetState(CivilianState.Dead);
            movement.Stop();               // zero velocity — no corpse sliding
            movement.SetEnabled(false);
            av.SetAnimatorEnabled(true);   // may have been AI-culled — die anim must still play
            av.ResetLocomotion();          // Speed = 0, locomotion silent
            av.TriggerDie();               // only die animation plays

            // disable colliders so corpse not blocking
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

            if (definition.lootTable != null)
                definition.lootTable.SpawnDrops(transform.position);

            if (population != null)
                population.NotifyDeath(this, definition.corpseLifetime);
            else
                Destroy(gameObject, definition.corpseLifetime);
        }

        void OnDrawGizmosSelected()
        {
            if (definition == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, definition.perceptionRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, definition.attackRange);
        }
    }
}
