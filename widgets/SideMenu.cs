using System;
using System.Collections.Generic;
using Godot;

namespace FishUI;

/// <summary>One headed group of choices in a <see cref="SideMenu"/>.</summary>
public sealed record MenuGroup(string Heading, IReadOnlyList<(string Label, Action Action)> Items);

/// <summary>
/// A menu against the right edge of the window, as tall as the window, with its choices in headed
/// groups. It is the app's Start menu: AppShell opens it from every page that has the bars, with
/// the page's own group first. B or Start closes it.
/// </summary>
public partial class SideMenu : Overlay
{
    public const int Width = 460;
    private const int SlideDistance = 48;
    private const int HeadingInset = 18;
    private const int HeadingGap = 14;
    private const int FocusMargin = 64;

    private readonly IReadOnlyList<MenuGroup> _groups;
    private readonly List<Control> _buttons = new();

    public SideMenu() { }

    public SideMenu(IReadOnlyList<MenuGroup> groups)
    {
        _groups = groups;
        CenterContent = false;
        Transition = PanelTransition.Fade;
        BackdropDim = 0.35f;
    }

    protected override void Build(PanelContainer panel)
    {
        panel.SetAnchorsPreset(LayoutPreset.RightWide);
        panel.OffsetLeft = -Width;
        panel.OffsetRight = 0;
        panel.OffsetTop = 0;
        panel.OffsetBottom = 0;

        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 5);
        var isFirst = true;
        foreach (var group in _groups)
        {
            if (group == null || group.Items.Count == 0) { continue; }
            var heading = Ui.Label(group.Heading, variation: "CaptionLabel");
            list.AddChild(Ui.Pad(heading, HeadingInset, isFirst ? 0 : HeadingGap, HeadingInset, 0));
            isFirst = false;
            foreach (var (label, action) in group.Items)
            {
                var button = Ui.Button(label);
                button.Alignment = HorizontalAlignment.Left;
                button.ClipText = true;
                button.Pressed += () =>
                {
                    Dismiss();
                    action?.Invoke();
                };
                list.AddChild(button);
                _buttons.Add(button);
            }
        }

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
        };
        scroll.AddChild(list);
        // The margin is wide enough that the first choice of a group brings its heading into view.
        FocusScroller.KeepVisible(scroll, list, FocusMargin, vertical: true);
        panel.AddChild(Ui.Pad(scroll, 14, 20, 14, 16));
        Ui.ConfineVertical(_buttons);

        AboutToOpen += () =>
        {
            panel.OffsetLeft += SlideDistance;
            panel.OffsetRight += SlideDistance;
            var slide = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            slide.TweenProperty(panel, "offset_left", -Width, OpenDuration);
            slide.TweenProperty(panel, "offset_right", 0, OpenDuration);
        };
    }

    public override bool OnInput(InputEvent e)
    {
        if (!e.IsActionPressed(Actions.Start)) { return false; }
        Dismiss();
        return true;
    }

    public override void FocusDefault()
    {
        if (_buttons.Count > 0) { _buttons[0].GrabFocus(); }
    }
}
