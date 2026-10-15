using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicMotion.Vision
{
    [Serializable]
    public sealed class HandData
    {
        public const int RequiredLandmarkCount = 21;

        [SerializeField] private Handedness handedness;
        [SerializeField, Range(0f, 1f)] private float confidence;
        [SerializeField] private Vector3 palmNormal;
        [SerializeField] private List<HandLandmark> landmarks = new List<HandLandmark>(RequiredLandmarkCount);

        public Handedness Handedness => handedness;
        public float Confidence => confidence;
        public Vector3 PalmNormal => palmNormal;
        public IReadOnlyList<HandLandmark> Landmarks => landmarks;
        public bool IsValid => landmarks != null && landmarks.Count == RequiredLandmarkCount;

        public HandData(
            Handedness handedness,
            float confidence,
            Vector3 palmNormal,
            IEnumerable<HandLandmark> landmarks)
        {
            this.handedness = handedness;
            this.confidence = Mathf.Clamp01(confidence);
            this.palmNormal = palmNormal;
            this.landmarks = landmarks == null
                ? new List<HandLandmark>()
                : new List<HandLandmark>(landmarks);
        }
    }
}

