using System;

namespace Patice.Practice;

public interface IPracticeView : IDisposable
{
    void Update(PracticeViewState state);
    void Press(int button);
    void RenderPreview(PracticeState state);
}
