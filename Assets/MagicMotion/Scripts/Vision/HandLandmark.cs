using System;
using UnityEngine;

namespace MagicMotion.Vision
{
    [Serializable]
    public struct HandLandmark
    {
        public HandLandmark(int index, Vector3 position)
        {
            Index = index;
            Position = position;
        }

        public int Index;
        public Vector3 Position;
    }
}

