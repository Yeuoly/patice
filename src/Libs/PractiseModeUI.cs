using AquaMai.Mods.UX.PracticeMode.Domain;
using Manager;
using UnityEngine;

namespace AquaMai.Mods.UX.PracticeMode.Libs;

public sealed class PracticeModeUI : MonoBehaviour
{
    private PracticeSession session;
    private PracticeView view;
    private int monitor;

    public void Initialize(int monitorIndex, Segment[] segments)
    {
        monitor = monitorIndex;
        // Snapshot the game before the preview seeks or enables autoplay.
        view = new PracticeView(monitor);
        session = new PracticeSession(new NativePracticePlayer(), segments);
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

    public void Entry() => session?.Entry();

    // Called before GameProcess.OnUpdate, not from an unordered MonoBehaviour.Update.
    public void Step()
    {
        if (session == null) return;
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
        if (NativePracticePlayer.PreviewActive) SoundManager.PauseMusic(true);
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
