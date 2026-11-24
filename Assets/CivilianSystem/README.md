# Civilian System

**Author: Shaban**

A civilian NPC framework I wrote earlier in the project, before the design settled on
the recruiter-and-ally structure that the finished game uses.

**It is disabled in the delivered scene.** The four `CivilianPopulationManager`
components sit under the `All_NPC` parent, which is inactive, so no civilians spawn
and the system has no effect on play or on the leadership assessment. The code is kept
in the project because it is my own work and because the scene still references the
prefabs, but it is not part of the submitted artefact's behaviour and is not discussed
in the project report.

## What it does

Path following and NavMesh obstacle avoidance; four behaviour archetypes (passive,
aggressive, coward, police); a priority task queue for multi-step routines; damage and
death through `IDamageable`; loot drops; audio and animator driving; world-space health
bars and dialogue; NPC conversations and task points; a wanted and arrest system;
population targets with respawn; object pooling; and distance-based culling that
switches AI off at one radius and the whole object off at a further one.

Requires the AI Navigation package for NavMesh.

## Layout

```
CivilianSystem/
├── Runtime/
│   ├── Civilian.cs                    main state machine
│   ├── CivilianDefinition.cs          ScriptableObject archetype
│   ├── CivilianHealth.cs              damage and death (IDamageable)
│   ├── CivilianMovement.cs            NavMesh wrapper: wander, flee, chase
│   ├── CivilianTask.cs                task queue
│   ├── CivilianAudioAnim.cs           sounds and animator parameters
│   ├── WaypointPath.cs                patrol paths: loop, ping-pong, once
│   ├── LootTable.cs                   death drops
│   ├── CivilianPool.cs                object pooling
│   ├── CivilianPopulationManager.cs   spawning, respawn, culling
│   ├── WantedSystem.cs                crime and arrest state
│   ├── TaskPoint.cs                   world locations a civilian can be sent to
│   ├── PlayerNPCInteractor.cs         conversation trigger
│   └── Demo/                          a self-contained test scene and player
└── Editor/
    ├── CivilianSetupWindow.cs         character setup wizard
    ├── CivilianDemoBuilder.cs         builds the demo scene
    ├── CivilianAnimatorBuilder.cs     generates the animator controller
    ├── WaypointPathEditor.cs          scene-view path editing
    ├── WorldUIPrefabCreator.cs        health bar and dialogue prefabs
    └── MissingScriptCleaner.cs        removes broken component references
```

## Design notes

The archetype lives in a `CivilianDefinition` asset rather than on the component, so a
population entry is a definition plus a target count and the manager handles the rest.
Animation is entirely parameter-driven — `Speed` as a float, `Attack`, `Hit` and `Die`
as triggers — which is what lets the same code drive humanoid and generic rigs without
branching.

Culling is the part I spent longest on. Running a full state machine on a hundred
civilians is not viable, so the manager amortises its distance checks across frames and
has two thresholds: beyond `aiCullDistance` the behaviour stops but the object stays
visible, and beyond `fullCullDistance` the object is deactivated and returned to the
pool. Aggressive civilians acquire the player inside `perceptionRadius`; every
non-passive civilian retaliates against whatever damaged it.

The editor tooling exists because building each civilian by hand was the slowest part
of using the system. `CivilianSetupWindow` takes a model and an animator controller and
produces a finished prefab and definition asset in one step.
