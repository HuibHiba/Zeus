namespace Zeus.Sim;

public sealed partial class Match
{
    private readonly bool _hasPaths;
    /// <summary>Upper bound on the total time of one path run (walking plus waiting); the brain takes over afterwards.</summary>
    internal const double MaxPathSeconds = 45;

    private static bool HasPaths(Tactic tactic) => tactic.Phases.Values.Concat(tactic.DefendVariants.Values)
        .Any(layout => layout.Players.Any(p => p.Path is { Count: > 0 }));

    private static void ValidatePaths(Tactic tactic)
    {
        foreach (var p in tactic.Phases.Values.Concat(tactic.DefendVariants.Values).SelectMany(l => l.Players))
            if (p.Path is { } points && (points.Count is < 1 or > 8 || points.Any(point =>
                !double.IsFinite(point.X) || !double.IsFinite(point.Y) || point.X < 0 || point.X > 1 || point.Y < 0 || point.Y > 1))
                || p.PathSpeed is not ("wandelen" or "joggen" or "sprint")
                || !double.IsFinite(p.PathWait) || p.PathWait < 0 || p.PathWait > 5)
                throw new ArgumentException($"Invalid path for player '{p.Id}': needs 1..8 points within 0..1, known speed and wait within 0..5.");
    }

    private sealed class PathProgress
    {
        public required PhasePlayer Layout;
        public int Index;
        public long? WaitUntil;
        public long StartTick;
    }

    private void EndPath(Player p, string reason)
    {
        if (p.Path is null) return;
        p.Path = null;
        p.NextDecisionTick = _tick;
        _events.Add(new Event(EventType.PathEnd, p.Team, p.Id, _tick, TimeSeconds, p.Body.Pos, reason));
    }

    private void StartPath(Player p, string reason)
    {
        if (!_config.EnablePaths || p.SentOff || RoleFallbackActive(p) || LayoutFor(p).Path is not { Count: > 0 }) return;
        EndPath(p, "restarted");
        p.Path = new PathProgress { Layout = LayoutFor(p), StartTick = _tick };
        p.RunUntilTick = 0;
        _events.Add(new Event(EventType.PathStart, p.Team, p.Id, _tick, TimeSeconds, p.Body.Pos, reason));
    }

    // Called after phase updates, before possession triggers: a trigger can restart the new phase's path.
    private void SyncPaths()
    {
        if (!_hasPaths) return;
        foreach (var p in _players)
        {
            var phase = PhaseOf(p.Team);
            if (p.PathPhase != phase || p.PathEndsSwapped != _endsSwapped)
            {
                EndPath(p, "phase changed");
                p.PathPhase = phase;
                p.PathEndsSwapped = _endsSwapped;
                StartPath(p, "phase start");
            }
            if (p.SentOff) EndPath(p, "sent off");
        }
    }

    private void UpdatePathTargets()
    {
        if (!_hasPaths) return;
        foreach (var p in _players)
        {
            if (!p.Started || p.SentOff || p.Path is not { } progress) continue;
            if ((_tick - progress.StartTick) * _config.StepSeconds > MaxPathSeconds) { EndPath(p, "timeout"); continue; }
            var layout = progress.Layout;
            var points = layout.Path!;
            var relative = progress.Index < points.Count ? points[progress.Index] : (layout.X, layout.Y);
            var target = Pitch.ToMeters(relative.Item1, relative.Item2, p.Team, _endsSwapped);
            if ((target - p.Body.Pos).Length <= 0.02 && p.Body.Vel.Length <= 0.02)
            {
                if (progress.Index == points.Count)
                {
                    EndPath(p, "completed");
                    p.Target = target;
                    p.Intent = new Intent(IntentKind.HoldPosition, target);
                    continue;
                }
                progress.WaitUntil ??= _tick + (long)Math.Ceiling(layout.PathWait / _config.StepSeconds - 1e-9);
                if (_tick >= progress.WaitUntil)
                {
                    progress.Index++;
                    progress.WaitUntil = null;
                    relative = progress.Index < points.Count ? points[progress.Index] : (layout.X, layout.Y);
                    target = Pitch.ToMeters(relative.Item1, relative.Item2, p.Team, _endsSwapped);
                }
            }
            p.Target = target;
            p.Intent = new Intent(IntentKind.MoveToPosition, target);
        }
    }
}
