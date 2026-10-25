using System.Collections.Generic;
using Patice.Domain;

namespace Patice.Segments;

public interface ISegmentStore
{
    Segment[] Get(int id, Difficulty difficulty);
}

public sealed class SegmentManagement
{
    private readonly List<ISegmentStore> stores = new();

    public void RegisterStore(ISegmentStore store) => stores.Add(store);

    public Segment[] Get(int id, Difficulty difficulty)
    {
        var result = new List<Segment>();
        foreach (var store in stores) result.AddRange(store.Get(id, difficulty));
        return result.ToArray();
    }
}
