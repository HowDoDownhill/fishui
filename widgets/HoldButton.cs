using System;
using Godot;

namespace FishUI;

/// <summary>
/// A list button for an action that cannot be undone. It runs only after A, Enter or the mouse
/// button stays down for HoldSeconds, and a fill crosses the button while it is held. Letting go
/// early drains the fill and does nothing.
/// </summary>
public partial class HoldButton : Button
{
    public const double HoldSeconds = 0.9;
    private const double DrainRate = 3.0;

    /// <summary>Raised once the button has been held for the full time.</summary>
    public event Action Held;

    private Panel _fill;
    private double _progress;
    private bool _down;
    private bool _done;

    public HoldButton() { }

    public HoldButton(string text, Action held = null)
    {
        Text = text;
        FocusMode = FocusModeEnum.All;
        ThemeTypeVariation = "WarningListButton";
        Alignment = HorizontalAlignment.Left;

        _fill = new Panel { ShowBehindParent = true, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        _fill.AddThemeStyleboxOverride("panel", Ui.Box(new Color(Appearance.WarningText, 0.35f), 10));
        AddChild(_fill);

        if (held != null) { Held += held; }
        ButtonDown += () =>
        {
            _down = true;
            _done = false;
            SetProcess(true);
        };
        ButtonUp += () => _down = false;
        FocusExited += () => _down = false;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (_done) { return; }

        if (_down)
        {
            _progress = Math.Min(1.0, _progress + delta / HoldSeconds);
            DrawFill();
            if (_progress >= 1.0)
            {
                _done = true;
                _down = false;
                SetProcess(false);
                Held?.Invoke();
            }
            return;
        }

        _progress = Math.Max(0.0, _progress - delta * DrainRate);
        DrawFill();
        if (_progress <= 0.0) { SetProcess(false); }
    }

    private void DrawFill()
    {
        _fill.Visible = _progress > 0.0;
        _fill.Position = Vector2.Zero;
        _fill.Size = new Vector2(Size.X * (float)_progress, Size.Y);
    }
}
