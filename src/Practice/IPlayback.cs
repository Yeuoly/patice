namespace Patice.Practice;

public enum ExitBehavior { ReturnToEntryPosition, ContinueFromCurrentPosition }
public enum PracticeAction { Cancel, Previous, Next, ChangeRate, Seek, ToggleLoop, Confirm, SetLoopStart, SetLoopEnd }

public interface IPlayback
{
    double EntryPositionMs { get; }
    double DurationMs { get; }
    double PositionMs { get; }
    void Capture();
    void Preview(bool enabled);
    void Pause(bool paused);
    void SetRate(float rate);
    void Seek(double milliseconds);
    void Restore(ExitBehavior behavior);
}
