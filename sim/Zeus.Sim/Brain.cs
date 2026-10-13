namespace Zeus.Sim;

public enum IntentKind { MoveToPosition, HoldPosition, Pass, Scan, Press }

public sealed record Intent(IntentKind Kind, Vec2 Target, string? ReceiverId = null);

/// <summary>Decision policy: receives a filtered perception, never the match or the complete world.</summary>
public interface IPlayerBrain
{
    Intent Decide(Perception perception);
}

/// <summary>First rule-based policy: keep the tactical position. Passing follows in task 15.</summary>
public sealed class FormationBrain : IPlayerBrain
{
    public Intent Decide(Perception perception) => new(
        (perception.FormationTarget - perception.Self.Pos).Length <= 0.02
            ? IntentKind.HoldPosition : IntentKind.MoveToPosition,
        perception.FormationTarget);
}
