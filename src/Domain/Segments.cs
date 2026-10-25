using System;

namespace Patice.Domain;

public sealed class Segment
{
    public readonly int musicId;
    public readonly Difficulty difficulty;
    public readonly Guid id;
    public readonly string name;
    public readonly int startMs;
    public readonly int endMs;

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
