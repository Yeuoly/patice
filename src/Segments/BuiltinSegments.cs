using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Patice.Domain;

namespace Patice.Segments;

public sealed class BuiltinSegments : ISegmentStore
{
    private static readonly Dictionary<int, Dictionary<string, Segment[]>> charts = Load();

    private static Dictionary<int, Dictionary<string, Segment[]>> Load()
    {
        var result = new Dictionary<int, Dictionary<string, Segment[]>>();
        var assembly = typeof(BuiltinSegments).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Patice.Segments.", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            var document = SegmentStorage.ReadDocument(stream);
            if (!result.TryGetValue(document.Id, out var levels))
                result.Add(document.Id, levels = new Dictionary<string, Segment[]>());
            var entries = levels.TryGetValue(document.Difficulty, out var existing)
                ? existing.Concat(document.Segments).ToArray() : document.Segments;
            if (entries.Select(segment => segment.id).Distinct().Count() != entries.Length)
                throw new InvalidDataException($"Duplicate built-in segment ID in {resource}.");
            levels[document.Difficulty] = entries;
        }
        return result;
    }

    public Segment[] Get(int id, string difficulty) =>
        charts.TryGetValue(id, out var levels) && levels.TryGetValue(difficulty, out var segments)
            ? (Segment[])segments.Clone() : Array.Empty<Segment>();
}
