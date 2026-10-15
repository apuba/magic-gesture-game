using UnityEngine;

namespace MagicMotion.Combat
{
    public readonly struct DamageEvent
    {
        public DamageEvent(
            int targetInstanceId,
            string sourceId,
            float amount,
            Vector3 hitPoint,
            Vector3 force)
        {
            TargetInstanceId = targetInstanceId;
            SourceId = sourceId;
            Amount = amount;
            HitPoint = hitPoint;
            Force = force;
        }

        public int TargetInstanceId { get; }
        public string SourceId { get; }
        public float Amount { get; }
        public Vector3 HitPoint { get; }
        public Vector3 Force { get; }
    }
}

