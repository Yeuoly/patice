namespace Patice.Practice;

public readonly struct PlaybackRange(double startMs, double endMs)
{
    public double StartMs { get; } = startMs;
    public double EndMs { get; } = endMs;
}

// A playback instruction has no dependency on the segment catalog.
internal sealed class PlaybackRequest(double startMs, PlaybackRange? range, bool loop)
{
    public double StartMs { get; } = startMs;
    public PlaybackRange? Range { get; } = range;
    public bool Loop { get; } = loop && range.HasValue;
}
