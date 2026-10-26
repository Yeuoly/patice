using System;
using System.IO;
using System.Linq;
using LitJson;
using Patice.Domain;

namespace Patice.Segments;

internal static class SegmentJson
{
    static SegmentJson()
    {
        JsonMapper.RegisterImporter<string, Guid>(value => new Guid(value));
        JsonMapper.RegisterExporter<Guid>((value, writer) => writer.Write(value.ToString()));
        JsonMapper.RegisterImporter<string, Difficulty>(value => (Difficulty)Enum.Parse(typeof(Difficulty), value, true));
        JsonMapper.RegisterExporter<Difficulty>((value, writer) => writer.Write(value.ToString().ToLowerInvariant()));
    }

    internal static Segment[] Read(TextReader reader)
    {
        var json = new JsonReader(reader) { SkipNonMembers = false };
        var segments = JsonMapper.ToObject<Segment[]>(json);
        Validate(segments);
        return segments;
    }

    // External data is checked at the storage boundary; CI uses the same rules.
    internal static void Validate(Segment[] segments)
    {
        if (segments == null || segments.Length == 0)
            throw new InvalidDataException("Invalid segment list.");
        foreach (var segment in segments)
        {
            if (segment == null || segment.id == Guid.Empty ||
                string.IsNullOrWhiteSpace(segment.name) || segment.name.Length > 40 ||
                segment.startMs < 0 || segment.endMs <= segment.startMs)
                throw new InvalidDataException("Invalid segment ID, name or range.");
        }
        if (segments.GroupBy(segment => new { segment.musicId, segment.difficulty, segment.id })
            .Any(group => group.Count() > 1))
            throw new InvalidDataException("Duplicate segment identity.");
    }

    internal static void Write(TextWriter writer, Segment[] segments)
    {
        var json = new JsonWriter(writer) { PrettyPrint = true };
        JsonMapper.ToJson(segments, json);
    }
}
