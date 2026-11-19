using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace FishUI;

/// <summary>
/// The layer that holds open overlays: menus, questions, the quick panel, the radial menu and
/// prompts. It gives focus back when one closes. AppShell has one built in. An app with its own
/// scenes adds one as the last child of its root and sets RoutesInput, which gets it every overlay
/// without the rest of the shell.
/// </summary>
public partial class OverlayHost : Control
{
    private readonly List<Overlay> _overlays = new();

    /// <summary>
    /// Whether the host reads input itself. AppShell leaves this off and passes input in. An app
    /// with its own scenes turns it on, and skips its own input handling while HasOverlay is true.
    /// </summary>
    public bool RoutesInput;

    /// <summary>Called when the last overlay closes and the control that had focus is gone.</summary>
    public Action FocusFallback;

    public Overlay Top => _overlays.Count > 0 ? _overlays[^1] : null;
    public bool HasOverlay => _overlays.Count > 0;

    public override void _EnterTree()
    {
        Actions.Register();
        MouseFilter = MouseFilterEnum.Ignore;
        Ui.Fill(this);
    }

    public void Open(Overlay overlay)
    {
        overlay.Host = this;
        overlay.PreviousFocus = GetViewport().GuiGetFocusOwner();
        _overlays.Add(overlay);
        AddChild(overlay);
        overlay.Open();
        overlay.FocusDefault();
    }

    public void Close(Overlay overlay)
    {
        if (!_overlays.Remove(overlay)) { return; }
        overlay.Closed += overlay.QueueFree;
        overlay.Close();
        var focus = overlay.PreviousFocus;
        if (focus != null && IsInstanceValid(focus) && focus.IsInsideTree() && focus.IsVisibleInTree()) { focus.GrabFocus(); }
        else if (Top != null) { Top.FocusDefault(); }
        else
        {
            GetViewport().GuiReleaseFocus();
            FocusFallback?.Invoke();
        }
        // Last, because code waiting on the overlay's answer may open the next overlay right away,
        // and that one must take focus after this one has given it back.
        overlay.OnDismissed();
    }

    public void CloseAll()
    {
        foreach (var overlay in _overlays.ToArray()) { Close(overlay); }
    }

    /// <summary>Opens a grid of tiles for picking one of many.</summary>
    public void ShowJump(string title, IReadOnlyList<(string Label, string Caption)> tiles, int selected, Action<int> chosen) =>
        Open(new JumpOverlay(title, tiles, selected, chosen));

    public void ShowMenu(string title, IReadOnlyList<(string Label, Action Action)> items, int selected = 0) =>
        Open(new MenuOverlay(title, items, selected));

    /// <summary>Opens a ring of up to eight choices, picked by direction. Items run clockwise from the top.</summary>
    public void ShowRadial(string title, IReadOnlyList<(string Label, Action Action)> items) =>
        Open(new RadialMenu(title, items));

    /// <summary>Shows a menu and completes with the index chosen, or -1 when it is dismissed.</summary>
    /// <param name="body">Optional text explaining the question, shown above the choices.</param>
    public Task<int> ChooseAsync(string title, IReadOnlyList<string> options, string body = null)
    {
        var result = new TaskCompletionSource<int>();
        var items = options.Select((label, index) => (label, (Action)(() => result.TrySetResult(index)))).ToList();
        Open(new MenuOverlay(title, items, 0, () => result.TrySetResult(-1), body));
        return result.Task;
    }

    /// <summary>
    /// Asks about something that cannot be undone. Completes with true only after the risky choice
    /// is held down; B, or the safe choice, completes with false.
    /// </summary>
    /// <param name="holdLabel">Names the action and the hold, such as "Hold to delete".</param>
    public Task<bool> ConfirmHoldAsync(string title, string body, string holdLabel, string cancelLabel = "Cancel")
    {
        var result = new TaskCompletionSource<bool>();
        Open(new ConfirmOverlay(title, body, holdLabel, cancelLabel, answer => result.TrySetResult(answer)));
        return result.Task;
    }

    /// <summary>Asks for text with the on-screen keyboard. Null when cancelled.</summary>
    public Task<string> PromptAsync(string title, string initial = "", bool secret = false)
    {
        var prompt = new TextPrompt(title, initial, secret);
        Open(prompt);
        return prompt.Result;
    }

    /// <summary>Gives the top overlay first look at an input event. True when it used the event.</summary>
    public bool HandleRawInput(InputEvent e) => Top?.OnRawInput(e) ?? false;

    /// <summary>
    /// Gives the top overlay an event the focused control left alone. B closes the overlay when it
    /// has no use for the event. True when an overlay is open, so nothing below should act on it.
    /// </summary>
    public bool HandleInput(InputEvent e)
    {
        var overlay = Top;
        if (overlay == null) { return false; }
        if (overlay.OnInput(e)) { GetViewport().SetInputAsHandled(); }
        else if (e.IsActionPressed(Actions.Back))
        {
            Close(overlay);
            GetViewport().SetInputAsHandled();
        }
        return true;
    }

    public override void _Input(InputEvent e)
    {
        if (RoutesInput && HandleRawInput(e)) { GetViewport().SetInputAsHandled(); }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (RoutesInput) { HandleInput(e); }
    }
}
