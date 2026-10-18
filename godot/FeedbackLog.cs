// Small ring log of important events (screens, matches, errors) for the feedback file, plus the global exception catchers.
// The logic (anonymizing, bounded ring) lives in Zeus.Sim.FeedbackPack/RingLog. No personal data goes in; nothing is sent anywhere.
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Zeus.Sim;

public static class FeedbackLog
{
    static readonly RingLog Ring = new(500, SafeUserName(), Feedback.PrivateDirs());
    static bool _installed;
    static long _lastErrorDialogTicks;

    static string SafeUserName() { try { return System.Environment.UserName; } catch (Exception) { return ""; } }

    public static string[] Lines => Ring.Snapshot();

    public static void Event(string text) => Ring.Add($"{DateTime.Now:HH:mm:ss} {text}");

    public static void Screen(string name) => Event("screen opened: " + name);

    public static void Fault(string source, Exception error)
        => Event($"ERROR [{source}] {error.GetType().FullName}: {error.Message} | {error.StackTrace}");

    // The last match, as a small JSON for the feedback file (no personal data).
    static string? _lastMatch;
    static string _matchStart = "";
    public static string? LastMatchJson => _lastMatch;

    public static void MatchStarted(ulong seed, string homeTactic, string awayTactic, string homeLevel, string awayLevel, string mode)
    {
        _matchStart = $"seed={seed} home='{homeTactic}' ({homeLevel}) away='{awayTactic}' ({awayLevel}) mode={mode}";
        Event("match started: " + _matchStart);
        _lastMatch = JsonSerializer.Serialize(new { status = "started", seed, home = homeTactic, away = awayTactic, homeLevel, awayLevel, mode },
            new JsonSerializerOptions { WriteIndented = true });
    }

    public static void MatchFinished(ulong seed, string homeTactic, string awayTactic, string homeLevel, string awayLevel, string mode,
        int homeGoals, int awayGoals, long steps, string status, string eventHash)
    {
        Event($"match finished: seed={seed} home='{homeTactic}' away='{awayTactic}' result={homeGoals}-{awayGoals} status={status} steps={steps}");
        _lastMatch = JsonSerializer.Serialize(new
        {
            status, seed, home = homeTactic, away = awayTactic, homeLevel, awayLevel, mode, homeGoals, awayGoals, steps, eventHash,
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Installs the exception catchers once (called from the Hotkeys autoload).</summary>
    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var error = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "unknown");
            Fault("UnhandledException" + (e.IsTerminating ? " (terminating)" : ""), error);
            if (e.IsTerminating) DumpForNextStart();
            Feedback.ShowErrorFromAnyThread();
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Fault("UnobservedTaskException", e.Exception);
            e.SetObserved();
            Feedback.ShowErrorFromAnyThread();
        };
        try { OS.AddLogger(new EngineLogger()); } catch (Exception e) { Event("engine logger unavailable: " + e.Message); }
        Event($"game started: version {GameInfo.FullVersion}, language {Loc.Language}");
    }

    // A terminating exception cannot show a dialog; keep the ring for the next start so F9 can still include it.
    static void DumpForNextStart()
    {
        try { System.IO.File.WriteAllLines(ProjectSettings.GlobalizePath("user://feedback_vorige_crash.txt"), Ring.Snapshot()); } catch (Exception) { }
    }

    /// <summary>Called by the dialog code; at most one error dialog every 3 seconds.</summary>
    public static bool ErrorDialogAllowed()
    {
        var now = System.Environment.TickCount64;
        if (now - _lastErrorDialogTicks < 3000) return false;
        _lastErrorDialogTicks = now;
        return true;
    }
}

// Receives the engine's own error stream (also from other threads). Exceptions thrown inside Godot callbacks show up here as "Unhandled exception".
sealed partial class EngineLogger : Logger
{
    // An exception thrown inside a Godot callback is logged by Godot as "Namespace.SomeException: message" with a stack trace ("   at ...").
    static readonly System.Text.RegularExpressions.Regex ExceptionText = new(@"\b[A-Za-z.]+Exception: [\s\S]*\n\s+at ", System.Text.RegularExpressions.RegexOptions.Compiled);
    static bool LooksLikeException(string text) => text.Contains("nhandled exception", StringComparison.OrdinalIgnoreCase) || ExceptionText.IsMatch(text);

    public override void _LogError(string function, string file, int line, string code, string rationale, bool editorNotify, int errorType,
        Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
    {
        var text = string.IsNullOrEmpty(rationale) ? code : rationale + " | " + code;
        FeedbackLog.Event($"engine {(errorType == 1 ? "warning" : "error")}: {text} ({file}:{line} {function})");
        if (errorType != 1 && LooksLikeException(text)) Feedback.ShowErrorFromAnyThread();
    }
}
