using System;
using System.Collections.Generic;
using Godot;

namespace FishUI;

/// <summary>
/// A panel that slides in from the right over whatever is on screen, for settings wanted in the
/// middle of a game or a video: volume, a toggle, a short list of actions. Add rows, then open it
/// with AppShell.OpenOverlay or OverlayHost.Open. B closes it.
/// </summary>
public partial class QuickPanel : Overlay
{
    public const int Width = 560;
    private const int SlideDistance = 48;
    private const float SliderStep = 0.05f;

    // Where a row's value starts, so a long one is cut short before it reaches the row's label.
    private const int ValueStart = 210;

    private readonly string _title;
    private readonly List<Control> _rows = new();

    public QuickPanel() { }

    public QuickPanel(string title)
    {
        _title = title;
        CenterContent = false;
        Transition = PanelTransition.Fade;
        BackdropDim = 0.35f;
    }

    /// <summary>A row that runs an action. The panel closes first unless keepOpen is set.</summary>
    /// <param name="value">Short text for the right of the row, such as the current choice.</param>
    public QuickPanel AddAction(string label, Action pressed, string value = null, bool keepOpen = false)
    {
        var button = Row(label, out var valueLabel);
        valueLabel.Text = value ?? "";
        button.Pressed += () =>
        {
            if (!keepOpen) { Dismiss(); }
            pressed?.Invoke();
        };
        return this;
    }

    /// <summary>A row that switches between On and Off when pressed.</summary>
    public QuickPanel AddToggle(string label, bool on, Action<bool> changed)
    {
        var button = Row(label, out var valueLabel);
        ShowToggle(valueLabel, on);
        button.Pressed += () =>
        {
            on = !on;
            ShowToggle(valueLabel, on);
            UiSounds.Play(UiSounds.Sound.Switch);
            changed?.Invoke(on);
        };
        return this;
    }

    /// <summary>A row with a bar that Left and Right move between 0 and 1.</summary>
    public QuickPanel AddSlider(string label, float value, Action<float> changed)
    {
        var button = Row(label, out _);
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.001,
            Value = Mathf.Clamp(value, 0, 1),
            ShowPercentage = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        bar.SetAnchorsPreset(LayoutPreset.CenterRight);
        bar.OffsetLeft = -258;
        bar.OffsetRight = -18;
        bar.OffsetTop = -5;
        bar.OffsetBottom = 5;
        button.AddChild(bar);

        button.GuiInput += e =>
        {
            var direction = e.IsActionPressed("ui_left", true) ? -1 : e.IsActionPressed("ui_right", true) ? 1 : 0;
            if (direction == 0) { return; }
            bar.Value = Mathf.Clamp(bar.Value + direction * SliderStep, 0, 1);
            changed?.Invoke((float)bar.Value);
            button.AcceptEvent();
        };
        return this;
    }

    private Button Row(string label, out Label valueLabel)
    {
        var button = Ui.Button(label);
        button.Alignment = HorizontalAlignment.Left;
        valueLabel = Ui.Label("", variation: "CaptionLabel");
        valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
        valueLabel.VerticalAlignment = VerticalAlignment.Center;
        button.AddChild(valueLabel);
        Ui.Fill(valueLabel);
        valueLabel.OffsetLeft = ValueStart;
        valueLabel.OffsetRight = -18;
        valueLabel.ClipText = true;
        valueLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _rows.Add(button);
        return button;
    }

    private static void ShowToggle(Label label, bool on)
    {
        label.Text = on ? "On" : "Off";
        label.ThemeTypeVariation = on ? "AccentLabel" : "CaptionLabel";
    }

    protected override void Build(PanelContainer panel)
    {
        panel.SetAnchorsPreset(LayoutPreset.RightWide);
        panel.OffsetLeft = -Width;
        panel.OffsetRight = 0;
        panel.OffsetTop = 0;
        panel.OffsetBottom = 0;

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 5);
        column.AddChild(Ui.Pad(Ui.Label(_title, variation: "TitleLabel"), 18, 0, 18, 10));
        foreach (var row in _rows) { column.AddChild(row); }
        panel.AddChild(Ui.Pad(column, 14, 20, 14, 16));
        Ui.ConfineVertical(_rows);

        AboutToOpen += () =>
        {
            panel.OffsetLeft += SlideDistance;
            panel.OffsetRight += SlideDistance;
            var slide = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            slide.TweenProperty(panel, "offset_left", -Width, OpenDuration);
            slide.TweenProperty(panel, "offset_right", 0, OpenDuration);
        };
    }

    public override void FocusDefault()
    {
        if (_rows.Count > 0) { _rows[0].GrabFocus(); }
    }
}
