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
            "seek:60000", "pause:False", "view:Selecting"
        }, flow.Events);
        Assert.True(flow.Controller.PreventGameEnd);
        flow.Controller.RenderPreview();
        Assert.Equal("render:Selecting", flow.Events.Last());

        flow.Player.PositionMs = 76000;
        flow.Controller.Tick(0);
        Assert.Equal(60000, flow.Player.PositionMs);
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
        Assert.Equal("Final", flow.Controller.Session.Current.name);
        flow.Press(PracticeAction.Next, 3);
        Assert.Equal("S1", flow.Controller.Session.Current.name);
        flow.Press(PracticeAction.Next, 3);
        Assert.Equal("S2", flow.Controller.Session.Current.name);

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
        Assert.Equal(new[] { "pause:True", "preview:False", "seek:104000", "pause:False" }, flow.PlaybackEvents);
        Assert.True(flow.Controller.PreventGameEnd);

        flow.Player.PositionMs = 115000;
        flow.Controller.Tick(0);
        Assert.Equal(104000, flow.Player.PositionMs);
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
        flow.Events.Clear();
        flow.Controller.Tick(0);
        Assert.Equal(new[] { "restore:ContinueFromCurrentPosition", "view.dispose" }, flow.Events);
        Assert.Equal(76001, flow.Player.PositionMs);
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
        Assert.Equal(35000, flow.Controller.Session.SeekPositionMs);
        flow.Press(PracticeAction.ChangeRate, 1);
        Assert.Equal(.9f, flow.Player.Rate);
        flow.Events.Clear();
        flow.Press(PracticeAction.Confirm, 4);
        Assert.Equal(new[] { "preview:False", "seek:35000", "pause:False" }, flow.PlaybackEvents);
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
        for (var i = 0; i < 8; i++) flow.Press(PracticeAction.Previous, 6);
        Assert.Equal(0, flow.Player.PositionMs);
        for (var i = 0; i < 30; i++) flow.Press(PracticeAction.Next, 3);
        Assert.Equal(flow.Player.DurationMs, flow.Player.PositionMs);
        flow.Press(PracticeAction.Cancel, 5);
        Assert.Equal(PracticeState.Selecting, flow.Controller.Session.State);
        Assert.Equal(104000, flow.Player.PositionMs);
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
        Assert.Equal(60000, flow.Player.PositionMs);
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

    private sealed class Flow : IDisposable
    {
        public readonly List<string> Events = new();
        public readonly Playback Player;
        public readonly View View;
        public readonly PracticeController Controller;
        public IEnumerable<string> PlaybackEvents => Events.Where(e => !e.StartsWith("view:") && !e.StartsWith("press:"));

        public Flow(bool open = true)
        {
            Player = new Playback(Events);
            View = new View(Events);
            Controller = new PracticeController(Player, new[]
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
        public void Update(PracticeSession session) => events.Add("view:" + session.State);
        public void Press(int button) => events.Add("press:" + button);
        public void RenderPreview(PracticeState state) => events.Add("render:" + state);
        public void Dispose() { events.Add("view.dispose"); Disposals++; }
    }
}
