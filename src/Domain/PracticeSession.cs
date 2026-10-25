using System;

namespace AquaMai.Mods.UX.PracticeMode.Domain;

public enum PracticeState { Closed, Selecting, Selected, Countdown, Playing, ExitPrompt }

// The native adapter owns the snapshot (autoplay, pause, rate, position and audio).
public interface IPracticePlayer
{
    double PositionMs { get; }
    void Capture();
    void Preview(bool enabled);
    void Pause(bool paused);
    void Seek(double milliseconds);
    void SetRate(float rate);
    void Restore(bool rewind);
}

public sealed class PracticeSession : IDisposable
{
    private readonly IPracticePlayer player;
    private readonly Segment[] segments;
    private readonly float[] rates = { 1f, .5f, .75f, .9f };
    private int rateIndex;
    private double elapsed;
    private bool captured;
    public PracticeState State { get; private set; }
    public int Index { get; private set; }
    public int Count => segments.Length;
    public Segment Current => segments[Index];
    public bool Loop { get; private set; } = true;
    public bool ConfirmExit { get; private set; }
    public float Rate => rates[rateIndex];
    public int Countdown => Math.Max(1, 3 - (int)elapsed);
    public float SelectionProgress => (float)Math.Min(1, elapsed / .4);

    public PracticeSession(IPracticePlayer player, Segment[] segments)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        if (segments == null || segments.Length == 0) throw new ArgumentException(nameof(segments));
        this.segments = segments;
    }

    public void Open()
    {
        if (State != PracticeState.Closed) return;
        player.Capture();
        captured = true;
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
            case PracticeState.Selected:
            case PracticeState.Countdown: StartPreview(); break;
            case PracticeState.Playing:
                player.Pause(true);
                ConfirmExit = false;
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
        if (State == PracticeState.ExitPrompt)
        {
            if (number == 3 || number == 6) { ConfirmExit = !ConfirmExit; return true; }
            if (number != 4) return false;
            if (ConfirmExit) Close(false);
            else { State = PracticeState.Playing; player.Pause(false); }
            return true;
        }
        if (State != PracticeState.Selecting) return false;
        switch (number)
        {
            case 6: Index = (Index + Count - 1) % Count; StartPreview(); break;
            case 3: Index = (Index + 1) % Count; StartPreview(); break;
            case 7: rateIndex = (rateIndex + 1) % rates.Length; player.SetRate(Rate); break;
            case 8: Loop = !Loop; break;
            case 4:
                player.Pause(true);
                State = PracticeState.Selected;
                elapsed = 0;
                break;
            default: return false;
        }
        return true;
    }

    public void Tick(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
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
        else if (State == PracticeState.Playing && player.PositionMs >= Current.endMs)
        {
            if (Loop) player.Seek(Current.startMs);
            else Close(false);
        }
    }

    private void Close(bool rewind)
    {
        State = PracticeState.Closed;
        if (!captured) return;
        captured = false;
        player.Restore(rewind);
    }

    // Unexpected destruction must never leave autoplay or speed behind.
    public void Dispose() => Close(State != PracticeState.Playing && State != PracticeState.ExitPrompt);
}
