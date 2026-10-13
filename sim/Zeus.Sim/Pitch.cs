namespace Zeus.Sim;

public enum Side { Home, Away }

/// <summary>Pitch constants and tactic-to-metre conversion. Origin at the centre spot, x in the home attacking direction.</summary>
public static class Pitch
{
    public const double Length = 105.0;
    public const double Width = 68.0;
    public const double HalfLength = Length / 2;
    public const double HalfWidth = Width / 2;

    /// <summary>
    /// Converts relative tactic coordinates (x 0..1 own goal line to opponent goal line, y 0..1 left to right
    /// seen in attack direction) to metres. Away is rotated 180 degrees about the centre.
    /// Home: y=0 (left) maps to -34, y=1 (right) to +34. Pass <paramref name="mirrored"/> to flip (e.g. second half).
    /// </summary>
    public static Vec2 ToMeters(double relX, double relY, Side side, bool mirrored = false)
    {
        var p = new Vec2((relX - 0.5) * Length, (relY - 0.5) * Width);
        return (side == Side.Away) != mirrored ? -p : p;
    }
}
