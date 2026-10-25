using System;
using System.Collections.Generic;
using Patice.Domain;

namespace Patice.Segments;

public sealed class LocalSegmentStore : ISegmentStore
{
    private readonly ISegmentStorage storage;

    public LocalSegmentStore(string path) : this(new SegmentStorage(path)) { }

    public LocalSegmentStore(ISegmentStorage storage) =>
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));

    public Segment[] Get(int id, Difficulty difficulty) => storage.Read(id, difficulty);

    public void Store(int id, Difficulty difficulty, Segment segment)
    {
        if (segment == null) throw new ArgumentNullException(nameof(segment));
        var segments = new List<Segment>(Get(id, difficulty));
        var index = segments.FindIndex(existing => existing.id == segment.id);
        if (index < 0) segments.Add(segment);
        else segments[index] = segment;
        storage.Write(id, difficulty, segments.ToArray());
    }
}
