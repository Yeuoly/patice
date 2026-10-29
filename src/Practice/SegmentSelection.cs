using System;
using Patice.Domain;

namespace Patice.Practice;

// Only the selection layer knows about catalog entries and their indices.
internal sealed class SegmentSelection(Segment[] segments)
{
    private int index;
    public bool HasSegments => segments.Length > 0;
    public bool Loop { get; set; } = true;

    public void Move(int direction) => index = (index + direction + segments.Length) % segments.Length;

    public SegmentPreview GetPreview(double durationMs)
    {
        var segment = segments[index];
        return new SegmentPreview(segment.name, segment.startMs, segment.endMs, index + 1, segments.Length,
            Loop, new PlaybackRange(Math.Max(1, segment.startMs - 2000d), Math.Min(durationMs - 1, segment.endMs + 2000d)));
    }
}
