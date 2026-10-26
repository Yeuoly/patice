using System;

namespace Patice.Practice;

public interface IPracticeView : IDisposable
{
    void Update(PracticeSession session);
    void Press(int button);
    void RenderPreview(PracticeState state);
}
