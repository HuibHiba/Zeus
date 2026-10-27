// The tutorial controller (design §5, §7): which step the active profile is on, what is locked on the home screen, and the practice matches.
// The state lives in OnboardingProgress (tutorial step + done). Practice matches are ChallengeRuns with Tutorial = true: Main records nothing for them.
using System;
using System.IO;
using Godot;
using Zeus.Sim;

public static class Tutorial
{
    static readonly string[] Letters = { "a", "b", "c" };

    /// <summary>On from the first start until the closing card; never without a profile (autotests and smoke without ZEUS_TUTORIAL=force have none).</summary>
    public static bool Active => !OnboardingProgress.State.TutorialDone && ProfileStore.Current is not null && GameSession.DebugEnvironment("ZEUS_TUTORIAL") != "skip";
    /// <summary>The explanation moments (MentorFirst) speak only after the tutorial, in a profile; autotests (ZEUS_TUTORIAL=skip) never get them.</summary>
    public static bool Explaining => ProfileStore.Current is not null && !Active && GameSession.DebugEnvironment("ZEUS_TUTORIAL") != "skip";
    public static int Step => OnboardingProgress.State.TutorialStep;

    public static bool Unlocked(string feature) => !Active || TutorialFlow.Unlocked(feature, Step, done: false);

    /// <summary>Greys a button out with a tooltip naming the step that opens it (it stays visible, so the player sees what is coming).</summary>
    public static void Lock(BaseButton button, string feature)
    {
        if (Unlocked(feature)) return;
        button.Disabled = true;
        button.TooltipText = Loc.T("lock.after." + TutorialFlow.After(feature));
        if (button is Button { Text.Length: > 0 } labelled) labelled.Text += Loc.T("lock.suffix");
    }

    public static void Advance(int step) => OnboardingProgress.SetTutorial(step, false);

    /// <summary>The intro cards after the first start (T0), then step 2. Also the way back when the game stopped between the first start and the end of the intro.</summary>
    public static void Intro(Node owner)
    {
        var args = new object[] { ProfileStore.Current?.Name ?? "", ClubIdentity.Name };
        Mentor.Card(owner, "intro.1", () => Mentor.Card(owner, "intro.2", () => Mentor.Card(owner, "intro.3", () =>
        {
            Advance(TutorialFlow.FirstScenarioStep);
            owner.GetTree().ReloadCurrentScene();       // the home screen is built again with the new locks
        }, args: args), args: args), args: args);
    }

    /// <summary>Called when the home screen opens while the tutorial is on.</summary>
    public static void OnHome(Node owner)
    {
        if (Step < TutorialFlow.FirstScenarioStep) Intro(owner);
        else if (Step == TutorialFlow.ClosingStep)
            Mentor.Card(owner, "end.summary", () => Mentor.Card(owner, "end.help", () => { OnboardingProgress.SetTutorial(OnboardingState.LastTutorialStep, true); owner.GetTree().ReloadCurrentScene(); }));
    }

    /// <summary>The practice match for the current step (A-C), or null.</summary>
    public static ChallengeSpec? Scenario()
    {
        int i = TutorialFlow.ScenarioIndex(Step);
        if (!Active || i < 0) return null;
        try { return TutorialScenarios.Load(OS.HasFeature("editor") ? Path.Combine(GameSession.DataRoot(), "data", "tutorial") : Path.Combine(GameSession.DataRoot(), "tutorial"), Loc.Language)[i]; }
        catch (FormatException e) { GD.PushWarning(e.Message); return null; }
    }

    public static string Letter => Letters[Math.Clamp(TutorialFlow.ScenarioIndex(Step), 0, 2)];

    public static void StartScenario(Node owner)
    {
        if (Scenario() is not { } spec) return;
        var dir = GameSession.TacticsDir();
        var player = TacticLoader.LoadFile(Path.Combine(dir, spec.StartTactic));
        var run = new ChallengeRun(spec, Path.Combine(dir, spec.StartTactic), spec.PlayerLevel, Path.Combine(dir, spec.Opponent), ProfileStore.Current?.Name ?? "", null,
            new HistoryEntry(player.Name, StyleProfile.From(player))) { Tutorial = true };
        Mentor.Card(owner, $"scenario_{Letter}.start", () => { run.Prepare(); owner.GetTree().ChangeSceneToFile("res://Main.tscn"); });
    }

    /// <summary>A practice match is over (end card button or Enter): next step, back to the home screen. Leaving early with Esc does not call this.</summary>
    public static void FinishScenario(Node owner)
    {
        GameSession.Challenge = null;
        Mentor.Dismiss();
        Advance(Step + 1);
        owner.GetTree().ChangeSceneToFile("res://Menu.tscn");
    }

    /// <summary>The real match (T4) was played from the league screen: on to the closing card.</summary>
    public static void LeagueMatchPlayed() { if (Active && Step == TutorialFlow.RealMatchStep) Advance(TutorialFlow.ClosingStep); }
}
