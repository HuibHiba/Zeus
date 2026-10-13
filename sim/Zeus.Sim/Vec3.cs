namespace Zeus.Sim;

/// <summary>3D vector in metres (x, y on the pitch, z = height above the grass).</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, double s) => new(a.X * s, a.Y * s, a.Z * s);

    [System.Text.Json.Serialization.JsonIgnore]
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    [System.Text.Json.Serialization.JsonIgnore]
    public Vec2 XY => new(X, Y);
}
