using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FishUI;

/// <summary>
/// One colour palette. Bg fills the window, Primary and Secondary colour the animated background
/// and give the accent its hue, and Panel's alpha sets how opaque frosted panels are.
/// </summary>
public readonly record struct Palette(string Name, Color Bg, Color Primary, Color Secondary, Color Panel)
{
    public Palette(string name, string bg, string primary, string secondary, string panel)
        : this(name, new Color(bg), new Color(primary), new Color(secondary), new Color(panel))
    {
    }

    public static implicit operator Palette((Color Bg, Color Primary, Color Secondary, Color Panel) colors) =>
        new("", colors.Bg, colors.Primary, colors.Secondary, colors.Panel);
}

/// <summary>The built-in palettes and animated backgrounds. An app can add its own palettes to All.</summary>
public static class Palettes
{
    /// <summary>The palette Find falls back to.</summary>
    public static string DefaultName { get; set; } = "Dark";

    public static readonly List<Palette> All = new()
    {
        new("Default", "#0a0713", "#341052", "#123a63", "#100c1a8c"),
        new("Light", "#ebecf0", "#cfd8ea", "#2f6fd6", "#ffffffb3"),
        new("Dark", "#2b2c30", "#3b3d44", "#2f6fd6", "#34363c8c"),
        new("Midnight", "#04060d", "#13215a", "#0a4a6e", "#080c1a8c"),
        new("Obsidian", "#0a0908", "#523312", "#7a5f22", "#13110e8c"),
        new("Amethyst", "#0b0612", "#3a1a66", "#6e1c52", "#140c1f8c"),
        new("Glacier", "#070c12", "#1d4159", "#4a7186", "#0d151d8c"),
        new("Bordeaux", "#0d0508", "#4c0e22", "#7c3f23", "#170b0f8c"),
        new("Jade", "#040c0a", "#0b3d32", "#3f6230", "#0a16138c"),
        new("Graphite", "#0b0c0f", "#22262e", "#3a5577", "#14161b8c"),
        new("Rose-pine", "#16141f", "#4a3663", "#8a3f59", "#1f1d2e8c"),
        new("Gruvbox", "#1d2021", "#7a2f24", "#2f5f57", "#2828288c"),
        new("catppuccin", "#181825", "#4c3a6b", "#33517f", "#1e1e2e8c"),
        new("Solarized Dark", "#00212b", "#6b3410", "#10537f", "#0736428c"),
        new("Solarized Light", "#3b3630", "#6e3a55", "#2f7a70", "#4a443a8c"),
        new("monokai", "#1e1f1c", "#7a1436", "#1f6b7a", "#2728228c"),
        new("Nord", "#232833", "#35506e", "#4a7a70", "#2e34408c"),
        new("Dracula", "#21222c", "#4a3a7d", "#8a3f6e", "#282a368c"),
        new("Tokyo Night", "#16161e", "#2f3b6b", "#5c3a7a", "#1a1b268c"),
        new("Synthwave", "#0d0620", "#6b1050", "#0e6b8a", "#140a2b8c"),
        new("Ember", "#140c08", "#7a2c0c", "#8a6b14", "#1c110b8c"),
        new("Abyss", "#030d14", "#0b3a4d", "#1a2f6b", "#06141e8c"),
        new("Verdant", "#0a120c", "#1d4a2b", "#5c7014", "#0e1a118c"),
        new("Noir", "#0f0f11", "#2b2b30", "#5a5a66", "#17171a8c"),
    };

    public static readonly string[] Backgrounds = { "Flow", "Mesh", "Silk", "Horizon", "Blobs", "Waves", "Aurora", "Bokeh", "Grid", "Gradient", "Solid" };

    public const string DefaultBackground = "Flow";

    public static IEnumerable<string> Names => All.Select(palette => palette.Name);

    /// <summary>The palette with this name, or the default palette when there is none.</summary>
    public static Palette Find(string name)
    {
        foreach (var palette in All)
        {
            if (palette.Name == name) { return palette; }
        }
        foreach (var palette in All)
        {
            if (palette.Name == DefaultName) { return palette; }
        }
        return All[0];
    }

    public static string BackgroundShaderPath(string name)
    {
        if (!Backgrounds.Contains(name)) { name = DefaultBackground; }
        return $"{FishPaths.Backgrounds}bg_{name.ToLowerInvariant()}.gdshader";
    }
}
