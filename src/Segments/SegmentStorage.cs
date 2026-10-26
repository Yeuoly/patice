using System;
using System.IO;
using System.Text;
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

    public SegmentStorage(string path) => root = Path.GetFullPath(path);

    public Segment[] Read(int id, Difficulty difficulty)
    {
        var path = ChartPath(id, difficulty);
        if (!File.Exists(path)) return Array.Empty<Segment>();
        using var reader = new StreamReader(path, Encoding.UTF8);
        return SegmentJson.Read(reader);
    }

    public void Write(int id, Difficulty difficulty, Segment[] segments)
    {
        SegmentJson.Validate(segments);
        var path = ChartPath(id, difficulty);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        SegmentJson.Write(writer, segments);
    }

    private string ChartPath(int id, Difficulty difficulty)
    {
        return Path.Combine(root, id.ToString(System.Globalization.CultureInfo.InvariantCulture), difficulty.ToString().ToLowerInvariant() + ".json");
    }
}
