namespace Zeus.Sim;

/// <summary>PCG32 (XSH-RR 64/32), deterministic and independent of the .NET version.</summary>
public sealed class Rng
{
    private const ulong Multiplier = 6364136223846793005UL;
    private const ulong DefaultStream = 54UL;
    private ulong _state;
    private readonly ulong _inc;

    public Rng(ulong seed) : this(seed, DefaultStream) { }

    public Rng(ulong initState, ulong initSeq)
    {
        _state = 0;
        _inc = (initSeq << 1) | 1UL;
        NextUInt();
        _state += initState;
        NextUInt();
    }

    public uint NextUInt()
    {
        ulong old = _state;
        unchecked
        {
            _state = old * Multiplier + _inc;
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }
    }

    /// <summary>Uniform double in [0,1) with 32-bit resolution.</summary>
    public double NextDouble() => NextUInt() / 4294967296.0;

    /// <summary>Unbiased integer in [0, bound).</summary>
    public uint NextUInt(uint bound)
    {
        if (bound == 0) throw new ArgumentOutOfRangeException(nameof(bound));
        uint threshold = (uint)((0x100000000UL - bound) % bound);
        while (true)
        {
            uint r = NextUInt();
            if (r >= threshold) return r % bound;
        }
    }
}
