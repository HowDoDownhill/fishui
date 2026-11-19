using System;
using System.Collections.Generic;
using Godot;

namespace FishUI;

/// <summary>
/// A ring of up to eight choices around the centre of the screen. Pushing the stick or D-pad
/// toward a choice marks it, A runs it, and B closes the ring. Good for a handful of actions wanted
/// in the middle of a game, where the direction becomes muscle memory.
/// </summary>
public partial class RadialMenu : Overlay
{
    public const int MaxItems = 8;
    private const float OuterRadius = 330;
    private const float InnerRadius = 130;
    private const float LabelRadius = 232;
    private const float GapPixels = 5;
    private const float Deadzone = 0.5f;
    private const int ArcSteps = 24;

    private readonly string _title;
    private readonly IReadOnlyList<(string Label, Action Action)> _items;
    private readonly Action _dismissed;
    private readonly List<Label> _labels = new();
    private Control _dial;
    private int _selected = -1;
    private bool _chosen;

    public RadialMenu() { }

    /// <param name="items">The choices, clockwise from the top. Only the first eight are shown.</param>
    /// <param name="dismissed">Called when the ring closes without a choice.</param>
    public RadialMenu(string title, IReadOnlyList<(string Label, Action Action)> items, Action dismissed = null)
    {
        _title = title;
        _items = items;
        _dismissed = dismissed;
        UseMica = false;
        ShadowSize = 0;
        Transition = PanelTransition.ScaleFade;
    }

    private int Count => Math.Min(_items.Count, MaxItems);

    protected override void Build(PanelContainer panel)
    {
        panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

        var size = new Vector2(OuterRadius * 2 + 8, OuterRadius * 2 + 8);
        _dial = new Control { CustomMinimumSize = size, FocusMode = FocusModeEnum.All };
        _dial.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        _dial.Draw += DrawDial;
        panel.AddChild(_dial);

        var centre = size / 2;
        var title = Ui.Label(_title, variation: "TitleLabel");
        PlaceLabel(title, centre, InnerRadius * 1.7f);
        _dial.AddChild(title);

        for (var i = 0; i < Count; i++)
        {
            var label = Ui.Label(_items[i].Label, 26);
            PlaceLabel(label, centre + Direction(i) * LabelRadius, 190);
            _dial.AddChild(label);
            _labels.Add(label);
        }
    }

    private static void PlaceLabel(Label label, Vector2 centre, float width)
    {
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.Size = new Vector2(width, 90);
        label.Position = centre - label.Size / 2;
    }

    /// <summary>The middle of slice i, as a unit vector. Slice 0 points up and the rest follow clockwise.</summary>
    private Vector2 Direction(int i) => Vector2.Up.Rotated(Mathf.Tau * i / Count);

    private void DrawDial()
    {
        var centre = _dial.Size / 2;
        var span = Mathf.Tau / Count;
        var surface = new Color(Appearance.SurfaceStyle.BgColor, 0.94f);

        for (var i = 0; i < Count; i++)
        {
            var middle = -Mathf.Pi / 2 + span * i;
            var points = Slice(centre, middle - span / 2, middle + span / 2);
            var selected = i == _selected;
            _dial.DrawColoredPolygon(points, selected ? surface.Lerp(Appearance.Accent, 0.3f) : surface);

            // Every slice has a faint edge, so the ring reads over a dark page. The marked one takes the accent.
            var outline = new Vector2[points.Length + 1];
            points.CopyTo(outline, 0);
            outline[^1] = points[0];
            if (selected) { _dial.DrawPolyline(outline, Appearance.Accent, 3, true); }
            else { _dial.DrawPolyline(outline, new Color(Appearance.BodyText, 0.22f), 1.5f, true); }
        }
    }

    /// <summary>One slice of the ring, with a gap of the same width at both radii so the gaps run parallel.</summary>
    private static Vector2[] Slice(Vector2 centre, float from, float to)
    {
        var points = new Vector2[(ArcSteps + 1) * 2];
        var outerInset = GapPixels / 2 / OuterRadius;
        var innerInset = GapPixels / 2 / InnerRadius;
        for (var step = 0; step <= ArcSteps; step++)
        {
            var t = step / (float)ArcSteps;
            var outer = Mathf.Lerp(from + outerInset, to - outerInset, t);
            var inner = Mathf.Lerp(to - innerInset, from + innerInset, t);
            points[step] = centre + Vector2.Right.Rotated(outer) * OuterRadius;
            points[ArcSteps + 1 + step] = centre + Vector2.Right.Rotated(inner) * InnerRadius;
        }
        return points;
    }

    public override void _Process(double delta)
    {
        if (_dial == null || !IsOpen) { return; }
        var push = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
        if (push.Length() < Deadzone) { return; }

        // The angle clockwise from straight up, rounded to the nearest slice.
        var angle = Mathf.PosMod(Mathf.Atan2(push.X, -push.Y), Mathf.Tau);
        Select(Mathf.RoundToInt(angle / Mathf.Tau * Count) % Count);
    }

    private void Select(int index)
    {
        if (index == _selected) { return; }
        _selected = index;
        for (var i = 0; i < _labels.Count; i++)
        {
            _labels[i].AddThemeColorOverride("font_color", i == index ? Appearance.StrongText : Appearance.BodyText);
        }
        UiSounds.Play(UiSounds.Sound.Move);
        _dial.QueueRedraw();
    }

    public override bool OnRawInput(InputEvent e)
    {
        // Directions mark a slice through _Process. Swallowing them here keeps focus where it is.
        if (e.IsAction("ui_left") || e.IsAction("ui_right") || e.IsAction("ui_up") || e.IsAction("ui_down")) { return true; }
        if (!e.IsActionPressed("ui_accept")) { return false; }
        if (_selected < 0) { return true; }

        _chosen = true;
        UiSounds.Play(UiSounds.Sound.Select);
        var action = _items[_selected].Action;
        Dismiss();
        action?.Invoke();
        return true;
    }

    public override void OnDismissed()
    {
        if (!_chosen) { _dismissed?.Invoke(); }
    }

    public override void FocusDefault() => _dial.GrabFocus();
}
