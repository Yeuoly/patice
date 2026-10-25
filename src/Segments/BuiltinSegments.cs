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
        var result = new Dictionary<int, Dictionary<Difficulty, Segment[]>>();
        var assembly = typeof(BuiltinSegments).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Patice.Segments.", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            var document = SegmentStorage.ReadDocument(stream);
            if (!result.TryGetValue(document.Chart.MusicId, out var levels))
                result.Add(document.Chart.MusicId, levels = new Dictionary<Difficulty, Segment[]>());
            var entries = levels.TryGetValue(document.Chart.Difficulty, out var existing)
                ? existing.Concat(document.Segments).ToArray() : document.Segments;
            levels[document.Chart.Difficulty] = entries;
        }
        return result;
    }

    public Segment[] Get(int id, Difficulty difficulty) =>
        charts.TryGetValue(id, out var levels) && levels.TryGetValue(difficulty, out var segments)
            ? (Segment[])segments.Clone() : Array.Empty<Segment>();
}
