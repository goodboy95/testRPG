using Godot;
using TownRpg.Application;

namespace TownRpg.Presentation;

/// One modal's lease and return route. Nested callers keep their own lease until return.
public sealed class ModalNavigation(Control view, ModalGate gate, string owner) : IDisposable
{
    private IDisposable? _lease;
    private Action? _return;
    public void Open(Action? returnTo = null)
    {
        if (_lease is null) { _lease = gate.Acquire(owner); _return = returnTo; }
        Input.MouseMode = Input.MouseModeEnum.Visible;
        view.Visible = true;
        UiStyles.FadeIn(view);
    }
    public void Close(bool returnToCaller = true)
    {
        UiStyles.ReleaseFocus(view);
        view.Visible = false;
        _lease?.Dispose(); _lease = null;
        var callback = _return; _return = null;
        if (returnToCaller) callback?.Invoke();
    }
    public void Dispose() { _return = null; _lease?.Dispose(); _lease = null; }
}
