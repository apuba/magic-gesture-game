namespace MagicMotion.Combat
{
    public readonly struct EnemyDefeated
    {
        public EnemyDefeated(int instanceId, string enemyId)
        {
            InstanceId = instanceId;
            EnemyId = enemyId;
        }

        public int InstanceId { get; }
        public string EnemyId { get; }
    }
}

