using System.Collections.Generic;
using MagicMotion.Vision;
using UnityEngine;

namespace MagicMotion.Tests
{
    internal static class HandTestFactory
    {
        public static HandData Create(Handedness handedness, Vector3 offset)
        {
            var landmarks = new List<HandLandmark>(HandData.RequiredLandmarkCount);
            for (var index = 0; index < HandData.RequiredLandmarkCount; index++)
            {
                landmarks.Add(new HandLandmark(index, offset + new Vector3(index * 0.01f, index * 0.02f, 0f)));
            }

            landmarks[(int)HandLandmarkIndex.Wrist] = new HandLandmark(0, offset + new Vector3(0f, 0f, 0f));
            landmarks[(int)HandLandmarkIndex.IndexMcp] = new HandLandmark(5, offset + new Vector3(-0.5f, 1f, 0f));
            landmarks[(int)HandLandmarkIndex.MiddleMcp] = new HandLandmark(9, offset + new Vector3(0f, 1.1f, 0f));
            landmarks[(int)HandLandmarkIndex.RingMcp] = new HandLandmark(13, offset + new Vector3(0.3f, 1f, 0f));
            landmarks[(int)HandLandmarkIndex.PinkyMcp] = new HandLandmark(17, offset + new Vector3(0.5f, 0.8f, 0f));
            return new HandData(handedness, 0.9f, Vector3.zero, landmarks);
        }
    }
}

