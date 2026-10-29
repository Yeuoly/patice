namespace Patice.Practice;

public sealed class SegmentPreview(string name, double startMs, double endMs, int page, int count,
    bool loop, PlaybackRange playbackRange)
{
    public string Name { get; } = name;
    public double StartMs { get; } = startMs;
    public double EndMs { get; } = endMs;
    public int Page { get; } = page;
    public int Count { get; } = count;
    public bool Loop { get; } = loop;
    public PlaybackRange PlaybackRange { get; } = playbackRange;
}

public sealed class SeekViewState(double positionMs, double? startMs, double? endMs, bool loop)
{
    public double PositionMs { get; } = positionMs;
    public double? StartMs { get; } = startMs;
    public double? EndMs { get; } = endMs;
    public bool CanLoop => StartMs.HasValue && EndMs.HasValue && EndMs > StartMs;
    public bool Loop { get; } = loop;
}

// Each page receives its own data. Rendering never indexes the segment catalog.
public sealed class PracticeViewState(PracticeState state, float rate, int countdown, float selectionProgress,
    SegmentPreview segment, SeekViewState seek, string message)
{
    public PracticeState State { get; } = state;
    public float Rate { get; } = rate;
    public int Countdown { get; } = countdown;
    public float SelectionProgress { get; } = selectionProgress;
    public SegmentPreview Segment { get; } = segment;
    public SeekViewState Seek { get; } = seek;
    public string Message { get; } = message;
}
