using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
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
    private static readonly JsonSerializerSettings jsonSettings = new()
    {
        MissingMemberHandling = MissingMemberHandling.Error,
        Converters = { new StringEnumConverter(new CamelCaseNamingStrategy(), false) }
    };

    public SegmentStorage(string path) => root = Path.GetFullPath(path);

    public Segment[] Read(int id, Difficulty difficulty)
    {
        var path = ChartPath(id, difficulty);
        if (!File.Exists(path)) return Array.Empty<Segment>();
        using var stream = File.OpenRead(path);
        var document = ReadDocument(stream);
        ValidateDocument(document);
        if (document.Chart.MusicId != id || document.Chart.Difficulty != difficulty)
            throw new InvalidDataException("Invalid segment document for the requested chart.");
        return document.Segments;
    }

    internal static SegmentDocument ReadDocument(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var json = new JsonTextReader(reader);
        return JsonSerializer.Create(jsonSettings).Deserialize<SegmentDocument>(json);
    }

    // External data is checked at the storage boundary; CI uses the same rules.
    internal static void ValidateDocument(SegmentDocument document)
    {
        if (document == null || document.SchemaVersion != SegmentDocument.CurrentVersion ||
            document.TimeBase != SegmentDocument.MusicTimeBase || document.ExampleOnly || document.Chart == null)
            throw new InvalidDataException("Invalid segment document.");
        ValidateChart(document.Chart.MusicId, document.Chart.Difficulty);
        var segments = document.Segments;
        if (segments == null || segments.Length == 0 || segments.Length > 256)
            throw new InvalidDataException("Invalid segment list.");
        var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var segment in segments)
        {
            if (segment == null || string.IsNullOrWhiteSpace(segment.id) ||
                !Regex.IsMatch(segment.id, "^[a-z0-9][a-z0-9-]{0,63}$") || !ids.Add(segment.id) ||
                string.IsNullOrWhiteSpace(segment.name) || segment.name.Length > 40 ||
                segment.startMs < 0 || segment.endMs <= segment.startMs)
                throw new InvalidDataException("Invalid segment ID, name or range.");
        }
    }

    public void Write(int id, Difficulty difficulty, Segment[] segments)
    {
        var document = new SegmentDocument
        {
            SchemaVersion = SegmentDocument.CurrentVersion,
            TimeBase = SegmentDocument.MusicTimeBase,
            Chart = new ChartIdentity { MusicId = id, Difficulty = difficulty },
            Segments = segments
        };
        ValidateDocument(document);
        var path = ChartPath(id, difficulty);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        using var json = new JsonTextWriter(writer) { Formatting = Formatting.Indented };
        JsonSerializer.Create(jsonSettings).Serialize(json, document);
    }

    private static void ValidateChart(int id, Difficulty difficulty)
    {
        if (id <= 0 || !Enum.IsDefined(typeof(Difficulty), difficulty))
            throw new ArgumentException("Invalid chart ID or difficulty.");
    }

    private string ChartPath(int id, Difficulty difficulty)
    {
        ValidateChart(id, difficulty);
        return Path.Combine(root, id.ToString(System.Globalization.CultureInfo.InvariantCulture), difficulty.ToString().ToLowerInvariant() + ".json");
    }
}
