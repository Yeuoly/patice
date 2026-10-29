using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Patice.Domain;
using Patice.Practice;
using Xunit;

public sealed class PracticeFlowIntegration
{
    [Fact]
    public void PreviewLoopsThenCancelRestoresTheEntryPositionBeforeRemovingTheView()
    {
        using var flow = new Flow();
        Assert.Equal(new[]
        {
            "capture", "view.create", "pause:True", "preview:True", "rate:1",
            "seek:58000", "pause:False", "view:Selecting"
        }, flow.Events);
        Assert.True(flow.Controller.PreventGameEnd);
        flow.Controller.RenderPreview();
        Assert.Equal("render:Selecting", flow.Events.Last());

        flow.Player.PositionMs = 76000;
        flow.Controller.Tick(0);
        Assert.Equal(76000, flow.Player.PositionMs);
        flow.Player.PositionMs = 78000;
        flow.Controller.Tick(0);
        Assert.Equal(58000, flow.Player.PositionMs);
        flow.Events.Clear();
        flow.Press(PracticeAction.Cancel, 5);
        Assert.Equal(new[] { "restore:ReturnToEntryPosition", "press:5", "view.dispose" }, flow.Events);
        Assert.Equal(30000, flow.Player.PositionMs);
        Assert.False(flow.Controller.PreventGameEnd);
        flow.Controller.Dispose();
        Assert.Equal(1, flow.Player.Restores);
        Assert.Equal(1, flow.View.Disposals);
    }

    [Fact]
    public void SelectionCountdownLoopAndConfirmedExitKeepTheirPlaybackOrder()
    {
        using var flow = new Flow();
        flow.Press(PracticeAction.Previous, 6);
        Assert.Equal("Final", flow.View.Latest.Segment.Name);
        flow.Press(PracticeAction.Next, 3);
        Assert.Equal("S1", flow.View.Latest.Segment.Name);
        flow.Press(PracticeAction.Next, 3);
        Assert.Equal("S2", flow.View.Latest.Segment.Name);

        flow.Events.Clear();
        flow.Press(PracticeAction.Confirm, 4);
        flow.Controller.Tick(.2);
        Assert.Equal(PracticeState.Selected, flow.Controller.Session.State);
        flow.Controller.Tick(.2);
        Assert.Equal(PracticeState.Countdown, flow.Controller.Session.State);
        flow.Controller.Tick(1);
        Assert.Equal(2, flow.Controller.Session.Countdown);
        flow.Controller.Tick(1);
        Assert.Equal(1, flow.Controller.Session.Countdown);
        flow.Controller.Tick(1);
        Assert.Equal(PracticeState.Playing, flow.Controller.Session.State);
        Assert.Equal(new[] { "pause:True", "preview:False", "seek:102000", "pause:False" }, flow.PlaybackEvents);
        Assert.True(flow.Controller.PreventGameEnd);

        flow.Player.PositionMs = 115000;
        flow.Controller.Tick(0);
        Assert.Equal(115000, flow.Player.PositionMs);
        flow.Player.PositionMs = 117000;
        flow.Controller.Tick(0);
        Assert.Equal(102000, flow.Player.PositionMs);
        flow.Controller.Entry();
        Assert.Equal(PracticeState.ExitPrompt, flow.Controller.Session.State);
        flow.Press(PracticeAction.Cancel, 5);
        Assert.Equal(PracticeState.Playing, flow.Controller.Session.State);
        Assert.False(flow.Player.Paused);
        flow.Player.PositionMs = 110000;
        flow.Controller.Entry();
        flow.Events.Clear();
        flow.Press(PracticeAction.Confirm, 4);
        Assert.Equal(new[] { "restore:ContinueFromCurrentPosition", "press:4", "view.dispose" }, flow.Events);
        Assert.Equal(110000, flow.Player.PositionMs);
        Assert.False(flow.Controller.PreventGameEnd);
    }

    [Fact]
    public void OnceContinuesPastTheSegmentAndReleasesTheNativeEndCheck()
    {
        using var flow = new Flow();
        flow.Press(PracticeAction.ToggleLoop, 8);
        flow.StartPlaying();
        Assert.False(flow.Controller.PreventGameEnd);
        flow.Player.PositionMs = 76001;
        flow.Controller.Tick(0);
        Assert.Equal(PracticeState.Playing, flow.Controller.Session.State);
        flow.Player.PositionMs = 78001;
        flow.Events.Clear();
        flow.Controller.Tick(0);
        Assert.Equal(new[] { "restore:ContinueFromCurrentPosition", "view.dispose" }, flow.Events);
        Assert.Equal(78001, flow.Player.PositionMs);
        Assert.Equal(1, flow.Player.Rate);
        Assert.False(flow.Player.Paused);
    }

    [Fact]
    public void SeekKeepsSpeedControlAndResumesFromTheChosenTimeWithoutLooping()
    {
        using var flow = new Flow();
        foreach (var expected in new[] { .9f, .8f, .7f, .6f, .5f, 2f, 1.5f, 1f })
        {
            flow.Press(PracticeAction.ChangeRate, 1);
            Assert.Equal(expected, flow.Player.Rate);
        }
        flow.Press(PracticeAction.Seek, 2);
        Assert.Equal(30000, flow.Player.PositionMs);
        Assert.True(flow.Player.Paused);
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.Previous, 6);
        Assert.Equal(31000, flow.Controller.Session.SeekPositionMs);
        flow.Press(PracticeAction.ChangeRate, 1);
        Assert.Equal(.9f, flow.Player.Rate);
        flow.Events.Clear();
        flow.Press(PracticeAction.Confirm, 4);
        Assert.Equal(PracticeState.Countdown, flow.Controller.Session.State);
        flow.Controller.Tick(3);
        Assert.Equal(new[] { "preview:False", "seek:31000", "pause:False" }, flow.PlaybackEvents);
        Assert.False(flow.Controller.PreventGameEnd);
        Assert.False(flow.Controller.Session.Repeating);
        flow.Player.PositionMs = 80000;
        flow.Controller.Tick(0);
        Assert.Equal(80000, flow.Player.PositionMs);
        Assert.Equal(PracticeState.Playing, flow.Controller.Session.State);
        flow.Controller.Entry();
        flow.Press(PracticeAction.Confirm, 4);
        Assert.Equal(80000, flow.Player.PositionMs);
        Assert.Equal(1, flow.Player.Restores);
    }

    [Fact]
    public void SeekClampsAtBothEndsAndCancelReturnsToTheSelectedPreview()
    {
        using var flow = new Flow();
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.Seek, 2);
        for (var i = 0; i < 35; i++) flow.Press(PracticeAction.Previous, 6);
        Assert.Equal(0, flow.Player.PositionMs);
        for (var i = 0; i < 140; i++) flow.Press(PracticeAction.Next, 3);
        Assert.Equal(flow.Player.DurationMs, flow.Player.PositionMs);
        flow.Press(PracticeAction.Cancel, 5);
        Assert.Equal(PracticeState.Selecting, flow.Controller.Session.State);
        Assert.Equal(102000, flow.Player.PositionMs);
        Assert.True(flow.Player.Previewing);
        Assert.False(flow.Player.Paused);
        Assert.Equal(0, flow.Player.Restores);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelDuringSelectionAnimationOrCountdownReturnsToPreview(bool countdown)
    {
        using var flow = new Flow();
        flow.Press(PracticeAction.Confirm, 4);
        if (countdown) flow.Controller.Tick(.4);
        flow.Press(PracticeAction.Cancel, 5);
        Assert.Equal(PracticeState.Selecting, flow.Controller.Session.State);
        Assert.True(flow.Player.Previewing);
        Assert.False(flow.Player.Paused);
        Assert.Equal(58000, flow.Player.PositionMs);
        Assert.Equal(0, flow.Player.Restores);
    }

    [Theory]
    [InlineData(PracticeState.Selecting, ExitBehavior.ReturnToEntryPosition)]
    [InlineData(PracticeState.Seeking, ExitBehavior.ReturnToEntryPosition)]
    [InlineData(PracticeState.Selected, ExitBehavior.ReturnToEntryPosition)]
    [InlineData(PracticeState.Countdown, ExitBehavior.ReturnToEntryPosition)]
    [InlineData(PracticeState.Playing, ExitBehavior.ContinueFromCurrentPosition)]
    [InlineData(PracticeState.ExitPrompt, ExitBehavior.ContinueFromCurrentPosition)]
    public void GameReleaseRestoresOnceFromEveryActivePage(PracticeState state, ExitBehavior exit)
    {
        using var flow = new Flow();
        if (state == PracticeState.Seeking) flow.Press(PracticeAction.Seek, 2);
        if (state is PracticeState.Selected or PracticeState.Countdown or PracticeState.Playing or PracticeState.ExitPrompt)
            flow.Press(PracticeAction.Confirm, 4);
        if (state is PracticeState.Countdown or PracticeState.Playing or PracticeState.ExitPrompt)
            flow.Controller.Tick(.4);
        if (state is PracticeState.Playing or PracticeState.ExitPrompt) flow.Controller.Tick(3);
        if (state == PracticeState.ExitPrompt) flow.Controller.Entry();
        Assert.Equal(state, flow.Controller.Session.State);
        flow.Events.Clear();
        flow.Controller.Dispose();
        flow.Controller.Dispose();
        Assert.Equal(new[] { "restore:" + exit, "view.dispose" }, flow.Events);
        Assert.False(flow.Controller.PreventGameEnd);
    }

    [Fact]
    public void FailedViewCreationRestoresTheCapturedGame()
    {
        using var flow = new Flow(open: false);
        Assert.Throws<InvalidOperationException>(() => flow.Controller.Open(() => throw new InvalidOperationException()));
        flow.Controller.Dispose();
        Assert.Equal(new[] { "capture", "restore:ReturnToEntryPosition" }, flow.Events);
        Assert.Equal(30000, flow.Player.PositionMs);
        Assert.False(flow.Player.Paused);
    }

    [Theory]
    [InlineData(60000, 76000, 58000, 78000)]
    [InlineData(0, 133559, 1, 133558)]
    [InlineData(500, 133000, 1, 133558)]
    [InlineData(1500, 132000, 1, 133558)]
    public void PreviewAndPracticeUseTheSamePaddedSegmentStrictlyInsideTheTrack(
        int start, int end, int practiceStart, int practiceEnd)
    {
        var segment = new Segment(417, Difficulty.Master, Guid.NewGuid(), "S1", start, end);
        using var flow = new Flow(segments: new[] { segment });
        var player = flow.Player;
        Assert.Equal(practiceStart, player.PositionMs);
        player.PositionMs = practiceEnd - 1;
        flow.Controller.Tick(0);
        Assert.Equal(practiceEnd - 1, player.PositionMs);
        player.PositionMs = practiceEnd;
        flow.Controller.Tick(0);
        Assert.Equal(practiceStart, player.PositionMs);

        flow.Press(PracticeAction.Confirm, 4);
        flow.Controller.Tick(.4);
        Assert.Equal(practiceStart, player.PositionMs);
        flow.Controller.Tick(3);
        player.PositionMs = practiceEnd - 1;
        flow.Controller.Tick(0);
        Assert.Equal(practiceEnd - 1, player.PositionMs);
        player.PositionMs = practiceEnd;
        flow.Controller.Tick(0);
        Assert.Equal(practiceStart, player.PositionMs);
        Assert.True(player.PositionMs > 0);
        Assert.True(practiceEnd < player.DurationMs);

        flow.Controller.Entry();
        flow.Press(PracticeAction.Confirm, 4);
        using var reopened = new Flow(segments: new[] { segment });
        Assert.Equal(practiceStart, reopened.Player.PositionMs);
        Assert.Equal(start, segment.startMs);
        Assert.Equal(end, segment.endMs);
    }

    [Fact]
    public void EmptyCatalogOpensSeekAndCancelRestoresOnceWithoutAnySegmentPreview()
    {
        using var flow = new Flow(segments: Array.Empty<Segment>());
        Assert.Equal(PracticeState.Seeking, flow.View.Latest.State);
        Assert.Null(flow.View.Latest.Segment);
        Assert.Equal(30000, flow.View.Latest.Seek.PositionMs);
        Assert.False(flow.View.Latest.Seek.CanLoop);
        Assert.True(flow.Player.Paused);
        Assert.True(flow.Player.Previewing);
        Assert.True(flow.Controller.PreventGameEnd);
        flow.Press(PracticeAction.ToggleLoop, 8);
        Assert.DoesNotContain("press:8", flow.Events);
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.ChangeRate, 1);
        flow.Events.Clear();
        flow.Press(PracticeAction.Cancel, 5);
        flow.Controller.Dispose();
        Assert.Equal(new[] { "restore:ReturnToEntryPosition", "press:5", "view.dispose" }, flow.Events);
        Assert.Equal(30000, flow.Player.PositionMs);
        Assert.Equal(1, flow.Player.Rate);
    }

    [Fact]
    public void ManualLoopUsesExactEndpointsAndExistingCountdownPlaybackAndExit()
    {
        using var flow = new Flow(segments: Array.Empty<Segment>());
        flow.Press(PracticeAction.SetLoopStart, 2);
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.SetLoopEnd, 7);
        flow.Press(PracticeAction.ChangeRate, 1);
        Assert.True(flow.View.Latest.Seek.Loop);
        flow.Events.Clear();
        flow.Press(PracticeAction.Confirm, 4);
        Assert.Equal(PracticeState.Countdown, flow.View.Latest.State);
        Assert.Null(flow.View.Latest.Segment);
        Assert.True(flow.Player.Paused);
        flow.Controller.Tick(2.9);
        Assert.True(flow.Player.Paused);
        flow.Controller.Tick(.1);
        Assert.Equal(new[] { "preview:False", "seek:30000", "pause:False" }, flow.PlaybackEvents);
        Assert.True(flow.Controller.PreventGameEnd);
        Assert.Equal(.9f, flow.Player.Rate);
        for (var repeat = 0; repeat < 3; repeat++)
        {
            flow.Player.PositionMs = 31999;
            flow.Controller.Tick(0);
            Assert.Equal(31999, flow.Player.PositionMs);
            flow.Player.PositionMs = 32000;
            flow.Controller.Tick(0);
            Assert.Equal(30000, flow.Player.PositionMs);
        }
        flow.Controller.Entry();
        flow.Press(PracticeAction.Cancel, 5);
        Assert.Equal(PracticeState.Playing, flow.View.Latest.State);
        flow.Player.PositionMs = 31500;
        flow.Controller.Entry();
        flow.Events.Clear();
        flow.Press(PracticeAction.Confirm, 4);
        Assert.Equal(new[] { "restore:ContinueFromCurrentPosition", "press:4", "view.dispose" }, flow.Events);
        Assert.Equal(31500, flow.Player.PositionMs);
        Assert.False(flow.Controller.PreventGameEnd);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ClearingEitherEndpointDisablesLoopAndPreservesTheOther(bool clearStart)
    {
        using var flow = new Flow(segments: Array.Empty<Segment>());
        // End may be set first and cleared even before a start exists.
        flow.Press(PracticeAction.SetLoopEnd, 7);
        flow.Press(PracticeAction.SetLoopEnd, 7);
        Assert.Null(flow.View.Latest.Seek.EndMs);
        flow.Press(PracticeAction.SetLoopStart, 2);
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.SetLoopEnd, 7);
        flow.Events.Clear();
        flow.Press(clearStart ? PracticeAction.SetLoopStart : PracticeAction.SetLoopEnd, clearStart ? 2 : 7);
        Assert.Empty(flow.PlaybackEvents);
        Assert.False(flow.View.Latest.Seek.CanLoop);
        Assert.False(flow.View.Latest.Seek.Loop);
        Assert.Equal(clearStart ? null : (double?)30000, flow.View.Latest.Seek.StartMs);
        Assert.Equal(clearStart ? (double?)31000 : null, flow.View.Latest.Seek.EndMs);
        flow.Press(PracticeAction.ToggleLoop, 8);
        Assert.False(flow.View.Latest.Seek.Loop);
        if (clearStart) flow.Press(PracticeAction.Previous, 6);
        flow.Press(clearStart ? PracticeAction.SetLoopStart : PracticeAction.SetLoopEnd, clearStart ? 2 : 7);
        Assert.True(flow.View.Latest.Seek.Loop);
    }

    [Fact]
    public void InvalidEndpointIsRejectedAndMessageExpiresWithoutChangingPlayback()
    {
        using var flow = new Flow(segments: Array.Empty<Segment>());
        flow.Press(PracticeAction.SetLoopEnd, 7);
        flow.Events.Clear();
        flow.Press(PracticeAction.SetLoopStart, 2);
        Assert.Null(flow.View.Latest.Seek.StartMs);
        Assert.Equal("End must be after start", flow.View.Latest.Message);
        Assert.Empty(flow.PlaybackEvents);
        Assert.DoesNotContain("press:2", flow.Events);
        flow.Controller.Tick(1.8);
        Assert.Null(flow.View.Latest.Message);
        flow.Press(PracticeAction.Previous, 6);
        flow.Press(PracticeAction.SetLoopStart, 2);
        Assert.True(flow.View.Latest.Seek.CanLoop);
    }

    [Fact]
    public void ManualOnceKeepsMarkersAndPlaysFromCursorWithoutStoppingAtTheRangeEnd()
    {
        using var flow = new Flow(segments: Array.Empty<Segment>());
        flow.Press(PracticeAction.SetLoopStart, 2);
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.SetLoopEnd, 7);
        flow.Press(PracticeAction.ToggleLoop, 8);
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.ChangeRate, 1);
        flow.Press(PracticeAction.Confirm, 4);
        flow.Controller.Tick(3);
        Assert.Equal(32000, flow.Player.PositionMs);
        Assert.False(flow.Controller.PreventGameEnd);
        flow.Player.PositionMs = 80000;
        flow.Controller.Tick(0);
        Assert.Equal(80000, flow.Player.PositionMs);
        Assert.Equal(.9f, flow.Player.Rate);
        flow.Controller.Entry();
        flow.Press(PracticeAction.Seek, 2);
        Assert.Equal(80000, flow.View.Latest.Seek.PositionMs);
        Assert.Equal(30000, flow.View.Latest.Seek.StartMs);
        Assert.Equal(31000, flow.View.Latest.Seek.EndMs);
        Assert.False(flow.View.Latest.Seek.Loop);
        flow.Press(PracticeAction.Cancel, 5);
        Assert.Equal(80000, flow.Player.PositionMs);
        Assert.Equal(1, flow.Player.Restores);
    }

    [Fact]
    public void SegmentSelectionAndSeekHaveIndependentRangesAndLoopSettings()
    {
        using var flow = new Flow();
        flow.Press(PracticeAction.Next, 3);
        Assert.Equal("S2", flow.View.Latest.Segment.Name);
        flow.Press(PracticeAction.Seek, 2);
        Assert.Null(flow.View.Latest.Segment);
        Assert.Null(flow.View.Latest.Seek.StartMs);
        flow.Press(PracticeAction.SetLoopStart, 2);
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.SetLoopEnd, 7);
        flow.Press(PracticeAction.ToggleLoop, 8);
        flow.Press(PracticeAction.Cancel, 5);
        Assert.Null(flow.View.Latest.Seek);
        Assert.Equal("S2", flow.View.Latest.Segment.Name);
        Assert.True(flow.View.Latest.Segment.Loop);
        Assert.Equal(104000, flow.View.Latest.Segment.StartMs);
        Assert.Equal(102000, flow.Player.PositionMs);
        flow.Press(PracticeAction.Seek, 2);
        Assert.Equal(30000, flow.View.Latest.Seek.StartMs);
        Assert.Equal(31000, flow.View.Latest.Seek.EndMs);
        Assert.False(flow.View.Latest.Seek.Loop);
        Assert.Equal(0, flow.Player.Restores);
        Assert.Single(flow.Events, e => e == "capture");
    }

    [Fact]
    public void ReturningFromALoopAndClearingItsEndReleasesTheOldPlaybackRange()
    {
        using var flow = new Flow(segments: Array.Empty<Segment>());
        flow.Press(PracticeAction.SetLoopStart, 2);
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.SetLoopEnd, 7);
        flow.Press(PracticeAction.Confirm, 4);
        flow.Controller.Tick(3);
        Assert.True(flow.Controller.Session.Repeating);
        flow.Controller.Entry();
        flow.Press(PracticeAction.Seek, 2);
        flow.Press(PracticeAction.SetLoopEnd, 7);
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.Confirm, 4);
        flow.Press(PracticeAction.Cancel, 5);
        Assert.Equal(PracticeState.Seeking, flow.View.Latest.State);
        Assert.Null(flow.View.Latest.Seek.EndMs);
        flow.Press(PracticeAction.Confirm, 4);
        flow.Controller.Tick(3);
        Assert.False(flow.Controller.Session.Repeating);
        Assert.False(flow.Controller.PreventGameEnd);
        flow.Player.PositionMs = flow.Player.DurationMs;
        flow.Controller.Tick(0);
        Assert.Equal(flow.Player.DurationMs, flow.Player.PositionMs);
        flow.Events.Clear();
        // The native game owns completion; its release closes the same controller.
        flow.Controller.Dispose();
        Assert.Equal(new[] { "restore:ContinueFromCurrentPosition", "view.dispose" }, flow.Events);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void GameReleaseWhileSeekingOrCountingDownUsesPlaybackHistory(bool alreadyPlayed, bool countdown)
    {
        using var flow = new Flow(segments: Array.Empty<Segment>());
        if (alreadyPlayed)
        {
            flow.Press(PracticeAction.Confirm, 4);
            flow.Controller.Tick(3);
            flow.Player.PositionMs = 80000;
            flow.Controller.Entry();
            flow.Press(PracticeAction.Seek, 2);
        }
        flow.Press(PracticeAction.Next, 3);
        if (countdown) flow.Press(PracticeAction.Confirm, 4);
        flow.Events.Clear();
        flow.Controller.Dispose();
        flow.Controller.Dispose();
        var behavior = alreadyPlayed ? ExitBehavior.ContinueFromCurrentPosition : ExitBehavior.ReturnToEntryPosition;
        Assert.Equal(new[] { "restore:" + behavior, "view.dispose" }, flow.Events);
        Assert.Equal(alreadyPlayed ? 81000 : 30000, flow.Player.PositionMs);
        Assert.False(flow.Player.Paused);
        Assert.Equal(1, flow.Player.Rate);
        Assert.False(flow.Controller.PreventGameEnd);
    }

    [Fact]
    public void CancelManualCountdownReturnsToSeekAndEndpointsStayInsideTheTrack()
    {
        using var flow = new Flow(segments: Array.Empty<Segment>());
        for (var i = 0; i < 35; i++) flow.Press(PracticeAction.Previous, 6);
        flow.Press(PracticeAction.SetLoopStart, 2);
        for (var i = 0; i < 140; i++) flow.Press(PracticeAction.Next, 3);
        flow.Press(PracticeAction.SetLoopEnd, 7);
        Assert.Equal(1, flow.View.Latest.Seek.StartMs);
        Assert.Equal(flow.Player.DurationMs - 1, flow.View.Latest.Seek.EndMs);
        flow.Press(PracticeAction.Confirm, 4);
        flow.Press(PracticeAction.Cancel, 5);
        Assert.Equal(PracticeState.Seeking, flow.View.Latest.State);
        Assert.Equal(flow.Player.DurationMs, flow.View.Latest.Seek.PositionMs);
        Assert.True(flow.View.Latest.Seek.Loop);
        Assert.True(flow.Player.Paused);
        Assert.True(flow.Player.Previewing);
        Assert.Equal(0, flow.Player.Restores);
    }

    private sealed class Flow : IDisposable
    {
        public readonly List<string> Events = new();
        public readonly Playback Player;
        public readonly View View;
        public readonly PracticeController Controller;
        public IEnumerable<string> PlaybackEvents => Events.Where(e => !e.StartsWith("view:") && !e.StartsWith("press:"));

        public Flow(bool open = true, Segment[] segments = null)
        {
            Player = new Playback(Events);
            View = new View(Events);
            Controller = new PracticeController(Player, segments ?? new[]
            {
                new Segment(417, Difficulty.Master, Guid.NewGuid(), "S1", 60000, 76000),
                new Segment(417, Difficulty.Master, Guid.NewGuid(), "S2", 104000, 115000),
                new Segment(417, Difficulty.Master, Guid.NewGuid(), "Final", 120000, 128000)
            });
            if (open) Controller.Open(() => { Events.Add("view.create"); return View; });
        }

        public void Press(PracticeAction action, int button)
        {
            Controller.Handle(action, button);
            Controller.Tick(0);
        }

        public void StartPlaying()
        {
            Press(PracticeAction.Confirm, 4);
            Controller.Tick(.4);
            Controller.Tick(3);
        }

        public void Dispose() => Controller.Dispose();
    }

    // Replace only the game and rendering boundaries; session/controller are production code.
    private sealed class Playback(List<string> events) : IPlayback
    {
        public double EntryPositionMs { get; private set; }
        public double DurationMs => 133559;
        public double PositionMs { get; set; } = 30000;
        public bool Paused { get; private set; }
        public bool Previewing { get; private set; }
        public float Rate { get; private set; } = 1;
        public int Restores { get; private set; }
        public void Capture() { events.Add("capture"); EntryPositionMs = PositionMs; Paused = true; }
        public void Pause(bool paused) { events.Add("pause:" + paused); Paused = paused; }
        public void Preview(bool enabled) { events.Add("preview:" + enabled); Previewing = enabled; }
        public void SetRate(float rate) { events.Add("rate:" + rate.ToString(CultureInfo.InvariantCulture)); Rate = rate; }
        public void Seek(double milliseconds) { events.Add("seek:" + milliseconds.ToString(CultureInfo.InvariantCulture)); PositionMs = milliseconds; }
        public void Restore(ExitBehavior behavior)
        {
            events.Add("restore:" + behavior);
            Restores++;
            if (behavior == ExitBehavior.ReturnToEntryPosition) PositionMs = EntryPositionMs;
            Paused = Previewing = false;
            Rate = 1;
        }
    }

    private sealed class View(List<string> events) : IPracticeView
    {
        public int Disposals { get; private set; }
        public PracticeViewState Latest { get; private set; }
        public void Update(PracticeViewState state) { Latest = state; events.Add("view:" + state.State); }
        public void Press(int button) => events.Add("press:" + button);
        public void RenderPreview(PracticeState state) => events.Add("render:" + state);
        public void Dispose() { events.Add("view.dispose"); Disposals++; }
    }
}
