using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace FishUI;

/// <summary>A full page in the navigation stack. AppShell routes input to the top screen only.</summary>
public abstract partial class Screen : Control
{
    private Control _lastFocus;

    /// <summary>Button prompts for the footer, as (input action, label) pairs.</summary>
    public virtual IEnumerable<(string Action, string Label)> Hints => new[] { (Actions.Accept, "Select"), (Actions.Back, "Back") };

    /// <summary>The sidebar page this screen is, such as "movies". Null for screens without the sidebar.</summary>
    public virtual string Tab => null;

    /// <summary>The page name shown in the top bar beside the app's title.</summary>
    public virtual string Title => null;

    /// <summary>A short status for the right of the top bar, such as an item count.</summary>
    public string Info { get; private set; }

    /// <summary>False hides the top bar and footer, for full-window content such as a video player.</summary>
    public virtual bool ShowChrome => true;

    /// <summary>False hides the top bar, for screens shown before the app has anything to put there.</summary>
    public virtual bool ShowTopBar => true;

    /// <summary>
    /// Whether the shell's chords work here: LB with RB for the sidebar, LT with RT for the quick
    /// actions ring. Off for screens without the top bar or footer, such as sign-in and a video.
    /// </summary>
    public virtual bool AllowsChords => ShowChrome && ShowTopBar;

    protected void SetInfo(string info)
    {
        Info = info;
        AppShell.I?.RefreshInfo(this);
    }

    protected Screen()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    /// <summary>Called once, after the screen joins the tree.</summary>
    public virtual void OnEnter() { }

    /// <summary>Called when a screen above this one closes.</summary>
    public virtual void OnResume() { }

    /// <summary>Handles input the focused control did not use. Return true to consume it.</summary>
    public virtual bool OnInput(InputEvent e) => false;

    /// <summary>Sees input before the focus system does. Return true to consume it.</summary>
    public virtual bool OnRawInput(InputEvent e) => false;

    /// <summary>Called when LB or RB is held. Return true when the screen used it, such as to open a jump menu.</summary>
    public virtual bool OnShoulderHold() => false;

    /// <summary>
    /// Called when LT (-1) or RT (1) is pressed and let go on its own, or for the [ and ] keys.
    /// Return true when the screen used it, such as to step a grid through the alphabet.
    /// </summary>
    public virtual bool OnTrigger(int direction) => false;

    /// <summary>The words for a footer prompt that shows LT and RT, such as "Letter". Null leaves the prompt out.</summary>
    public virtual string TriggerLabel => null;

    /// <summary>Called for B. Return true when the screen handled it and should stay open.</summary>
    public virtual bool OnBack() => false;

    /// <summary>Called on the top screen before the app exits, so it can save or report its state.</summary>
    public virtual Task OnQuitAsync() => Task.CompletedTask;

    /// <summary>Focuses the screen's starting control.</summary>
    public virtual void FocusDefault() { }

    public void SaveFocus()
    {
        var owner = GetViewport()?.GuiGetFocusOwner();
        _lastFocus = owner != null && IsAncestorOf(owner) ? owner : null;
    }

    public void RestoreFocus()
    {
        if (_lastFocus != null && IsInstanceValid(_lastFocus) && _lastFocus.IsInsideTree() && _lastFocus.IsVisibleInTree())
        {
            _lastFocus.GrabFocus();
        }
        else { FocusDefault(); }
    }
}
