namespace Zeus.Sim;

public enum Mouth { Closed, Small, Open, Wide }

/// <summary>Timing and mouth of a mentor line. The mouth follows the text (rough on purpose: it only has to look alive); audio replaces it later.</summary>
public static class MentorTiming
{
    public const double BaseSeconds = 2.5, SecondsPerChar = 0.06, ClosedTail = 0.4, FlapSeconds = 0.1;
    public const int MaxLineLength = 140;

    public static double Duration(string text) => BaseSeconds + SecondsPerChar * text.Length;

    /// <summary>The mouth at <paramref name="t"/> seconds into the line: one character per flap, closed for the last <see cref="ClosedTail"/> seconds and after the end.</summary>
    public static Mouth MouthAt(string text, double t)
    {
        if (text.Length == 0 || t < 0 || t >= Duration(text) - ClosedTail) return Mouth.Closed;
        // The text is spoken over the whole speaking time, so mouth and subtitle end together.
        int i = (int)(t / (Duration(text) - ClosedTail) * text.Length);
        return Of(text, Math.Min(i, text.Length - 1));
    }

    static Mouth Of(string text, int i)
    {
        char c = char.ToLowerInvariant(text[i]);
        if (!char.IsLetter(c)) return Mouth.Closed;
        if (c is 'a' or 'o' or 'u') return Mouth.Open;
        if (c is 'e' or 'i') return Mouth.Wide;
        return Mouth.Small;
    }
}
