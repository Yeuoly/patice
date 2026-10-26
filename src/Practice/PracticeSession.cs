using System;
using Patice.Domain;

namespace Patice.Practice;

public enum PracticeState { Closed, Selecting, Seeking, Selected, Countdown, Playing, ExitPrompt }

public sealed class PracticeSession
{
    private readonly IPlayback player;
    private readonly Segment[] segments;
    private readonly float[] rates = { 1f, .9f, .8f, .7f, .6f, .5f, 2f, 1.5f };
    private int rateIndex;
    private double elapsed;
    private bool playingSegment;
    public PracticeState State { get; private set; }
    public int Index { get; private set; }
    public double SeekPositionMs { get; private set; }
    public int Count => segments.Length;
    public Segment Current => segments[Index];
    private double PracticeStartMs => Math.Max(1, Current.startMs - 2000d);
    private double PracticeEndMs => Math.Min(player.DurationMs - 1, Current.endMs + 2000d);
    public bool Loop { get; private set; } = true;
    public bool Repeating => playingSegment && Loop;
    public bool PreventGameEnd => State != PracticeState.Closed &&
        (State != PracticeState.Playing || Repeating);
    public ExitBehavior ExitBehavior { get; private set; }
    public float Rate => rates[rateIndex];
    public int Countdown => Math.Max(1, 3 - (int)elapsed);
    public float SelectionProgress => (float)Math.Min(1, elapsed / .4);

    public PracticeSession(IPlayback player, Segment[] segments)
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
        player.Seek(PracticeStartMs);
        player.Pause(false);
    }

    public void Entry()
    {
        switch (State)
        {
            case PracticeState.Selecting: End(ExitBehavior.ReturnToEntryPosition); break;
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

    public bool Handle(PracticeAction action)
    {
        if (action == PracticeAction.Cancel && State != PracticeState.Closed && State != PracticeState.Playing)
        {
            Entry();
            return true;
        }
        if (State == PracticeState.ExitPrompt)
        {
            if (action != PracticeAction.Confirm) return false;
            End(ExitBehavior.ContinueFromCurrentPosition);
            return true;
        }
        if (action == PracticeAction.ChangeRate && (State == PracticeState.Selecting || State == PracticeState.Seeking))
        {
            rateIndex = (rateIndex + 1) % rates.Length;
            player.SetRate(Rate);
            return true;
        }
        if (State == PracticeState.Seeking)
        {
            if (action == PracticeAction.Confirm)
            {
                player.Preview(false);
                player.Seek(SeekPositionMs);
                player.Pause(false);
                playingSegment = false;
                State = PracticeState.Playing;
                return true;
            }
            if (action != PracticeAction.Next && action != PracticeAction.Previous) return false;
            SeekPositionMs = Math.Max(0, Math.Min(player.DurationMs,
                SeekPositionMs + (action == PracticeAction.Next ? 5000 : -5000)));
            player.Seek(SeekPositionMs);
            return true;
        }
        if (State != PracticeState.Selecting) return false;
        switch (action)
        {
            case PracticeAction.Previous: Index = (Index + Count - 1) % Count; StartPreview(); break;
            case PracticeAction.Next: Index = (Index + 1) % Count; StartPreview(); break;
            case PracticeAction.Seek:
                player.Pause(true);
                SeekPositionMs = player.EntryPositionMs;
                player.Seek(SeekPositionMs);
                State = PracticeState.Seeking;
                break;
            case PracticeAction.ToggleLoop: Loop = !Loop; break;
            case PracticeAction.Confirm:
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
        if (State == PracticeState.Selecting && player.PositionMs >= PracticeEndMs)
            player.Seek(PracticeStartMs);
        else if (State == PracticeState.Selected)
        {
            elapsed += seconds;
            if (elapsed < .4) return;
            player.Preview(false);
            player.Seek(PracticeStartMs);
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
        else if (State == PracticeState.Playing && playingSegment && player.PositionMs >= PracticeEndMs)
        {
            if (Loop) player.Seek(PracticeStartMs);
            else End(ExitBehavior.ContinueFromCurrentPosition);
        }
    }

    internal void End(ExitBehavior behavior)
    {
        State = PracticeState.Closed;
        ExitBehavior = behavior;
    }
}
