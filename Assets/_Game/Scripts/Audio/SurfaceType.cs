namespace NorthStar.Audio
{
    /// <summary>
    /// Ground material under the player's feet, used by <see cref="FootstepSystem"/> to
    /// pick the right footstep SFX. <see cref="Unknown"/> is the fallback when a raycast
    /// hits geometry that maps to no configured surface.
    /// </summary>
    public enum SurfaceType
    {
        Unknown = 0,
        Grass,
        Stone,
        Wood,
        Water
    }
}
