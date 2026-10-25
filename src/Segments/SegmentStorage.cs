using System;
using System.IO;
using System.Linq;
using System.Text;
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
        if (document.Id != id || document.Difficulty != difficulty)
            throw new InvalidDataException("Invalid segment document for the requested chart.");
        return document.Segments;
    }

    internal sealed class Document
    {
        internal int Id;
        internal string Difficulty;
        internal Segment[] Segments;
    }

    internal static Document ReadDocument(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var json = JObject.Parse(reader.ReadToEnd());
        if ((int)json["schemaVersion"] != 1 || (string)json["timeBase"] != "music-start-ms" ||
            ((bool?)json["exampleOnly"] ?? false))
            throw new InvalidDataException("Invalid segment document.");
        var id = (int)json["chart"]["musicId"];
        var difficulty = (string)json["chart"]["difficulty"];
        ValidateChart(id, difficulty);
        var segments = ((JArray)json["segments"]).Select(entry => new Segment(
            (string)entry["id"], (string)entry["name"],
            (int)entry["startMs"], (int)entry["endMs"])).ToArray();
        if (segments.Length == 0 || segments.Select(segment => segment.id).Distinct().Count() != segments.Length)
            throw new InvalidDataException("Segment documents require unique, nonempty entries.");
        return new Document { Id = id, Difficulty = difficulty, Segments = segments };
    }

    public void Write(int id, string difficulty, Segment[] segments)
    {
        if (segments == null) throw new ArgumentNullException(nameof(segments));
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
