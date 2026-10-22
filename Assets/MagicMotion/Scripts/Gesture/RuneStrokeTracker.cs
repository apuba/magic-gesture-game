using System.Collections.Generic;
using UnityEngine;

namespace MagicMotion.Gesture
{
    public enum RuneStrokeState
    {
        Idle,
        SwordFingerCandidate,
        Drawing,
        StrokeEnd
    }

    public sealed class RuneStrokeTracker
    {
        private readonly float hoverSeconds;
        private readonly float maximumHoverDrift;
        private readonly float missingGraceSeconds;
        private readonly float minimumPointDistance;
        private readonly float maximumSampleInterval;
        private readonly float completedStrokeSeconds;
        private readonly List<Vector3> points = new List<Vector3>(128);

        private Vector3 candidateAnchor;
        private double candidateStartedAt;
        private double lastPoseAt;
        private double lastPointAt;
        private double strokeEndedAt;

        public RuneStrokeTracker(
            float hoverSeconds = 1f,
            float maximumHoverDrift = 0.3f,
            float missingGraceSeconds = 0.2f,
            float minimumPointDistance = 0.008f,
            float maximumSampleInterval = 0.05f,
            float completedStrokeSeconds = 1.5f)
        {
            this.hoverSeconds = Mathf.Max(0.1f, hoverSeconds);
            this.maximumHoverDrift = Mathf.Max(0.01f, maximumHoverDrift);
            this.missingGraceSeconds = Mathf.Max(0f, missingGraceSeconds);
            this.minimumPointDistance = Mathf.Max(0.0001f, minimumPointDistance);
            this.maximumSampleInterval = Mathf.Max(0.01f, maximumSampleInterval);
            this.completedStrokeSeconds = Mathf.Max(0f, completedStrokeSeconds);
        }

        public RuneStrokeState State { get; private set; }
        public IReadOnlyList<Vector3> Points => points;
        public float CandidateProgress { get; private set; }

        public void UpdatePose(bool isSwordFinger, Vector3 brushPoint, float palmWidth, double timestamp)
        {
            if (!isSwordFinger)
            {
                HandleMissing(timestamp);
                return;
            }

            lastPoseAt = timestamp;
            switch (State)
            {
                case RuneStrokeState.Idle:
                case RuneStrokeState.StrokeEnd:
                    BeginCandidate(brushPoint, timestamp);
                    break;
                case RuneStrokeState.SwordFingerCandidate:
                    UpdateCandidate(brushPoint, palmWidth, timestamp);
                    break;
                case RuneStrokeState.Drawing:
                    AddPointIfNeeded(brushPoint, timestamp);
                    break;
            }
        }

        public void Tick(double timestamp)
        {
            if ((State == RuneStrokeState.SwordFingerCandidate || State == RuneStrokeState.Drawing) &&
                timestamp - lastPoseAt > missingGraceSeconds)
            {
                if (State == RuneStrokeState.Drawing)
                {
                    EndStroke(timestamp);
                }
                else
                {
                    Reset();
                }
            }

            if (State == RuneStrokeState.StrokeEnd && timestamp - strokeEndedAt > completedStrokeSeconds)
            {
                Reset();
            }
        }

        public void Reset()
        {
            State = RuneStrokeState.Idle;
            CandidateProgress = 0f;
            points.Clear();
            candidateStartedAt = 0d;
            lastPoseAt = 0d;
            lastPointAt = 0d;
        }

        private void BeginCandidate(Vector3 point, double timestamp)
        {
            points.Clear();
            candidateAnchor = point;
            candidateStartedAt = timestamp;
            CandidateProgress = 0f;
            State = RuneStrokeState.SwordFingerCandidate;
        }

        private void UpdateCandidate(Vector3 point, float palmWidth, double timestamp)
        {
            var scale = Mathf.Max(palmWidth, 0.0001f);
            if (Vector3.Distance(point, candidateAnchor) / scale > maximumHoverDrift)
            {
                BeginCandidate(point, timestamp);
                return;
            }

            CandidateProgress = Mathf.Clamp01((float)((timestamp - candidateStartedAt) / hoverSeconds));
            if (CandidateProgress < 1f)
            {
                return;
            }

            State = RuneStrokeState.Drawing;
            points.Clear();
            points.Add(point);
            lastPointAt = timestamp;
        }

        private void AddPointIfNeeded(Vector3 point, double timestamp)
        {
            if (points.Count == 0 ||
                Vector3.Distance(points[points.Count - 1], point) >= minimumPointDistance ||
                timestamp - lastPointAt >= maximumSampleInterval)
            {
                points.Add(point);
                lastPointAt = timestamp;
            }
        }

        private void HandleMissing(double timestamp)
        {
            Tick(timestamp);
        }

        private void EndStroke(double timestamp)
        {
            State = RuneStrokeState.StrokeEnd;
            CandidateProgress = 0f;
            strokeEndedAt = timestamp;
        }
    }
}
