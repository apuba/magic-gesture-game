#!/usr/bin/env python3
"""Analyze a MagicMotion push calibration session using only Python stdlib."""

from __future__ import annotations

import argparse
import csv
import json
import math
from collections import defaultdict
from pathlib import Path
from statistics import mean


def read_jsonl(path: Path):
    if not path.exists():
        return []
    with path.open("r", encoding="utf-8") as stream:
        return [json.loads(line) for line in stream if line.strip()]


def percentile(values, probability):
    values = sorted(values)
    if not values:
        return None
    position = (len(values) - 1) * probability
    lower = int(math.floor(position))
    upper = int(math.ceil(position))
    if lower == upper:
        return values[lower]
    fraction = position - lower
    return values[lower] * (1.0 - fraction) + values[upper] * fraction


def accepted_attempts(trials):
    return {
        (record["trialId"], record["attempt"])
        for record in trials
        if record.get("status") == "accepted"
    }


def choose_primary_hand(frames):
    tracks = defaultdict(list)
    for frame in frames:
        for hand in frame.get("hands", []):
            handedness = hand.get("handedness")
            if handedness and handedness != "Invalid":
                tracks[handedness].append((frame, hand))
    if not tracks:
        return None, []
    hand_name = max(tracks, key=lambda name: len(tracks[name]))
    return hand_name, sorted(tracks[hand_name], key=lambda item: item[0]["timestamp"])


def vector_xy(record):
    return float(record.get("x", 0.0)), float(record.get("y", 0.0))


def extract_features(key, frames, events):
    first_frame = frames[0]
    hand_name, track = choose_primary_hand(frames)
    base = {
        "trial_id": key[0],
        "attempt": key[1],
        "category": first_frame.get("category"),
        "action": first_frame.get("action"),
        "zone": first_frame.get("zone"),
        "distance_m": first_frame.get("distanceMeters"),
        "hand": hand_name or "None",
        "frame_count": len(frames),
        "hand_frame_count": len(track),
        "max_growth": 0.0,
        "growth_velocity": 0.0,
        "growth_duration": 0.0,
        "depth_change": 0.0,
        "lateral_residual": 999.0,
        "open_palm_ratio": 0.0,
        "forward_ratio": 0.0,
        "motion_events": sum(1 for event in events if event.get("eventType") == "motion"),
        "cast_events": sum(1 for event in events if event.get("eventType") == "skill_cast"),
    }
    if len(track) < 2:
        return base

    open_count = sum(1 for _, hand in track if hand.get("rawGesture") == "OpenPalm")
    forward_count = sum(1 for _, hand in track if hand.get("palmDirection") == "Forward")
    base["open_palm_ratio"] = open_count / len(track)
    base["forward_ratio"] = forward_count / len(track)

    best = None
    for start_index, (start_frame, start_hand) in enumerate(track[:-1]):
        start_width = max(float(start_hand.get("palmWidth", 0.0)), 0.0001)
        start_center = vector_xy(start_hand.get("palmCenter", {}))
        start_depth = float(start_hand.get("meanDepth", 0.0))
        start_time = float(start_frame["timestamp"])
        for end_frame, end_hand in track[start_index + 1 :]:
            duration = float(end_frame["timestamp"]) - start_time
            if duration < 0.08:
                continue
            if duration > 1.2:
                break
            end_width = float(end_hand.get("palmWidth", 0.0))
            growth = (end_width - start_width) / start_width
            velocity = growth / duration
            end_center = vector_xy(end_hand.get("palmCenter", {}))
            travelled = (end_center[0] - start_center[0], end_center[1] - start_center[1])
            radial = (start_center[0] - 0.5, start_center[1] - 0.5)
            expected = (radial[0] * growth * 0.5, radial[1] * growth * 0.5)
            residual = math.hypot(travelled[0] - expected[0], travelled[1] - expected[1]) / start_width
            depth = (start_depth - float(end_hand.get("meanDepth", 0.0))) / start_width
            candidate = (growth, velocity, duration, depth, residual)
            if best is None or growth > best[0]:
                best = candidate

    if best is not None:
        base["max_growth"] = best[0]
        base["growth_velocity"] = best[1]
        base["growth_duration"] = best[2]
        base["depth_change"] = best[3]
        base["lateral_residual"] = best[4]
    return base


def summarize(features):
    positive = [item for item in features if item["category"] == "positive"]
    negative = [item for item in features if item["category"] == "negative"]

    def values(items, field):
        return [float(item[field]) for item in items]

    positive_growth = values(positive, "max_growth")
    positive_velocity = values(positive, "growth_velocity")
    positive_duration = values(positive, "growth_duration")
    positive_lateral = values(positive, "lateral_residual")
    negative_growth = values(negative, "max_growth")
    negative_velocity = values(negative, "growth_velocity")

    growth_floor = percentile(positive_growth, 0.10)
    negative_growth_ceiling = percentile(negative_growth, 0.90)
    velocity_floor = percentile(positive_velocity, 0.10)
    negative_velocity_ceiling = percentile(negative_velocity, 0.90)
    recommended_growth = None if growth_floor is None else growth_floor
    recommended_velocity = None if velocity_floor is None else velocity_floor
    if growth_floor is not None and negative_growth_ceiling is not None and negative_growth_ceiling < growth_floor:
        recommended_growth = (growth_floor + negative_growth_ceiling) / 2.0
    if velocity_floor is not None and negative_velocity_ceiling is not None and negative_velocity_ceiling < velocity_floor:
        recommended_velocity = (velocity_floor + negative_velocity_ceiling) / 2.0

    return {
        "accepted_positive": len(positive),
        "accepted_negative": len(negative),
        "positive_distribution": {
            "growth_p10": growth_floor,
            "growth_median": percentile(positive_growth, 0.50),
            "velocity_p10": velocity_floor,
            "velocity_median": percentile(positive_velocity, 0.50),
            "duration_p90": percentile(positive_duration, 0.90),
            "lateral_p90": percentile(positive_lateral, 0.90),
            "open_palm_ratio_mean": mean(values(positive, "open_palm_ratio")) if positive else None,
            "forward_ratio_mean": mean(values(positive, "forward_ratio")) if positive else None,
        },
        "negative_distribution": {
            "growth_p90": negative_growth_ceiling,
            "velocity_p90": negative_velocity_ceiling,
            "algorithm_motion_events": sum(item["motion_events"] for item in negative),
            "algorithm_cast_events": sum(item["cast_events"] for item in negative),
        },
        "suggested_motion_config": {
            "minimumScaleGrowth": recommended_growth,
            "minimumScaleVelocity": recommended_velocity,
            "maximumDuration": percentile(positive_duration, 0.90),
            "maximumLateralDrift": percentile(positive_lateral, 0.90),
        },
        "warning": "Suggested values require held-out validation; do not deploy solely from this report.",
    }


def main():
    parser = argparse.ArgumentParser(description="Analyze MagicMotion push calibration JSONL files.")
    parser.add_argument("session_dir", type=Path)
    args = parser.parse_args()
    session_dir = args.session_dir.resolve()

    trials = read_jsonl(session_dir / "trials.jsonl")
    frames = read_jsonl(session_dir / "frames.jsonl")
    events = read_jsonl(session_dir / "events.jsonl")
    accepted = accepted_attempts(trials)
    frames_by_attempt = defaultdict(list)
    events_by_attempt = defaultdict(list)
    for frame in frames:
        key = (frame.get("trialId"), frame.get("attempt"))
        if key in accepted:
            frames_by_attempt[key].append(frame)
    for event in events:
        key = (event.get("trialId"), event.get("attempt"))
        if key in accepted:
            events_by_attempt[key].append(event)

    features = [
        extract_features(key, attempt_frames, events_by_attempt[key])
        for key, attempt_frames in sorted(frames_by_attempt.items())
        if attempt_frames
    ]
    if not features:
        raise SystemExit("No accepted trial frames were found in the session.")

    csv_path = session_dir / "trial_features.csv"
    with csv_path.open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(features[0].keys()))
        writer.writeheader()
        writer.writerows(features)

    report = summarize(features)
    report_path = session_dir / "analysis_report.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(report_path)
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
