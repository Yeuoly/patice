using System;
using System.Collections.Generic;
using Patice.Domain;

namespace Patice.Segments;

public sealed partial class BuiltinSegments : ISegmentStore
{
    private static readonly Dictionary<int, Dictionary<Difficulty, Segment[]>> charts = CreateCharts();

    private static partial Dictionary<int, Dictionary<Difficulty, Segment[]>> CreateCharts();

    public Segment[] Get(int id, Difficulty difficulty) =>
        charts.TryGetValue(id, out var levels) && levels.TryGetValue(difficulty, out var segments)
            ? (Segment[])segments.Clone() : Array.Empty<Segment>();
}
