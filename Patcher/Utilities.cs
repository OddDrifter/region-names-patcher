using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;
using Noggog;

namespace Patcher;

public record Segment(P2Float P1, P2Float P2);

public static class Utilities
{
    //public static P2Float Min(P2Float i, P2Float k) => new(Math.Min(i.X, k.X), Math.Min(i.Y, k.Y));

    //public static P2Float Round(this P2Float p) => new((float)Math.Round(p.X), (float)Math.Round(p.Y)); 

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
