# Soulbound Awakening

My final year project: a third-person fantasy action game built in Unity 6, with a small
neural network inside it that judges how well you led your allies.

Shaban — S3228381 — CIS3040-N Computing Project, Teesside University

## What this repo holds

Only the code I wrote. The game also uses licensed character, animation, environment and
AI packages from the Unity Asset Store and Fab, and I'm not allowed to redistribute those,
so there is no art, no models and no third-party framework code here. The full project went
to the assessors separately.

That comes to 57 C# scripts (about 10,400 lines), a 293-line Python training script, and
the trained model file as it ships in the game.

## The idea

Plenty of games give you companions. Almost none of them ever tell you whether you looked
after them. This one does.

A campaign takes about fifteen minutes. While you play, the game quietly reduces the whole
run to seven numbers:

- how long each ally waited for you to help them
- how long the campaign took
- how long the boss fight took
- enemies you defeated
- enemies you left behind
- allies you left behind
- times you died

When the boss dies, those seven numbers go into the network and it gives you a tier:
Normal, Average, Good or Gorgeous.

## The network

7 inputs, then hidden layers of 32 and 16, then 4 outputs. 852 parameters in total, which
fits in a 27 KB JSON file.

I train it offline in Python. I wrote the rules for what good leadership means, used those
rules to generate and label 12,000 simulated campaigns, trained on them with scikit-learn,
and exported the weights. The game then reads that file and runs the forward pass in my own
C#. No inference library, no plug-in, no server, and the game never touches the network.

```
expert_rules.json  ->  train_leadership_mlp.py  ->  leadership_mlp.json
                                                          |
                            LeadershipMlpModel.cs  <-------+
                                    ^
                      LeadershipSessionTracker.cs
```

It reproduces its labels 92.4% of the time on 2,400 held-out campaigns. I also wrote a
separate C# test harness that re-implements the rules and the data generator from scratch,
and ran 20,000 fresh campaigns through the game's own arithmetic — that agreed 93.0% of the
time. Every disagreement was between neighbouring tiers, and nearly all of them sat right on
a tier boundary. One classification takes about 0.35 microseconds.

## Where things are

The assessment is all in `Assets/Scripts/Leadership/`:

- `LeadershipSessionTracker.cs` — records the seven features while you play
- `LeadershipData.cs` — the feature set and the four tiers
- `LeadershipMlpModel.cs` — the network itself, the forward pass in C#
- `ProvisionalLeadershipEvaluator.cs` — the live score shown during a run
- `LeadershipPanelUI.cs` — the panel you open with L
- `LeadershipStorage.cs` — writes each campaign to disk as JSON and CSV
- `AllyCompanionManager.cs` — the four allies and how they follow you

The rest:

- `Assets/Scripts/PlayerCombat/` — four spells, lock-on, and the 0.45 s dodge window
- `Assets/Scripts/Damage/` — the bridges between the different health systems
- `Assets/Scripts/Enemy Spawner/` — the waves that place 126 enemies
- `Assets/Scripts/Menu System/` — menus, save slots, and pausing the campaign clock
- `Assets/AbyssalWarden/Scripts/` — the two-phase boss and the cinematics
- `MLP_Training/` — the Python side

`Assets/CivilianSystem/` is an NPC crowd framework I built earlier in the year. It's
switched off in the finished game and has nothing to do with the assessment, but it's my
code so it's here.

## The part that took longest

The player, the ordinary enemies and the boss all came from different packages, each with
its own health system, and none of them knew about the others. Rewriting any of them wasn't
realistic. So each bridge in `Assets/Scripts/Damage/` watches one side and replays the
damage through the other side's public interface. That way every hit reaction, health bar
and death animation still runs in the code that owns it. It went through three versions
before it stopped breaking.

## Retraining it

```bash
pip install -r MLP_Training/requirements.txt
python MLP_Training/train_leadership_mlp.py
```

That writes a new `leadership_mlp.json`. Drop it into the build's
`StreamingAssets/Leadership` folder and the game picks it up — no rebuild needed.

## Note

This won't compile on its own, because the licensed packages aren't here. The playable
Windows build and the complete Unity project were handed in separately.

Built with Unity 6000.3.10f1, URP 17.4, IL2CPP.
