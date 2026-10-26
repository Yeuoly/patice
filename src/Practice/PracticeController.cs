using System;
using Patice.Domain;

namespace Patice.Practice;

public sealed class PracticeController : IDisposable
{
    private readonly IPlayback playback;
    private IPracticeView view;
    private bool restored;
    public PracticeSession Session { get; }
    public bool IsClosed => Session.State == PracticeState.Closed;
    public bool PreventGameEnd => Session.PreventGameEnd;

    public PracticeController(IPlayback playback, Segment[] segments)
    {
        this.playback = playback;
        Session = new PracticeSession(playback, segments);
    }

    public void Open(Func<IPracticeView> createView)
    {
        playback.Capture();
        // Capture the background before preview playback changes the game.
        view = createView();
        Session.Open();
        view.Update(Session);
    }

    public void Entry()
    {
        Session.Entry();
        RestoreIfClosed();
    }

    public void Handle(PracticeAction action, int button)
    {
        var accepted = Session.Handle(action);
        RestoreIfClosed();
        if (accepted) view.Press(button);
    }

    public void Tick(double seconds)
    {
        Session.Tick(seconds);
        if (IsClosed) Dispose();
        else view.Update(Session);
    }

    public void RenderPreview() => view?.RenderPreview(Session.State);

    private void RestoreIfClosed()
    {
        if (!IsClosed || restored) return;
        restored = true;
        playback.Restore(Session.ExitBehavior);
    }

    public void Dispose()
    {
        if (!IsClosed)
            Session.End(Session.State == PracticeState.Playing || Session.State == PracticeState.ExitPrompt
                ? ExitBehavior.ContinueFromCurrentPosition : ExitBehavior.ReturnToEntryPosition);
        try { RestoreIfClosed(); }
        finally
        {
            var closing = view;
            view = null;
            closing?.Dispose();
        }
    }
}
