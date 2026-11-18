using System.Collections.Generic;
using Godot;

namespace FishUI;

/// <summary>
/// Builders for controls made in code. The look comes from the FishUI theme resource and its type
/// variations, frosted mica panels, controller glyphs, and the Appearance palette colours.
/// </summary>
public static class Ui
{
    private const string MicaMaterialPath = FishPaths.MicaMaterial;

    /// <summary>Left and right content margin, in 1080p units.</summary>
    public const int Margin = 60;

    /// <summary>Gap between the window edge and a panel.</summary>
    public const int PanelGap = 10;

    /// <summary>Left and right padding inside the top bar and footer.</summary>
    public const int ChromePad = 28;

    /// <summary>Corner radius of buttons, keys and other small boxes.</summary>
    public const int Radius = 3;

    /// <summary>Corner radius of panels and overlays.</summary>
    public const int PanelRadius = 4;

    public static Color Text => Appearance.Text;
    public static Color TextDim => Appearance.CaptionText;
    public static Color Accent => Appearance.Accent;
    public static Color AccentDim => new(Appearance.Accent, 0.35f);
    public static Color Surface => Appearance.Overlay(0.07f, 0.06f);
    public static Color SurfaceHi => Appearance.Overlay(0.14f, 0.1f);

    public static ShaderMaterial Mica => GD.Load<ShaderMaterial>(MicaMaterialPath);

    public static StyleBoxFlat Box(Color bg, int radius = Radius, int border = 0, Color? borderColor = null, int padX = 0, int padY = 0, int expand = 0)
    {
        var box = new StyleBoxFlat { BgColor = bg, BorderColor = borderColor ?? Colors.White, AntiAliasing = true };
        box.SetCornerRadiusAll(radius);
        box.SetBorderWidthAll(border);
        box.SetExpandMarginAll(expand);
        box.ContentMarginLeft = padX;
        box.ContentMarginRight = padX;
        box.ContentMarginTop = padY;
        box.ContentMarginBottom = padY;
        return box;
    }

    /// <param name="variation">A theme type from app_theme.tres: DisplayLabel, TitleLabel, CaptionLabel, MutedLabel, AccentLabel, WarningLabel.</param>
    public static Label Label(string text, int size = 0, Color? color = null, string variation = null)
    {
        var label = new Label { Text = text ?? "", MouseFilter = Control.MouseFilterEnum.Ignore };
        if (variation != null) { label.ThemeTypeVariation = variation; }
        if (size > 0) { label.AddThemeFontSizeOverride("font_size", size); }
        if (color != null) { label.AddThemeColorOverride("font_color", color.Value); }
        return label;
    }

    /// <summary>
    /// The box behind the list button for the page or section being shown: a faint accent wash
    /// with an accent bar down its left edge.
    /// </summary>
    public static StyleBoxFlat CurrentMarker()
    {
        var box = Box(new Color(Appearance.Accent, 0.16f), 0, padX: 18, padY: 8);
        box.BorderColor = Appearance.Accent;
        box.BorderWidthLeft = 3;
        return box;
    }

    /// <summary>A list button: no box of its own, marked by the gliding selection highlight.</summary>
    public static Button Button(string text, System.Action pressed = null)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.All, ThemeTypeVariation = "ListButton" };
        if (pressed != null) { button.Pressed += pressed; }
        return button;
    }

    /// <summary>A list button with a faint box, for actions that sit directly on the background.</summary>
    public static Button ActionButton(string text, System.Action pressed = null)
    {
        var button = Button(text, pressed);
        button.AddThemeStyleboxOverride("normal", Box(Surface, padX: 18, padY: 8));
        button.AddThemeStyleboxOverride("hover", Box(SurfaceHi, padX: 18, padY: 8));
        return button;
    }

    /// <summary>A footer prompt: the glyph for an input action beside its label. Clicking it performs the action.</summary>
    public static Button Hint(string action, string label, int fontSize = 0)
    {
        var button = new Button
        {
            Text = label,
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            ThemeTypeVariation = "FlatButton",
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
        {
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }
        if (fontSize > 0) { button.AddThemeFontSizeOverride("font_size", fontSize); }
        ControllerGlyph.Apply(button, action);
        button.Pressed += () =>
        {
            Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
            Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
        };
        return button;
    }

    /// <summary>A flat panel with rounded corners and drop shadow, for content inside a page.</summary>
    public static PanelContainer Panel(Control content = null, int padX = 24, int padY = 16)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Appearance.SurfaceStyle);
        if (content != null) { panel.AddChild(Pad(content, padX, padY, padX, padY)); }
        return panel;
    }

    /// <summary>A bar that runs to the window edge: the wash behind the top bar, sidebar and footer.</summary>
    public static PanelContainer Chrome(Control content, int padX, int padY)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Appearance.ChromeStyle);
        panel.AddChild(Pad(content, padX, padY, padX, padY));
        return panel;
    }

    /// <summary>
    /// A frosted panel with rounded corners and drop shadow, for things that float over moving
    /// content, such as a toast or controls over a video. Simple draws it flat.
    /// </summary>
    public static PanelContainer MicaPanel(Control content = null, int padX = 24, int padY = 16)
    {
        var panel = new PanelContainer { Material = Mica };
        panel.AddThemeStyleboxOverride("panel", Box(Colors.Black, PanelRadius));
        MicaShadow.Attach(panel, MicaShadow.DefaultColor);
        if (content != null) { panel.AddChild(Pad(content, padX, padY, padX, padY)); }
        return panel;
    }

    /// <summary>The header bar at the top of a screen.</summary>
    public static Control Header(string title, params Control[] right)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 28);
        row.AddChild(Label(title, variation: "DisplayLabel"));
        row.AddChild(Spacer(expand: true));
        foreach (var control in right) { row.AddChild(control); }
        return Chrome(row, ChromePad, 12);
    }

    public static Control Spacer(int width = 0, int height = 0, bool expand = false)
    {
        var spacer = new Control { CustomMinimumSize = new Vector2(width, height), MouseFilter = Control.MouseFilterEnum.Ignore };
        if (expand)
        {
            spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            spacer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        }
        return spacer;
    }

    public static MarginContainer Pad(Control child, int left, int top, int right, int bottom)
    {
        var margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        if (child != null) { margin.AddChild(child); }
        return margin;
    }

    public static void Fill(Control control) => control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

    /// <summary>
    /// Keeps directional focus inside a vertical list: up from the first and down from the last wrap around,
    /// left and right stay put. Used by modal menus so focus cannot reach the screen underneath.
    /// </summary>
    public static void ConfineVertical(IReadOnlyList<Control> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var self = item.GetPath();
            item.FocusNeighborLeft = self;
            item.FocusNeighborRight = self;
            item.FocusNeighborTop = items[(i + items.Count - 1) % items.Count].GetPath();
            item.FocusNeighborBottom = items[(i + 1) % items.Count].GetPath();
            item.FocusPrevious = item.FocusNeighborTop;
            item.FocusNext = item.FocusNeighborBottom;
        }
    }

    public static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0) { seconds = 0; }
        var span = System.TimeSpan.FromSeconds(seconds);
        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}" : $"{span.Minutes}:{span.Seconds:00}";
    }
}
