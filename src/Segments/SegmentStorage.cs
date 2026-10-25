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
        var json = JObject.Parse(File.ReadAllText(path));
        if ((int)json["schemaVersion"] != 1 || (string)json["timeBase"] != "music-start-ms" ||
            ((bool?)json["exampleOnly"] ?? false) ||
            (int)json["chart"]["musicId"] != id || (string)json["chart"]["difficulty"] != difficulty)
            throw new InvalidDataException("Invalid segment document for the requested chart.");
        return ((JArray)json["segments"]).Select(entry => new Segment(
            (string)entry["id"], (string)entry["name"],
            (int)entry["startMs"], (int)entry["endMs"])).ToArray();
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

    private string ChartPath(int id, string difficulty)
    {
        if (id <= 0 || Array.IndexOf(difficulties, difficulty) < 0)
            throw new ArgumentException("Invalid chart ID or difficulty.");
        return Path.Combine(root, id.ToString(System.Globalization.CultureInfo.InvariantCulture), difficulty + ".json");
    }
}
