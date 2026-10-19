using System;
using Zeus.Sim;

/// <summary>
/// Coordinate convention between the simulation and the 3D scene (docs/3d-architectuur.md §2.3).
/// Sim: x along the pitch, y across the pitch, z up. World: x along the pitch, y up, z across the pitch.
/// The mapping (x, y, z) -> (x, z, y) mirrors handedness, so a sim heading becomes a negated yaw.
/// No Godot types: usable from the Godot-free test project.
/// </summary>
public static class SimWorld
{
    /// <summary>
    /// UNVERIFIED: assumes a model whose forward axis is -Z (Godot's convention for cameras and lights); glTF/Mixamo
    /// imports usually face +Z. Check this offset against the first real model (capsule nose cone, T3/T4) and adjust.
    /// Yaw of a model whose forward axis is -Z in model space, for a sim heading of 0 (+x).
    /// A rotation of theta about +Y maps model forward -Z to (-sin theta, 0, -cos theta); heading h must map to
    /// (cos h, 0, sin h), so theta = -h - pi/2.
    /// </summary>
    public const double ModelYawOffset = -Math.PI / 2;

    /// <summary>Sim position (metres, z up) to world position (metres, y up).</summary>
    public static Vec3 SimToWorld(Vec2 pos, double height) => new(pos.X, height, pos.Y);

    /// <summary>Sim ball position (x, y, z-up) to world (x, y-up, z).</summary>
    public static Vec3 SimToWorld(Vec3 pos) => new(pos.X, pos.Z, pos.Y);

    /// <summary>World position back to sim position; the height is the world y.</summary>
    public static Vec2 WorldToSim(Vec3 world, out double height)
    {
        height = world.Y;
        return new Vec2(world.X, world.Z);
    }

    /// <summary>Yaw about world +Y for a sim heading (radians, 0 = +x), normalised to (-pi, pi].</summary>
    public static double YawFromHeading(double heading) => Mover.NormalizeAngle(-heading + ModelYawOffset);

    /// <summary>Forward direction in world space of a -Z-forward model rotated by <paramref name="yaw"/> about +Y.</summary>
    public static Vec3 ForwardFromYaw(double yaw) => new(-Math.Sin(yaw), 0, -Math.Cos(yaw));
}
