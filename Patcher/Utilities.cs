using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Noggog;
using System.Collections.Immutable;
using System.Numerics;

namespace Patcher;

public record Segment(P2Float P1, P2Float P2);

public static class Utilities
{
    public static ImmutableArray<P2Float> Directions { get; } = ImmutableArray.CreateRange<P2Float>([
        new(0, 0), new(4096, 0), new(0, 4096), new(4096, 4096)
    ]);

    public static T NearestMult<T>(this T num, T fac, MidpointRounding mode = MidpointRounding.AwayFromZero) where T : IFloatingPoint<T>
    {
        return T.Round(num / fac, mode) * fac;
    }

    public static T NearestCeilingOf<T>(this T t, T factor) where T : IFloatingPoint<T> => NearestMult(t, factor, MidpointRounding.ToPositiveInfinity);
  
    public static T NearestFloorOf<T>(this T t, T factor) where T : IFloatingPoint<T> => NearestMult(t, factor, MidpointRounding.ToNegativeInfinity);


    public static T? TryGetParent<T>(this IModContext context)
    {
        context.TryGetParent<T>(out var item);
        return item;
    }

    public enum Orientation
    {
        Collinear, 
        Clockwise,
        CounterClockwise
    }

    public static Orientation FindOrientation(P2Float p1, P2Float p2, P2Float p3)
    {
        double i = (p2.Y - p1.Y) * (p3.X - p2.X) - (p2.X - p1.X) * (p3.Y - p2.Y);
        if (Math.Abs(i) < double.Epsilon)
            return Orientation.Collinear;
        return i > 0 ? Orientation.Clockwise : Orientation.CounterClockwise;
    }

    public static bool IsPointOnSegment(P2Float p1, P2Float p2, P2Float p3)
    {
        return 
            p2.X <= Math.Max(p1.X, p3.X) && p2.X >= Math.Min(p1.X, p3.X) &&
            p2.Y <= Math.Max(p1.Y, p3.Y) && p2.Y >= Math.Min(p1.Y, p3.Y);
    }

    public static bool Intersects(Segment s1, Segment s2)
    {
        var o1 = FindOrientation(s1.P1, s1.P2, s2.P1);
        var o2 = FindOrientation(s1.P1, s1.P2, s2.P2);
        var o3 = FindOrientation(s2.P1, s2.P2, s1.P1);
        var o4 = FindOrientation(s2.P1, s2.P2, s1.P2);

        if (o1 != o2 && o3 != o4)
            return true;

        if (o1 == Orientation.Collinear && IsPointOnSegment(s1.P1, s2.P1, s1.P2))
            return true;

        if (o2 == Orientation.Collinear && IsPointOnSegment(s1.P1, s2.P2, s1.P2))
            return true;

        if (o3 == Orientation.Collinear && IsPointOnSegment(s2.P1, s1.P1, s2.P2))
            return true;

        if (o4 == Orientation.Collinear && IsPointOnSegment(s2.P1, s1.P2, s2.P2))
            return true;

        return false;
    }
}
