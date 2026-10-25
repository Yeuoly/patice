using Newtonsoft.Json;
using Patice.Domain;

namespace Patice.Segments;

internal sealed class ChartIdentity
{
    [JsonProperty("musicId")]
    public int MusicId { get; set; }

    [JsonProperty("difficulty")]
    public Difficulty Difficulty { get; set; }
}

internal sealed class SegmentDocument
{
    internal const int CurrentVersion = 1;
    internal const string MusicTimeBase = "music-start-ms";

    [JsonProperty("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonProperty("timeBase")]
    public string TimeBase { get; set; }

    [JsonProperty("exampleOnly", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool ExampleOnly { get; set; }

    [JsonProperty("chart")]
    public ChartIdentity Chart { get; set; }

    [JsonProperty("segments")]
    public Segment[] Segments { get; set; }
}
