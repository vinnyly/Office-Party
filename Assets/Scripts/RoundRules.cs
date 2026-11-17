using System;

public static class RoundRules
{
    // Counts completed seconds, allowing for tiny floating-point rounding errors.
    public static int WholeSeconds(double seconds)
    {
        return (int)Math.Floor(
            Math.Max(0.0, seconds) + 0.0000001
        );
    }

    // Awards survival points and subtracts whole seen-second penalties.
    public static int Score(
        double survived,
        double seen,
        int penaltyPerSecond)
    {
        long earned = 10L * WholeSeconds(survived);

        long penalty =
            (long)Math.Max(0, penaltyPerSecond) *
            WholeSeconds(Math.Min(survived, seen));

        return (int)Math.Min(
            int.MaxValue,
            Math.Max(0L, earned - penalty)
        );
    }

    // Finds contact along a frame's relative movement: 0..1 for a catch, or -1.
    public static double CatchFraction(
        double x,
        double z,
        double endX,
        double endZ,
        double radius)
    {
        double c = x * x + z * z - radius * radius;

        if (c <= 0.0)
            return 0.0;

        double dx = endX - x;
        double dz = endZ - z;

        double a = dx * dx + dz * dz;

        if (a < 1e-12)
            return -1.0;

        double b = 2.0 * (x * dx + z * dz);
        double discriminant = b * b - 4.0 * a * c;

        if (discriminant < 0.0)
            return -1.0;

        double t =
            (-b - Math.Sqrt(discriminant)) / (2.0 * a);

        return t >= 0.0 && t <= 1.0 ? t : -1.0;
    }
}