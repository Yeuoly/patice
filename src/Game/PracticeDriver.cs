using System;
using Manager;
using Patice.Practice;
using Patice.UI;
using UnityEngine;

namespace Patice.Game;

// Unity callbacks and cabinet input only; the controller owns the practice lifetime.
public sealed class PracticeDriver : MonoBehaviour
{
    private static readonly (int Button, PracticeAction Action)[] bindings =
    {
        (5, PracticeAction.Cancel), (6, PracticeAction.Previous),
        (3, PracticeAction.Next), (1, PracticeAction.ChangeRate),
        (2, PracticeAction.Seek), (8, PracticeAction.ToggleLoop),
        (4, PracticeAction.Confirm), (7, PracticeAction.SetLoopEnd)
    };
    private int monitor;
    public PracticeController Controller { get; private set; }

    public void Initialize(int monitorIndex, PracticeController controller)
    {
        monitor = monitorIndex;
        Controller = controller;
        Controller.Open(() => new PracticeView(monitor, GetComponent<Monitor.GameMonitor>()));
    }

    // Keep input and transitions before the native GameProcess.OnUpdate.
    public void Step()
    {
        foreach (var binding in bindings)
        {
            if (!InputManager.GetButtonDown(monitor, (InputManager.ButtonSetting)(binding.Button - 1))) continue;
            var action = binding.Button == 2 && Controller.Session.State == PracticeState.Seeking
                ? PracticeAction.SetLoopStart : binding.Action;
            Controller.Handle(action, binding.Button);
            break;
        }
        Controller.Tick(Time.unscaledDeltaTime);
        if (Controller.IsClosed) GameHooks.Close();
    }

    private void LateUpdate()
    {
        if (Controller == null) return;
        try { Controller.RenderPreview(); }
        catch (Exception exception)
        {
            MelonLoader.MelonLogger.Error($"[PracticeMode] Preview rendering failed: {exception}");
            GameHooks.Close();
        }
    }

    public void Close()
    {
        try { Controller?.Dispose(); }
        finally { Controller = null; }
    }

    private void OnDestroy() => Close();
}
