using System;
using System.Collections.Generic;
using System.Linq;
using Patice.Domain;

namespace Patice.Segments;

public sealed class BuiltinSegments : ISegmentStore
{
    private static readonly Dictionary<int, Dictionary<Difficulty, Segment[]>> charts = Load();

    private static Dictionary<int, Dictionary<Difficulty, Segment[]>> Load()
    {
        var segments = new List<Segment>();
        var assembly = typeof(BuiltinSegments).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Patice.Segments.", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            segments.AddRange(SegmentStorage.ReadSegments(stream));
        }
        return segments.GroupBy(segment => segment.musicId).ToDictionary(
            chart => chart.Key,
            chart => chart.GroupBy(segment => segment.difficulty)
                .ToDictionary(level => level.Key, level => level.ToArray()));
    }

    public Segment[] Get(int id, Difficulty difficulty) =>
        charts.TryGetValue(id, out var levels) && levels.TryGetValue(difficulty, out var segments)
            ? (Segment[])segments.Clone() : Array.Empty<Segment>();
}
