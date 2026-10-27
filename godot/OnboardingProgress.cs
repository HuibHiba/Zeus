using System;
using Godot;
using Zeus.Sim;

public static class OnboardingProgress
{
    static string File => Profiles.Path("onboarding.json");
    static OnboardingState? _state;
    static bool _blocked;
    public static OnboardingState State
    {
        get
        {
            if (_state is not null) return _state;
            _state = new OnboardingState();
            try
            {
                var status = SafeFile.Read(File, out var json);
                _blocked = status is SafeFile.ReadStatus.Unreadable or SafeFile.ReadStatus.Newer;
                if (status == SafeFile.ReadStatus.Ok)
                {
                    try { _state = OnboardingState.Parse(json); }
                    catch (FormatException) { _blocked = !SafeFile.SetAside(File); }
                }
            }
            catch (Exception e) { _blocked = true; GD.PushWarning(e.Message); }
            return _state;
        }
    }
    public static void Mark(string step) { if (State.Mark(step)) Save(); }
    public static void SeenWelcome() { State.WelcomeSeen = true; Save(); }
    static void Save()
    {
        if (_blocked || GameSession.DebugEnvironment("ZEUS_NOSAVE").Length > 0) return;
        try { if (!SafeFile.Write(File, State.ToJson(), out var error)) GD.PushWarning(error); }
        catch (Exception e) { GD.PushWarning(e.Message); }
    }
}
