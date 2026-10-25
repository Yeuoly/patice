using System;
using System.Text.RegularExpressions;

namespace Patice.Domain;

public sealed class Segment
{
    public readonly string id;
    public readonly string name;
    public readonly int startMs;
    public readonly int endMs;

    public Segment(string id, string name, int startMs, int endMs)
    {
        if (string.IsNullOrWhiteSpace(id) || !Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{0,63}$"))
            throw new ArgumentException("Invalid segment ID.", nameof(id));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 40)
            throw new ArgumentException("Invalid segment name.", nameof(name));
        if (startMs < 0 || endMs <= startMs)
            throw new ArgumentException("Invalid segment range.");
        this.id = id;
        this.name = name;
        this.startMs = startMs;
        this.endMs = endMs;
    }
}
