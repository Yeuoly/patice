using System;
using Patice.Domain;

namespace Patice.Practice;

public enum PracticeState { Closed, Selecting, Seeking, Selected, Countdown, Playing, ExitPrompt }

public sealed class PracticeSession
{
    private readonly IPlayback player;
    private readonly SegmentSelection selection;
    private readonly float[] rates = { 1f, .9f, .8f, .7f, .6f, .5f, 2f, 1.5f };
    private int rateIndex;
    private double elapsed, messageElapsed;
    private SegmentPreview preview;
    private PlaybackRequest playback;
    private PracticeState seekOrigin, playbackOrigin;
    private double? manualStartMs, manualEndMs;
    private bool manualLoop;
    private string message;
    public PracticeState State { get; private set; }
    public double SeekPositionMs { get; private set; }
    public bool Repeating => playback?.Loop == true;
    public bool PreventGameEnd => State != PracticeState.Closed &&
        (State != PracticeState.Playing || Repeating);
    // Retained across page changes, including Playing -> Seeking -> Countdown.
    public ExitBehavior ExitBehavior { get; private set; }
    public float Rate => rates[rateIndex];
    public int Countdown => Math.Max(1, 3 - (int)elapsed);
    private bool HasManualRange => manualStartMs.HasValue && manualEndMs.HasValue && manualEndMs > manualStartMs;

    public PracticeSession(IPlayback player, Segment[] segments)
    {
        this.player = player;
        selection = new SegmentSelection(segments);
    }

    public PracticeViewState GetViewState() => new(State, Rate, Countdown, (float)Math.Min(1, elapsed / .4),
        State == PracticeState.Selecting || State == PracticeState.Selected ? preview : null,
        State == PracticeState.Seeking ? new SeekViewState(SeekPositionMs, manualStartMs, manualEndMs, manualLoop) : null,
        message);

    public void Open()
    {
        if (selection.HasSegments) StartPreview(selection.GetPreview(player.DurationMs));
        else StartSeek(player.EntryPositionMs, PracticeState.Closed);
    }

    private void StartPreview(SegmentPreview selected)
    {
        preview = selected;
        State = PracticeState.Selecting;
        message = null;
        player.Pause(true);
        player.Preview(true);
        player.SetRate(Rate);
        player.Seek(preview.PlaybackRange.StartMs);
        player.Pause(false);
    }

    private void StartSeek(double positionMs, PracticeState origin)
    {
        seekOrigin = origin;
        State = PracticeState.Seeking;
        message = null;
        player.Pause(true);
        player.Preview(true);
        player.SetRate(Rate);
        SeekPositionMs = Math.Max(0, Math.Min(player.DurationMs, positionMs));
        player.Seek(SeekPositionMs);
    }

    // Both entry points use the same native playback preparation and countdown.
    private void BeginPlayback(PlaybackRequest request)
    {
        playback = request;
        player.Preview(false);
        player.Seek(request.StartMs);
        State = PracticeState.Countdown;
        elapsed = 0;
    }

    public void Entry()
    {
        switch (State)
        {
            case PracticeState.Selecting: End(ExitBehavior); break;
            case PracticeState.Seeking:
                if (seekOrigin == PracticeState.Selecting) StartPreview(selection.GetPreview(player.DurationMs));
                else End(ExitBehavior);
                break;
            case PracticeState.Selected:
            case PracticeState.Countdown:
                if (playbackOrigin == PracticeState.Selecting) StartPreview(selection.GetPreview(player.DurationMs));
                else StartSeek(SeekPositionMs, seekOrigin);
                break;
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
        message = null;
        if (action == PracticeAction.Cancel && State != PracticeState.Closed && State != PracticeState.Playing)
        {
            Entry();
            return true;
        }
        if (State == PracticeState.ExitPrompt)
        {
            if (action == PracticeAction.Seek)
            {
                StartSeek(player.PositionMs, PracticeState.Playing);
                return true;
            }
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
        if (State == PracticeState.Seeking) return HandleSeek(action);
        if (State != PracticeState.Selecting) return false;
        switch (action)
        {
            case PracticeAction.Previous:
            case PracticeAction.Next:
                selection.Move(action == PracticeAction.Next ? 1 : -1);
                StartPreview(selection.GetPreview(player.DurationMs));
                break;
            case PracticeAction.Seek: StartSeek(player.EntryPositionMs, PracticeState.Selecting); break;
            case PracticeAction.ToggleLoop:
                selection.Loop = !selection.Loop;
                preview = selection.GetPreview(player.DurationMs);
                break;
            case PracticeAction.Confirm:
                player.Pause(true);
                playbackOrigin = PracticeState.Selecting;
                playback = new PlaybackRequest(preview.PlaybackRange.StartMs, preview.PlaybackRange, preview.Loop);
                State = PracticeState.Selected;
                elapsed = 0;
                break;
            default: return false;
        }
        return true;
    }

    private bool HandleSeek(PracticeAction action)
    {
        switch (action)
        {
            case PracticeAction.Confirm:
                playbackOrigin = PracticeState.Seeking;
                var range = manualLoop && HasManualRange
                    ? new PlaybackRange(manualStartMs.Value, manualEndMs.Value) : (PlaybackRange?)null;
                BeginPlayback(new PlaybackRequest(range?.StartMs ?? SeekPositionMs, range, manualLoop));
                return true;
            case PracticeAction.Next:
            case PracticeAction.Previous:
                SeekPositionMs = Math.Max(0, Math.Min(player.DurationMs,
                    SeekPositionMs + (action == PracticeAction.Next ? 1000 : -1000)));
                player.Seek(SeekPositionMs);
                return true;
            case PracticeAction.SetLoopStart:
            case PracticeAction.SetLoopEnd:
                return SetEndpoint(action == PracticeAction.SetLoopStart);
            case PracticeAction.ToggleLoop:
                if (!HasManualRange) return false;
                manualLoop = !manualLoop;
                return true;
            default: return false;
        }
    }

    private bool SetEndpoint(bool start)
    {
        if (start && manualStartMs.HasValue) { manualStartMs = null; manualLoop = false; return true; }
        if (!start && manualEndMs.HasValue) { manualEndMs = null; manualLoop = false; return true; }
        var position = Math.Max(1, Math.Min(player.DurationMs - 1, SeekPositionMs));
        if ((start && manualEndMs.HasValue && position >= manualEndMs.Value) ||
            (!start && manualStartMs.HasValue && position <= manualStartMs.Value))
        {
            message = "End must be after start";
            messageElapsed = 0;
            return false;
        }
        if (start) manualStartMs = position;
        else manualEndMs = position;
        manualLoop = HasManualRange;
        return true;
    }

    public void Tick(double seconds)
    {
        if (message != null && (messageElapsed += seconds) >= 1.8) message = null;
        if (State == PracticeState.Selecting && player.PositionMs >= preview.PlaybackRange.EndMs)
            player.Seek(preview.PlaybackRange.StartMs);
        else if (State == PracticeState.Selected)
        {
            elapsed += seconds;
            if (elapsed < .4) return;
            BeginPlayback(playback);
        }
        else if (State == PracticeState.Countdown)
        {
            elapsed += seconds;
            if (elapsed < 3) return;
            ExitBehavior = ExitBehavior.ContinueFromCurrentPosition;
            player.Pause(false);
            State = PracticeState.Playing;
        }
        else if (State == PracticeState.Playing && playback.Range is PlaybackRange range && player.PositionMs >= range.EndMs)
        {
            if (playback.Loop) player.Seek(range.StartMs);
            else End(ExitBehavior.ContinueFromCurrentPosition);
        }
    }

    internal void End(ExitBehavior behavior)
    {
        State = PracticeState.Closed;
        ExitBehavior = behavior;
    }
}
