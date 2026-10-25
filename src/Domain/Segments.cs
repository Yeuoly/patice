using System;
using System.IO;
using Manager;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Patice.Domain;

public sealed class ChartIdentity
{
    public int musicId;
    public string difficulty;
}

public sealed class Segment
{
    public string id;
    public string name;
    public int startMs;
    public int endMs;
}

public sealed class SegmentDocument
{
    public int schemaVersion;
    public string timeBase;
    public bool exampleOnly;
    public ChartIdentity chart;
    public Segment[] segments;

    public static SegmentDocument Load(int monitor)
    {
        var musicId = GameManager.SelectMusicID[monitor];
        var difficulty = GameManager.SelectDifficultyID[monitor];
        var difficulties = new[] { "basic", "advanced", "expert", "master", "remaster", "utage" };
        if (difficulty < 0 || difficulty >= difficulties.Length) throw new InvalidDataException("Unknown chart difficulty.");
        var identity = new ChartIdentity { musicId = musicId, difficulty = difficulties[difficulty] };
        var path = Path.Combine(Environment.CurrentDirectory, $"UserData/PracticeSegments/{musicId}/{identity.difficulty}.json");
        if (new FileInfo(path).Length > 256 * 1024) throw new InvalidDataException("Segment document exceeds 256 KiB.");
        // Read the fixed document shape directly; no runtime contract discovery.
        var json = JObject.Parse(File.ReadAllText(path));
        var entries = new List<Segment>();
        foreach (var entry in (JArray)json["segments"])
            entries.Add(new Segment
            {
                id = (string)entry["id"], name = (string)entry["name"],
                startMs = (int)entry["startMs"], endMs = (int)entry["endMs"]
            });
        var document = new SegmentDocument
        {
            schemaVersion = (int)json["schemaVersion"],
            timeBase = (string)json["timeBase"],
            exampleOnly = (bool?)json["exampleOnly"] ?? false,
            chart = new ChartIdentity
            {
                musicId = (int)json["chart"]["musicId"],
                difficulty = (string)json["chart"]["difficulty"]
            },
            segments = entries.ToArray()
        };
        document.Validate(identity, NotesManager.Instance().getPlayFinalMsec() - Playback.MusicStartDelayMs);
        return document;
    }

    public void Validate(ChartIdentity actual, double durationMs)
    {
        if (schemaVersion != 1 || timeBase != "music-start-ms" || exampleOnly)
            throw new ArgumentException("Unsupported segment document.");
        if (chart == null || actual == null || chart.musicId != actual.musicId ||
            chart.difficulty != actual.difficulty)
            throw new ArgumentException($"Segment chart {chart?.musicId}/{chart?.difficulty} does not match loaded chart {actual?.musicId}/{actual?.difficulty}.");
        if (segments == null || segments.Length == 0 || segments.Length > 256 || double.IsNaN(durationMs) || durationMs <= 0)
            throw new ArgumentException("Invalid segment list or chart duration.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var segment in segments)
            if (segment == null || string.IsNullOrWhiteSpace(segment.id) || !Regex.IsMatch(segment.id, "^[a-z0-9][a-z0-9-]{0,63}$") || !ids.Add(segment.id) ||
                string.IsNullOrWhiteSpace(segment.name) || segment.name.Length > 40 ||
                segment.startMs < 0 || segment.endMs <= segment.startMs || segment.endMs > durationMs)
                throw new ArgumentException("Invalid segment name, ID or range.");
    }
}
