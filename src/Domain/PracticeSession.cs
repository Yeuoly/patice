using System;

namespace Patice.Domain;

public enum PracticeState { Closed, Selecting, Seeking, Selected, Countdown, Playing, ExitPrompt }

public sealed class PracticeSession : IDisposable
{
    private readonly Playback player;
    private readonly Segment[] segments;
    private readonly float[] rates = { 1f, .5f, .75f, .9f };
    private int rateIndex;
    private double elapsed;
    private bool playingSegment;
    public PracticeState State { get; private set; }
    public int Index { get; private set; }
    public double SeekPositionMs { get; private set; }
    public int Count => segments.Length;
    public Segment Current => segments[Index];
    public bool Loop { get; private set; } = true;
    public bool Repeating => playingSegment && Loop;
    public float Rate => rates[rateIndex];
    public int Countdown => Math.Max(1, 3 - (int)elapsed);
    public float SelectionProgress => (float)Math.Min(1, elapsed / .4);

    public PracticeSession(Playback player, Segment[] segments)
    {
        this.player = player;
        this.segments = segments;
    }

    public void Open()
    {
        StartPreview();
    }

    private void StartPreview()
    {
        State = PracticeState.Selecting;
        player.Pause(true);
        player.Preview(true);
        player.SetRate(Rate);
        player.Seek(Current.startMs);
        player.Pause(false);
    }

    public void Entry()
    {
        switch (State)
        {
            case PracticeState.Selecting: Close(true); break;
            case PracticeState.Seeking: StartPreview(); break;
            case PracticeState.Selected:
            case PracticeState.Countdown: StartPreview(); break;
            case PracticeState.Playing:
                player.Pause(true);
                State = PracticeState.ExitPrompt;
                break;
            case PracticeState.ExitPrompt:
                player.Pause(false);
                State = PracticeState.Playing;
                break;
        }
    }

    public bool Button(int number)
    {
        if (number == 5 && State != PracticeState.Closed && State != PracticeState.Playing)
        {
            Entry();
            return true;
        }
        if (State == PracticeState.ExitPrompt)
        {
            if (number != 6) return false;
            Close(false);
            return true;
        }
        if (number == 1 && (State == PracticeState.Selecting || State == PracticeState.Seeking))
        {
            rateIndex = (rateIndex + 1) % rates.Length;
            player.SetRate(Rate);
            return true;
        }
        if (State == PracticeState.Seeking)
        {
            if (number == 4)
            {
                player.Preview(false);
                player.Seek(SeekPositionMs);
                player.Pause(false);
                playingSegment = false;
                State = PracticeState.Playing;
                return true;
            }
            if (number != 3 && number != 6) return false;
            SeekPositionMs = Math.Max(0, Math.Min(player.DurationMs,
                SeekPositionMs + (number == 3 ? 5000 : -5000)));
            player.Seek(SeekPositionMs);
            return true;
        }
        if (State != PracticeState.Selecting) return false;
        switch (number)
        {
            case 6: Index = (Index + Count - 1) % Count; StartPreview(); break;
            case 3: Index = (Index + 1) % Count; StartPreview(); break;
            case 2:
                player.Pause(true);
                SeekPositionMs = player.EntryPositionMs;
                player.Seek(SeekPositionMs);
                State = PracticeState.Seeking;
                break;
            case 8: Loop = !Loop; break;
            case 4:
                player.Pause(true);
                playingSegment = true;
                State = PracticeState.Selected;
                elapsed = 0;
                break;
            default: return false;
        }
        return true;
    }

    public void Tick(double seconds)
    {
        if (State == PracticeState.Selecting && player.PositionMs >= Current.endMs)
            player.Seek(Current.startMs);
        else if (State == PracticeState.Selected)
        {
            elapsed += seconds;
            if (elapsed < .4) return;
            player.Preview(false);
            player.Seek(Current.startMs);
            State = PracticeState.Countdown;
            elapsed = 0;
        }
        else if (State == PracticeState.Countdown)
        {
            elapsed += seconds;
            if (elapsed < 3) return;
            player.Pause(false);
            State = PracticeState.Playing;
        }
        else if (State == PracticeState.Playing && playingSegment && player.PositionMs >= Current.endMs)
        {
            if (Loop) player.Seek(Current.startMs);
            else Close(false);
        }
    }

    private void Close(bool rewind)
    {
        State = PracticeState.Closed;
        player.Restore(rewind);
    }

    // Unexpected destruction must never leave autoplay or speed behind.
    public void Dispose() => Close(State != PracticeState.Playing && State != PracticeState.ExitPrompt);
}
