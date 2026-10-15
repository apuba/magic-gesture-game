namespace MagicMotion.Combat
{
    public readonly struct DamageApplied
    {
        public DamageApplied(
            int targetInstanceId,
            string sourceId,
            float appliedDamage,
            float remainingHealth)
        {
            TargetInstanceId = targetInstanceId;
            SourceId = sourceId;
            AppliedDamage = appliedDamage;
            RemainingHealth = remainingHealth;
        }

        public int TargetInstanceId { get; }
        public string SourceId { get; }
        public float AppliedDamage { get; }
        public float RemainingHealth { get; }
    }
}

