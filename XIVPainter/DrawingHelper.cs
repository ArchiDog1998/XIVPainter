﻿using Dalamud.Logging;

namespace XIVPainter;

internal static class DrawingHelper
{
    public static uint ToColor(this Vector3 color, float alpha)
        => new Vector4(color.X, color.Y, color.Z, alpha).ToColor();

    public static uint ToColor(this Vector4 color)
        => ImGui.ColorConvertFloat4ToU32(color);

    public static uint ColorAlpha(this uint color, float alpha)
    {
        var c = ImGui.ColorConvertU32ToFloat4(color);
        c.W *= alpha;
        return c.ToColor();
    }

    public static bool IsPointInside(Vector3 pt, IEnumerable<IEnumerable<Vector3>> pts)
    {
        var count = 0;

        foreach (var partPts in pts) 
        {
            if(!partPts.Any()) continue;

            Vector3 prePt = default;
            bool isFirst = true;
            foreach (var rightPt in partPts)
            {
                if (isFirst)
                {
                    isFirst = false;
                    prePt = rightPt;
                    continue;
                }
                if (InLine(prePt, rightPt, pt)) count++;
                prePt = rightPt;
            }

            if (InLine(prePt, partPts.First(), pt)) count++;
        }

        return count % 2 == 1;
    }

    static bool InLine(Vector3 pt1, Vector3 pt2, Vector3 pt)
        => (pt.Z < pt1.Z) != (pt.Z < pt2.Z) &&
            pt.X < pt1.X + (pt.Z - pt1.Z) / (pt2.Z - pt1.Z) * (pt2.X - pt1.X);

    public static IEnumerable<Vector2[]> ConvexPoints(Vector2[] points)
    {
        int index = 0;
        float leftBottom = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            var pt = points[i];
            var value = pt.X + pt.Y;
            if (value < leftBottom)
            {
                index = i;
                leftBottom = value;
            }
        }

        if (PointCross(points, index, out _, out _) > 0.1f)
        {
            points = points.Reverse().ToArray();
        }
        return ConvexPointsOrdered(points);
    }

    static IEnumerable<Vector2[]> ConvexPointsOrdered(Vector2[] points)
    {
        if (points.Length < 4)
        {
            return new Vector2[][] { points };
        }

        int breakIndex = -1;
        Vector2 dir = Vector2.Zero;
        for (int i = 0; i < points.Length; i++)
        {
            if (PointCross(points, i, out var vec1, out var vec2) > 0.1f)
            {
                breakIndex = i;
                dir = vec1 - vec2;
                dir /= dir.Length();
                break;
            }
        }

        if (breakIndex < 0)
        {
            return new Vector2[][] { points };
        }
        else
        {
            try
            {
                var pt = points[breakIndex];
                var index = 0;
                double maxValue = double.MinValue;
                for (int i = 0; i < points.Length; i++)
                {
                    if (Math.Abs(i - breakIndex) < 2) continue;
                    if (Math.Abs(i + points.Length - breakIndex) < 2) continue;
                    if (Math.Abs(i - points.Length - breakIndex) < 2) continue;
                    var d = points[i] - pt;
                    d /= d.Length();

                    var angle = Vector2.Dot(d, dir);

                    if (angle > maxValue)
                    {
                        maxValue = angle;
                        index = i;
                    }
                }

                var minIndex = Math.Min(breakIndex, index);
                var maxIndex = Math.Max(breakIndex, index);

                var list1 = new List<Vector2>(points.Length);
                var list2 = new List<Vector2>(points.Length);
                for (int i = 0; i < points.Length; i++)
                {
                    if (i <= minIndex || i >= maxIndex)
                    {
                        list1.Add(points[i]);
                    }

                    if (i >= minIndex && i <= maxIndex)
                    {
                        list2.Add(points[i]);
                    }
                }

                return ConvexPointsOrdered(list1.ToArray()).Union(ConvexPointsOrdered(list2.ToArray())).Where(l => l.Count() > 2);
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Bad at drawing");
                return new Vector2[][] { points };
            }
        }
    }

    private static float PointCross(Vector2[] pts, int index, out Vector2 vec1, out Vector2 vec2)
    {
        var length = pts.Length;
        var prePt = pts[(index - 1 + length) % length];
        var midPt = pts[index];
        var nextPt = pts[(index + 1) % length];

        vec1 = midPt - prePt;
        vec2 = nextPt - midPt;

        vec1 /= vec1.Length();
        vec2 /= vec2.Length();

        return Vector3.Cross(new Vector3(vec1.X, vec1.Y, 0), new Vector3(vec2.X, vec2.Y, 0)).Z;
    }

    public static Func<double, double> EaseFuncRemap(EaseFuncType inType, EaseFuncType outType)
    {
        if (inType == EaseFuncType.None)
        {
            if (outType == EaseFuncType.None)
            {
                return (x) => x;
            }
            else
            {
                return (x) => FindOutFuction(outType)(x);
            }
        }
        else
        {
            if (outType == EaseFuncType.None)
            {
                return (x) => 1 - FindOutFuction(inType)(1 - x);
            }
            else
            {
                return (x) => x < 0.5 ? (1 - FindOutFuction(inType)(1 - (2 * x))) / 2
                                      : (1 + FindOutFuction(outType)((2 * x) - 1)) / 2;
            }
        }
    }

    const double c1 = 1.70158;
    const double c3 = c1 + 1;
    const double c4 = (2 * Math.PI) / 3;
    const double n1 = 7.5625;
    const double d1 = 2.75;

    static Func<double, double> FindOutFuction(EaseFuncType outType) => outType switch 
    {
        EaseFuncType.Sine => x => Math.Sin(x * Math.PI / 2),
        EaseFuncType.Quad => x => 1 - ((1 - x) * (1 - x)),
        EaseFuncType.Cubic => x => 1 - Math.Pow(1 - x, 3),
        EaseFuncType.Quart => x => 1 - Math.Pow(1 - x, 4),
        EaseFuncType.Quint => x => 1 - Math.Pow(1 - x, 5),
        EaseFuncType.Expo => x => x == 1 ? 1 : 1 - Math.Pow(2, -10 * x),
        EaseFuncType.Circ => x => Math.Sqrt(1 - Math.Pow(x - 1, 2)),
        EaseFuncType.Back => x => 1 + (c3 * Math.Pow(x - 1, 3)) + (c1 * Math.Pow(x - 1, 2)),
        EaseFuncType.Elastic => x => x == 0 ? 0 : x == 1 ? 1 : (Math.Pow(2, -10 * x) * Math.Sin(((x * 10) - 0.75) * c4)) + 1,
        EaseFuncType.Bounce => x =>
        {
            if (x < 1 / d1)
            {
                return n1 * x * x;
            }
            else if (x < 2 / d1)
            {
                return (n1 * (x -= 1.5 / d1) * x) + 0.75;
            }
            else if (x < 2.5 / d1)
            {
                return (n1 * (x -= 2.25 / d1) * x) + 0.9375;
            }
            else
            {
                return (n1 * (x -= 2.625 / d1) * x) + 0.984375;
            }
        } ,
        _ => x => x,
    };
}

public enum EaseFuncType
{
    None,

    /// <summary>
    /// <see href="https://easings.net#easeInSine">In</see>,
    /// <see href="https://easings.net#easeOutSine">Out</see>
    /// </summary>
    Sine,

    /// <summary>
    /// <see href="https://easings.net#easeInQuad">In</see>,
    /// <see href="https://easings.net#easeOutQuad">Out</see>
    /// </summary>
    Quad,

    /// <summary>
    /// <see href="https://easings.net#easeInCubic">In</see>,
    /// <see href="https://easings.net#easeOutCubic">Out</see>
    /// </summary>
    Cubic,

    /// <summary>
    /// <see href="https://easings.net#easeInQuart">In</see>,
    /// <see href="https://easings.net#easeOutQuart">Out</see>
    /// </summary>
    Quart,

    /// <summary>
    /// <see href="https://easings.net#easeInQuint">In</see>,
    /// <see href="https://easings.net#easeOutQuint">Out</see>
    /// </summary>
    Quint,

    /// <summary>
    /// <see href="https://easings.net#easeInExpo">In</see>,
    /// <see href="https://easings.net#easeOutExpo">Out</see>
    /// </summary>
    Expo,

    /// <summary>
    /// <see href="https://easings.net#easeInCirc">In</see>,
    /// <see href="https://easings.net#easeOutCirc">Out</see>
    /// </summary>
    Circ,

    /// <summary>
    /// <see href="https://easings.net#easeInBack">In</see>,
    /// <see href="https://easings.net#easeOutBack">Out</see>
    /// </summary>
    Back,

    /// <summary>
    /// <see href="https://easings.net#easeInElastic">In</see>,
    /// <see href="https://easings.net#easeOutElastic">Out</see>
    /// </summary>
    Elastic,

    /// <summary>
    /// <see href="https://easings.net#easeInBounce">In</see>,
    /// <see href="https://easings.net#easeOutBounce">Out</see>
    /// </summary>
    Bounce,
}
