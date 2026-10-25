using System;
using System.Collections.Generic;
using Patice.Domain;

namespace Patice.Segments;

public sealed class BuiltinSegments : ISegmentStore
{
    private static readonly Dictionary<int, Dictionary<string, Segment[]>> charts = new()
    {
        [11820] = new()
        {
            ["basic"] = new[]
            {
                new Segment("opening", "开场", 2000, 9000),
                new Segment("passage", "后段", 15000, 23000)
            }
        },
        [12025] = new()
        {
            ["master"] = new[]
            {
                new Segment("opening", "开场", 2000, 9000),
                new Segment("passage", "后段", 15000, 23000)
            }
        }
    };

    public Segment[] Get(int id, string difficulty) =>
        charts.TryGetValue(id, out var levels) && levels.TryGetValue(difficulty, out var segments)
            ? (Segment[])segments.Clone() : Array.Empty<Segment>();
}
