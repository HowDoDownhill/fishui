using System;
using System.Collections.Generic;
using Godot;

namespace FishUI;

/// <summary>
/// A question about something that cannot be undone. Focus starts on the safe choice, and the
/// risky one is a HoldButton, so a stray press of A changes nothing.
/// </summary>
public partial class ConfirmOverlay : Overlay
{
    private readonly string _title;
    private readonly string _body;
    private readonly string _holdLabel;
    private readonly string _cancelLabel;
    private readonly Action<bool> _answered;
    private readonly List<Control> _buttons = new();
    private bool _confirmed;

    public ConfirmOverlay() { }

    /// <param name="holdLabel">Names the action and the hold, such as "Hold to delete".</param>
    /// <param name="answered">Called with true after the hold completes, or false when the question is dismissed.</param>
    public ConfirmOverlay(string title, string body, string holdLabel, string cancelLabel, Action<bool> answered)
    {
        _title = title;
        _body = body;
        _holdLabel = holdLabel;
        _cancelLabel = cancelLabel;
        _answered = answered;
    }

    protected override void Build(PanelContainer panel)
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(680, 0) };
        column.AddThemeConstantOverride("separation", 8);
        panel.AddChild(Ui.Pad(column, 14, 16, 14, 16));
        column.AddChild(Ui.Pad(Ui.Label(_title, variation: "TitleLabel"), 18, 0, 18, 6));

        if (!string.IsNullOrEmpty(_body))
        {
            var body = Ui.Label(_body, 24);
            body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.CustomMinimumSize = new Vector2(680, 0);
            column.AddChild(Ui.Pad(body, 18, 0, 18, 14));
        }

        var cancel = Ui.Button(_cancelLabel, Dismiss);
        cancel.Alignment = HorizontalAlignment.Left;
        column.AddChild(cancel);
        _buttons.Add(cancel);

        var hold = new HoldButton(_holdLabel, () =>
        {
            _confirmed = true;
            Dismiss();
        });
        column.AddChild(hold);
        _buttons.Add(hold);

        Ui.ConfineVertical(_buttons);
    }

    public override void OnDismissed() => _answered?.Invoke(_confirmed);

    public override void FocusDefault() => _buttons[0].GrabFocus();
}
