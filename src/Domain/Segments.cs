using System;

namespace Patice.Domain;

public sealed class Segment
{
    public int musicId;
    public Difficulty difficulty;
    public Guid id;
    public string name;
    public int startMs;
    public int endMs;

    public Segment() { }

    public Segment(int musicId, Difficulty difficulty, Guid id, string name, int startMs, int endMs)
    {
        this.musicId = musicId;
        this.difficulty = difficulty;
        this.id = id;
        this.name = name;
        this.startMs = startMs;
        this.endMs = endMs;
    }
}
