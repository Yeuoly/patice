using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Patice.Domain;

namespace Patice.Segments;

public interface ISegmentStorage
{
    Segment[] Read(int id, string difficulty);
    void Write(int id, string difficulty, Segment[] segments);
}

public sealed class SegmentStorage : ISegmentStorage
{
    private readonly string root;
    private static readonly string[] difficulties = { "basic", "advanced", "expert", "master", "remaster", "utage" };

    public SegmentStorage(string path) => root = Path.GetFullPath(path);

    public Segment[] Read(int id, string difficulty)
    {
        var path = ChartPath(id, difficulty);
        if (!File.Exists(path)) return Array.Empty<Segment>();
        using var stream = File.OpenRead(path);
        var document = ReadDocument(stream);
        ValidateDocument(document);
        if (document.Id != id || document.Difficulty != difficulty)
            throw new InvalidDataException("Invalid segment document for the requested chart.");
        return document.Segments;
    }

    internal sealed class Document
    {
        internal int SchemaVersion = 1;
        internal string TimeBase = "music-start-ms";
        internal bool ExampleOnly;
        internal int Id;
        internal string Difficulty;
        internal Segment[] Segments;
    }

    internal static Document ReadDocument(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = JObject.Parse(reader.ReadToEnd());
        var id = (int)json["chart"]["musicId"];
        var difficulty = (string)json["chart"]["difficulty"];
        var segments = ((JArray)json["segments"]).Select(entry => new Segment(
            (string)entry["id"], (string)entry["name"],
            (int)entry["startMs"], (int)entry["endMs"])).ToArray();
        return new Document
        {
            SchemaVersion = (int)json["schemaVersion"], TimeBase = (string)json["timeBase"],
            ExampleOnly = (bool?)json["exampleOnly"] ?? false,
            Id = id, Difficulty = difficulty, Segments = segments
        };
    }

    // External data is checked at the storage boundary; CI uses the same rules.
    internal static void ValidateDocument(Document document)
    {
        if (document.SchemaVersion != 1 || document.TimeBase != "music-start-ms" || document.ExampleOnly)
            throw new InvalidDataException("Invalid segment document.");
        ValidateChart(document.Id, document.Difficulty);
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

    public void Write(int id, string difficulty, Segment[] segments)
    {
        ValidateDocument(new Document { Id = id, Difficulty = difficulty, Segments = segments });
        var path = ChartPath(id, difficulty);
        var json = new JObject
        {
            ["schemaVersion"] = 1,
            ["timeBase"] = "music-start-ms",
            ["chart"] = new JObject { ["musicId"] = id, ["difficulty"] = difficulty },
            ["segments"] = new JArray(segments.Select(segment => new JObject
            {
                ["id"] = segment.id, ["name"] = segment.name,
                ["startMs"] = segment.startMs, ["endMs"] = segment.endMs
            }))
        };
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, json.ToString(Formatting.Indented), new UTF8Encoding(false));
    }

    private static void ValidateChart(int id, string difficulty)
    {
        if (id <= 0 || Array.IndexOf(difficulties, difficulty) < 0)
            throw new ArgumentException("Invalid chart ID or difficulty.");
    }

    private string ChartPath(int id, string difficulty)
    {
        ValidateChart(id, difficulty);
        return Path.Combine(root, id.ToString(System.Globalization.CultureInfo.InvariantCulture), difficulty + ".json");
    }
}
