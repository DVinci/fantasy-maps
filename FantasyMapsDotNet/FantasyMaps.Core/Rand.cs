namespace FantasyMaps.Core;

public static class Rand
{
    private static double? _spare;

    public static double Normal()
    {
        if (_spare.HasValue) { var s = _spare.Value; _spare = null; return s; }
        double u, v, mag;
        do {
            u = Random.Shared.NextDouble() * 2 - 1;
            v = Random.Shared.NextDouble() * 2 - 1;
            mag = u * u + v * v;
        } while (mag >= 1 || mag == 0);
        double mul = Math.Sqrt(-2 * Math.Log(mag) / mag);
        _spare = v * mul;
        return u * mul;
    }

    public static double Uniform(double lo, double hi) => lo + Random.Shared.NextDouble() * (hi - lo);
}
