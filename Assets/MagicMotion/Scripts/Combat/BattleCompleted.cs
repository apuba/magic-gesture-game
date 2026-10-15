namespace MagicMotion.Combat
{
    public readonly struct BattleCompleted
    {
        public BattleCompleted(int defeatedEnemies)
        {
            DefeatedEnemies = defeatedEnemies;
        }

        public int DefeatedEnemies { get; }
    }
}

