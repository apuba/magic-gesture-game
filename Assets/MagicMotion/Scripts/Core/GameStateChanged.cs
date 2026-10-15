namespace MagicMotion.Core
{
    public readonly struct GameStateChanged
    {
        public GameStateChanged(GameState previous, GameState current)
        {
            Previous = previous;
            Current = current;
        }

        public GameState Previous { get; }
        public GameState Current { get; }
    }
}

