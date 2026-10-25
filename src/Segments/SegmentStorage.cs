using System;
using System.IO;
using System.Text;
using System.Linq;
using LitJson;
using Patice.Domain;

namespace Patice.Segments;

public interface ISegmentStorage
{
    Segment[] Read(int id, Difficulty difficulty);
    void Write(int id, Difficulty difficulty, Segment[] segments);
}

public sealed class SegmentStorage : ISegmentStorage
{
    private readonly string root;
    static SegmentStorage()
    {
        JsonMapper.RegisterImporter<string, Guid>(value => new Guid(value));
        JsonMapper.RegisterExporter<Guid>((value, writer) => writer.Write(value.ToString()));
        JsonMapper.RegisterImporter<string, Difficulty>(value => (Difficulty)Enum.Parse(typeof(Difficulty), value, true));
        JsonMapper.RegisterExporter<Difficulty>((value, writer) => writer.Write(value.ToString().ToLowerInvariant()));
    }

    public SegmentStorage(string path) => root = Path.GetFullPath(path);

    public Segment[] Read(int id, Difficulty difficulty)
    {
        var path = ChartPath(id, difficulty);
        if (!File.Exists(path)) return Array.Empty<Segment>();
        using var stream = File.OpenRead(path);
        var segments = ReadSegments(stream);
        ValidateSegments(segments);
        return segments;
    }

    internal static Segment[] ReadSegments(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = new JsonReader(reader) { SkipNonMembers = false };
        return JsonMapper.ToObject<Segment[]>(json);
    }

    // External data is checked at the storage boundary; CI uses the same rules.
    internal static void ValidateSegments(Segment[] segments)
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

    public void Write(int id, Difficulty difficulty, Segment[] segments)
    {
        ValidateSegments(segments);
        var path = ChartPath(id, difficulty);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        var json = new JsonWriter(writer) { PrettyPrint = true };
        JsonMapper.ToJson(segments, json);
    }

    private string ChartPath(int id, Difficulty difficulty)
    {
        return Path.Combine(root, id.ToString(System.Globalization.CultureInfo.InvariantCulture), difficulty.ToString().ToLowerInvariant() + ".json");
    }
}
