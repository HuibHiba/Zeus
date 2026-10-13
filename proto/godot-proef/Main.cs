// WP8 weergave-proef (wegwerp): 22 stippen met kijkrichting, posities uit de tactiekbestanden.
// De simulatie draait met een vaste stap (0,1 s), los van de framerate van Godot.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

public partial class Main : Node2D
{
    const double L = 105, W = 68;
    const float Px = 10f;                    // pixels per meter
    const double Dt = 0.1, Speed = 6.0;      // vaste stap en loopsnelheid
    static readonly Vector2 Origin = new(550, 360);

    record Slot(double X, double Y, double Fx, double Fy);

    readonly Dictionary<string, Slot[]>[] tac = new Dictionary<string, Slot[]>[2];
    readonly Vector2[,] pos = new Vector2[2, 11];
    readonly Vector2[,] face = new Vector2[2, 11];
    double acc, clock;
    int shots;
    int possTeam;

    public override void _Ready()
    {
        var dir = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "..", "tactics"));
        tac[0] = Load(Path.Combine(dir, "feyenoord_433_voorlopig.json"));
        tac[1] = Load(Path.Combine(dir, "laag_blok_442.json"));
        for (var t = 0; t < 2; t++)
            for (var i = 0; i < 11; i++)
            {
                var s = tac[t]["opbouw"][i];
                pos[t, i] = World(t, s.X, s.Y);
                face[t, i] = new Vector2(t == 0 ? 1 : -1, 0);
            }
        GD.Print("Tactieken geladen, 22 spelers klaar");
    }

    static Dictionary<string, Slot[]> Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var fases = doc.RootElement.GetProperty("fases");
        var ids = doc.RootElement.GetProperty("spelers").EnumerateArray().Select(s => s.GetProperty("id").GetString()!).ToList();
        var result = new Dictionary<string, Slot[]>();
        foreach (var f in fases.EnumerateObject()) result[f.Name] = Resolve(fases, f.Name, ids);
        return result;
    }

    static Slot[] Resolve(JsonElement fases, string name, List<string> ids)
    {
        var f = fases.GetProperty(name);
        if (f.TryGetProperty("gelijk_aan", out var g)) return Resolve(fases, g.GetString()!, ids);
        var map = f.GetProperty("spelers").EnumerateArray().ToDictionary(s => s.GetProperty("id").GetString()!, s => s);
        return ids.Select(id =>
        {
            var s = map[id];
            var p = s.GetProperty("pos");
            double fx = 0, fy = 0;
            if (s.TryGetProperty("volg_bal", out var v)) { fx = v[0].GetDouble(); fy = v[1].GetDouble(); }
            return new Slot(p[0].GetDouble(), p[1].GetDouble(), fx, fy);
        }).ToArray();
    }

    // Tactiek (0..1, aanvalsrichting) naar meters; uitteam gespiegeld (180 graden).
    static Vector2 World(int team, double sx, double sy) =>
        team == 0 ? new Vector2((float)((sx - 0.5) * L), (float)((sy - 0.5) * W))
                  : new Vector2((float)((0.5 - sx) * L), (float)((0.5 - sy) * W));

    public override void _Process(double delta)
    {
        acc += delta * 4; // 4x versneld afspelen
        while (acc >= Dt) { acc -= Dt; Step(); }
        QueueRedraw();

        var shot = OS.GetEnvironment("ZEUS_SHOT");
        if (shot.Length > 0 && ((shots == 0 && clock > 6) || (shots == 1 && clock > 14)))
        {
            GetViewport().GetTexture().GetImage().SavePng($"{shot}_{shots + 1}.png");
            GD.Print($"Screenshot {shots + 1} bij klok {clock:F1} s");
            shots++;
        }
        if (shot.Length > 0 && shots >= 2) GetTree().Quit();
    }

    void Step()
    {
        clock += Dt;
        possTeam = ((int)(clock / 8)) % 2; // elke 8 s wisselt het balbezit
        var ball = pos[possTeam, 9];
        for (var t = 0; t < 2; t++)
        {
            var slots = tac[t][t == possTeam ? "opbouw" : "verdedigen"];
            var ballRel = t == 0 ? ball.X / L + 0.5 : 0.5 - ball.X / L;
            for (var i = 0; i < 11; i++)
            {
                var s = slots[i];
                var target = World(t, s.X + s.Fx * (ballRel - 0.5) * 0.5, s.Y);
                var d = target - pos[t, i];
                var step = (float)(Speed * Dt);
                if (d.Length() <= step) pos[t, i] = target;
                else { pos[t, i] += d.Normalized() * step; face[t, i] = d.Normalized(); }
            }
        }
    }

    public override void _Draw()
    {
        // veld
        var field = new Rect2(Origin - new Vector2((float)L * Px / 2, (float)W * Px / 2), new Vector2((float)L * Px, (float)W * Px));
        DrawRect(field, new Color(0.18f, 0.5f, 0.2f));
        var line = new Color(1, 1, 1, 0.8f);
        DrawRect(field, line, false, 2);
        DrawLine(Origin + new Vector2(0, -(float)W * Px / 2), Origin + new Vector2(0, (float)W * Px / 2), line, 2);
        DrawArc(Origin, 9.15f * Px, 0, Mathf.Tau, 48, line, 2);
        foreach (var sign in new[] { -1, 1 })
        {
            var boxW = 16.5f * Px; var boxH = 40.3f * Px;
            var x = Origin.X + sign * ((float)L * Px / 2 - boxW / 2);
            DrawRect(new Rect2(x - boxW / 2, Origin.Y - boxH / 2, boxW, boxH), line, false, 2);
        }
        // spelers: team 0 rood (aanvalsrichting rechts), team 1 blauw
        for (var t = 0; t < 2; t++)
        {
            var col = t == 0 ? new Color(0.9f, 0.1f, 0.1f) : new Color(0.15f, 0.35f, 0.95f);
            for (var i = 0; i < 11; i++)
            {
                var p = Origin + pos[t, i] * Px;
                DrawCircle(p, 7, col);
                DrawArc(p, 7, 0, Mathf.Tau, 24, Colors.White, 1.5f);
                DrawLine(p, p + face[t, i] * 14, Colors.White, 2);
            }
        }
        // bal bij de baldrager
        DrawCircle(Origin + pos[possTeam, 9] * Px + new Vector2(6, 6), 4, Colors.White);
        DrawString(ThemeDB.FallbackFont, new Vector2(12, 24), $"t={clock:F1}s  bezit: {(possTeam == 0 ? "rood (Feyenoord voorlopig)" : "blauw (laag blok)")}", HorizontalAlignment.Left, -1, 16, Colors.White);
    }
}
