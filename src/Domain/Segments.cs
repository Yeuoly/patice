namespace Patice.Domain;

public sealed class Segment
{
    public readonly string id;
    public readonly string name;
    public readonly int startMs;
    public readonly int endMs;

    public Segment(string id, string name, int startMs, int endMs)
    {
        this.id = id;
        this.name = name;
        this.startMs = startMs;
        this.endMs = endMs;
    }
}
