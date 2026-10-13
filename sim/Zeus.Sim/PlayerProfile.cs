namespace Zeus.Sim;

/// <summary>Movement limits of one player. Defaults from the measured data (see Natuurkundewaarden).</summary>
public sealed record PlayerProfile(
    double MaxSpeed = 7.8,       // m/s
    double Acceleration = 4.5,   // m/s^2
    double Deceleration = 5.0,   // m/s^2
    double TurnRate = 6.0)       // rad/s, body direction
{
    public static readonly PlayerProfile Default = new();
}
