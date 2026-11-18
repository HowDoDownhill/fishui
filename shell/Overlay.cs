using Godot;

namespace FishUI;

/// <summary>
/// A modal layer above the screens: menus, prompts and progress. It is a UiPanel, so it gets the
/// frosted panel, drop shadow and scale-fade transition. Open one with AppShell.OpenOverlay.
/// </summary>
public abstract partial class Overlay : UiPanel
{
    public Control PreviousFocus;

    protected Overlay()
    {
        BackdropDim = 0.45f;
        CenterContent = true;
    }

    public override void _Ready()
    {
        base._Ready();
        Build((PanelContainer)ContentRoot);
    }

    /// <summary>Fills the frosted panel with the overlay's content.</summary>
    protected abstract void Build(PanelContainer panel);

    /// <summary>Handles input the focused control did not use. Return true to consume it. B closes the overlay when this returns false.</summary>
    public virtual bool OnInput(InputEvent e) => false;

    /// <summary>Sees input before the focus system does. Return true to consume it.</summary>
    public virtual bool OnRawInput(InputEvent e) => false;

    public virtual void FocusDefault() { }

    /// <summary>Called when the overlay is dismissed, before it fades out.</summary>
    public virtual void OnDismissed() { }

    /// <summary>Closes this overlay through the shell, which returns focus to where it was.</summary>
    protected void Dismiss() => AppShell.I.CloseOverlay(this);
}
