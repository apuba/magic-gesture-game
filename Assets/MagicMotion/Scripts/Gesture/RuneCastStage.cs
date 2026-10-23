namespace MagicMotion.Gesture
{
    /// <summary>
    /// The stages of one casting attempt, from the sword-finger start through
    /// to the push that releases the spell.
    /// </summary>
    public enum RuneCastStage
    {
        Idle,
        SwordReady,
        Flipping,
        BaseRuneReady,
        RuneReady,
        Releasing,
        Cooldown
    }
}
