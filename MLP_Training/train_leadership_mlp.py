"""
Train the leadership-assessment MLP and export it for Unity.

Inputs (the MLP features recorded by LeadershipSessionTracker):
    recruitment_duration_s, total_game_duration_s, boss_encounter_duration_s, enemies_killed,
    enemies_undefeated, allies_left_behind, player_deaths
Output classes:
    Normal, Average, Good, Gorgeous

Training data:
    1. Synthetic runs labelled by the expert rules in expert_rules.json
       (until enough real labelled runs exist, the MLP learns to imitate these rules).
    2. Real runs from sessions.csv whose `leadership_label` column has been filled in
       (oversampled by --real-weight so they outweigh the synthetic data).

Usage (from the project folder):
    MLP_Training\\.venv\\Scripts\\python.exe MLP_Training\\train_leadership_mlp.py

Writes:
    Assets/StreamingAssets/Leadership/leadership_mlp.json   (loaded by the game)
    MLP_Training/last_training_report.txt
"""

import argparse
import csv
import json
import os
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from sklearn.metrics import accuracy_score, classification_report, confusion_matrix
from sklearn.model_selection import train_test_split
from sklearn.neural_network import MLPClassifier
from sklearn.preprocessing import StandardScaler

FEATURES = [
    "recruitment_duration_s",
    "total_game_duration_s",
    "boss_encounter_duration_s",
    "enemies_killed",
    "enemies_undefeated",
    "allies_left_behind",
    "player_deaths",
]
TIERS = ["Normal", "Average", "Good", "Gorgeous"]

TRAINING_DIR = Path(__file__).resolve().parent
PROJECT_DIR = TRAINING_DIR.parent
DEFAULT_SESSIONS = (
    Path(os.environ.get("USERPROFILE", "~")).expanduser()
    / "AppData" / "LocalLow" / "fyp" / "Soulbound Awakening" / "Leadership" / "sessions.csv"
)
DEFAULT_OUTPUT = PROJECT_DIR / "Assets" / "StreamingAssets" / "Leadership" / "leadership_mlp.json"


# =========================================================
# EXPERT RULES (mirror of ProvisionalLeadershipEvaluator for a finished run)
# =========================================================

def inverse_lerp(a, b, v):
    if a == b:
        return np.zeros_like(v, dtype=float)
    return np.clip((v - a) / (b - a), 0.0, 1.0)


def rule_scores(x, rules):
    """x: (n, len(FEATURES)) raw features -> (n,) score in 0..1."""
    recruitment, total, boss, killed, undefeated, left_behind, deaths = (x[:, i] for i in range(7))

    avg_task = recruitment / max(1, rules["allyTaskCount"])
    in_area = killed + undefeated
    clearance = np.divide(killed, in_area, out=np.ones_like(killed, dtype=float), where=in_area > 0)
    kept = 1.0 - left_behind / max(1, rules["alliesRecruited"])

    parts = [
        (inverse_lerp(rules["poorTaskSeconds"], rules["goodTaskSeconds"], avg_task), rules["taskWeight"]),
        (inverse_lerp(rules["poorClearance"], rules["goodClearance"], clearance), rules["clearanceWeight"]),
        (np.clip(kept, 0.0, 1.0), rules["alliesWeight"]),
        (inverse_lerp(rules["poorBossSeconds"], rules["goodBossSeconds"], boss), rules["bossWeight"]),
        (inverse_lerp(rules["poorGameSeconds"], rules["goodGameSeconds"], total), rules["gameWeight"]),
    ]

    weighted = sum(value * weight for value, weight in parts) / sum(weight for _, weight in parts)
    penalty = np.minimum(rules["maxDeathPenalty"], deaths * rules["penaltyPerDeath"])
    return np.clip(weighted - penalty, 0.0, 1.0)


def rule_labels(x, rules):
    s = rule_scores(x, rules)
    labels = np.full(len(s), "Normal", dtype=object)
    labels[s >= rules["averageFrom"]] = "Average"
    labels[s >= rules["goodFrom"]] = "Good"
    labels[s >= rules["gorgeousFrom"]] = "Gorgeous"
    return labels


# =========================================================
# DATA
# =========================================================

def log_uniform(rng, low, high, n):
    return np.exp(rng.uniform(np.log(low), np.log(high), n))


def synthetic_runs(rng, rules, per_class):
    """Balanced synthetic runs: sample broad plausible ranges, keep per_class of each tier."""
    kept = {tier: [] for tier in TIERS}

    while any(len(rows) < per_class for rows in kept.values()):
        n = 20000
        recruitment = log_uniform(rng, 20, 4000, n)
        boss = log_uniform(rng, 15, 1800, n)
        other = log_uniform(rng, 30, 6000, n)     # exploring, travel, fights outside tasks
        total = recruitment + boss + other
        in_area = rng.integers(0, rules["maxEnemiesInArea"] + 1, n).astype(float)
        killed = np.floor(rng.random(n) * (in_area + 1))
        undefeated = in_area - killed
        left_behind = rng.integers(0, rules["alliesRecruited"] + 1, n).astype(float)
        deaths = np.minimum(rng.geometric(0.45, n) - 1, 10).astype(float)

        x = np.column_stack([recruitment, total, boss, killed, undefeated, left_behind, deaths])
        y = rule_labels(x, rules)

        for tier in TIERS:
            need = per_class - len(kept[tier])
            if need > 0:
                kept[tier].extend(x[y == tier][:need])

    x = np.vstack([np.array(kept[t]) for t in TIERS])
    y = np.concatenate([[t] * per_class for t in TIERS])
    return x, y


def read_sessions(path):
    """Returns (labelled_x, labelled_y, unlabelled_rows) from sessions.csv."""
    labelled_x, labelled_y, unlabelled = [], [], []

    n = len(FEATURES)

    if not path.exists():
        return np.empty((0, n)), np.empty(0, dtype=object), unlabelled

    with open(path, newline="", encoding="utf-8") as f:
        for row in csv.DictReader(f):
            try:
                features = [float(row[name]) for name in FEATURES]
            except (KeyError, ValueError, TypeError):
                continue  # rows from an older CSV format lack the newer features

            if row.get("started_midway", "0").strip() == "1":
                continue

            label = row.get("leadership_label", "").strip().capitalize()
            if label in TIERS:
                labelled_x.append(features)
                labelled_y.append(label)
            else:
                unlabelled.append((row.get("campaign_id", "?"), features, row.get("provisional_tier", "")))

    return np.array(labelled_x).reshape(-1, n), np.array(labelled_y, dtype=object), unlabelled


def transform(x):
    # Durations and kills are skewed; log1p keeps long runs from dominating.
    return np.log1p(np.maximum(x, 0.0))


# =========================================================
# EXPORT
# =========================================================

def export_model(path, model, scaler, metadata):
    layers = []
    for i, (w, b) in enumerate(zip(model.coefs_, model.intercepts_)):
        last = i == len(model.coefs_) - 1
        layers.append({
            "inputs": int(w.shape[0]),
            "outputs": int(w.shape[1]),
            "activation": model.out_activation_ if last else model.activation,
            # Row-major: weights[i * outputs + j] connects input i to output j.
            "weights": [float(v) for v in w.reshape(-1)],
            "biases": [float(v) for v in b],
        })

    data = {
        "version": 1,
        "featureNames": FEATURES,
        "inputTransform": "log1p",
        "scalerMean": [float(v) for v in scaler.mean_],
        "scalerScale": [float(v) for v in scaler.scale_],
        "layers": layers,
        "classes": [str(c) for c in model.classes_],
        **metadata,
    }

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")


# =========================================================
# MAIN
# =========================================================

def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--sessions", type=Path, default=DEFAULT_SESSIONS)
    parser.add_argument("--rules", type=Path, default=TRAINING_DIR / "expert_rules.json")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--synthetic-per-class", type=int, default=3000)
    parser.add_argument("--real-weight", type=int, default=20, help="How many times each real labelled run is repeated.")
    parser.add_argument("--hidden", type=int, nargs="+", default=[32, 16])
    parser.add_argument("--seed", type=int, default=42)
    args = parser.parse_args()

    rules = json.loads(args.rules.read_text(encoding="utf-8"))
    rng = np.random.default_rng(args.seed)

    syn_x, syn_y = synthetic_runs(rng, rules, args.synthetic_per_class)
    real_x, real_y, unlabelled = read_sessions(args.sessions)

    sx_train, sx_test, sy_train, sy_test = train_test_split(
        syn_x, syn_y, test_size=0.2, stratify=syn_y, random_state=args.seed)

    x_train = np.vstack([sx_train] + [real_x] * args.real_weight)
    y_train = np.concatenate([sy_train] + [real_y] * args.real_weight)

    scaler = StandardScaler().fit(transform(x_train))

    model = MLPClassifier(
        hidden_layer_sizes=tuple(args.hidden),
        activation="relu",
        solver="adam",
        alpha=1e-4,
        max_iter=1000,
        early_stopping=True,
        n_iter_no_change=25,
        random_state=args.seed,
    )
    model.fit(scaler.transform(transform(x_train)), y_train)

    test_pred = model.predict(scaler.transform(transform(sx_test)))
    test_accuracy = accuracy_score(sy_test, test_pred)

    label_source = "synthetic expert-rule labels"
    if len(real_y):
        label_source += f" + {len(real_y)} real labelled runs"

    report = []
    report.append(f"Leadership MLP training report  ({datetime.now(timezone.utc).isoformat()})")
    report.append(f"Label source: {label_source}")
    report.append(f"Synthetic runs: {len(syn_y)} ({args.synthetic_per_class} per class), real labelled runs: {len(real_y)}")
    report.append(f"Features: {', '.join(FEATURES)}")
    report.append(f"Network: {len(FEATURES)} -> {' -> '.join(map(str, args.hidden))} -> {len(TIERS)} (ReLU, softmax), iterations: {model.n_iter_}")
    report.append("")
    report.append(f"Held-out synthetic test accuracy: {test_accuracy:.3f}")
    report.append("(This measures how well the MLP reproduces the expert rules, not how well it judges real players.)")
    report.append(classification_report(sy_test, test_pred, labels=TIERS, digits=3))
    report.append("Confusion matrix (rows = true, cols = predicted): " + ", ".join(TIERS))
    report.append(str(confusion_matrix(sy_test, test_pred, labels=TIERS)))

    if len(real_y):
        real_pred = model.predict(scaler.transform(transform(real_x)))
        report.append("")
        report.append(f"Real labelled runs (used in training): accuracy {accuracy_score(real_y, real_pred):.3f}")

    if unlabelled:
        report.append("")
        report.append("Unlabelled completed runs (add a leadership_label in sessions.csv to train on them):")
        ux = np.array([f for _, f, _ in unlabelled])
        probs = model.predict_proba(scaler.transform(transform(ux)))
        for (cid, features, provisional), p in zip(unlabelled, probs):
            best = int(np.argmax(p))
            report.append(
                f"  {cid}: features={features} -> MLP {model.classes_[best]} ({p[best]:.0%}), "
                f"expert rules: {rule_labels(np.array([features]), rules)[0]}, in-game provisional: {provisional}")

    text = "\n".join(report)
    print(text)
    (TRAINING_DIR / "last_training_report.txt").write_text(text + "\n", encoding="utf-8")

    export_model(args.output, model, scaler, {
        "createdUtc": datetime.now(timezone.utc).isoformat(),
        "labelSource": label_source,
        "syntheticSamples": int(len(syn_y)),
        "realLabelledSamples": int(len(real_y)),
        "testAccuracy": float(test_accuracy),
    })
    print(f"\nModel exported to {args.output}")


if __name__ == "__main__":
    main()
