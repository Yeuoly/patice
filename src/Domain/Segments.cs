using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AquaMai.Mods.UX.PracticeMode.Domain;

[Serializable]
public sealed class ChartIdentity
{
    public int musicId;
    public string difficulty;
}

[Serializable]
public sealed class Segment
{
    public string id;
    public string name;
    public int startMs;
    public int endMs;
}

[Serializable]
public sealed class SegmentDocument
{
    public int schemaVersion;
    public string timeBase;
    public bool exampleOnly;
    public ChartIdentity chart;
    public Segment[] segments;

    public void Validate(ChartIdentity actual, double durationMs)
    {
        if (schemaVersion != 1 || timeBase != "music-start-ms" || exampleOnly)
            throw new ArgumentException("Unsupported segment document.");
        if (chart == null || actual == null || chart.musicId != actual.musicId ||
            chart.difficulty != actual.difficulty)
            throw new ArgumentException("Segment chart identity does not match the loaded chart.");
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
