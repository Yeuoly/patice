using Patice.Domain;
using Manager;
using UnityEngine;

namespace Patice.Libs;

public sealed class PracticeModeUI : MonoBehaviour
{
    private PracticeSession session;
    private PracticeView view;
    private int monitor;
    private Playback player;

    public void Initialize(int monitorIndex, Segment[] segments, Playback playback)
    {
        monitor = monitorIndex;
        player = playback;
        // Snapshot the game before the preview seeks or enables autoplay.
        view = new PracticeView(monitor);
        session = new PracticeSession(player, segments);
        session.Open();
        view.Update(session);
    }

    public bool PreventGameEnd => session != null && session.State != PracticeState.Closed &&
        (session.State != PracticeState.Playing || session.Loop);

    private void LateUpdate()
    {
        if (session == null) return;
        try { view.RenderPreview(session.State); }
        catch (System.Exception exception)
        {
            MelonLoader.MelonLogger.Error($"[PracticeMode] Preview rendering failed: {exception}");
            Close();
            Destroy(this);
        }
    }

    public void Entry() => session.Entry();

    // Called before GameProcess.OnUpdate, not from an unordered MonoBehaviour.Update.
    public void Step()
    {
        foreach (var number in new[] { 6, 3, 7, 8, 4 })
        {
            if (!InputManager.GetButtonDown(monitor, (InputManager.ButtonSetting)(number - 1))) continue;
            if (session.Button(number)) view.Press(number);
            break;
        }
        session.Tick(Time.unscaledDeltaTime);
        if (session.State == PracticeState.Closed)
        {
            Close();
            Destroy(this);
            return;
        }
        if (player.Previewing) SoundManager.PauseMusic(true);
        view.Update(session);
    }

    public void Close()
    {
        try { session?.Dispose(); }
        finally
        {
            session = null;
            view?.Dispose();
            view = null;
            if (PracticeMode.ui == this) PracticeMode.ui = null;
        }
    }

    private void OnDestroy() => Close();
}
