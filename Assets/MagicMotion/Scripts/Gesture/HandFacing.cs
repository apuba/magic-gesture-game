namespace MagicMotion.Gesture
{
    /// <summary>
    /// Which side of the hand is turned towards the camera, in the three states
    /// the summon sequence is written against. The tracking layer reports a palm
    /// normal, not a facing, and the two are not the same vocabulary: a normal
    /// pointing at the lens means the palm faces the lens, a normal pointing
    /// away means the back of the hand does.
    /// </summary>
    public enum HandFacing
    {
        Unknown,
        BackToCamera,
        SideToCamera,
        PalmToCamera
    }
}
