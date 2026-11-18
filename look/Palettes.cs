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
        new("Dark", "#2b2c30", "#3b3d44", "#2f6fd6", "#34363c8c"),
        new("Light", "#ebecf0", "#dde4f2", "#a9c2ee", "#ffffffb3"),
        new("Noir", "#0f0f11", "#2b2b30", "#5a5a66", "#17171a8c"),
        new("Midnight", "#04060d", "#13215a", "#0a4a6e", "#080c1a8c"),
        new("Amethyst", "#0b0612", "#3a1a66", "#6e1c52", "#140c1f8c"),
        new("Jade", "#040c0a", "#0b3d32", "#3f6230", "#0a16138c"),
        new("Ember", "#140c08", "#7a2c0c", "#8a6b14", "#1c110b8c"),
        new("Nord", "#232833", "#35506e", "#4a7a70", "#2e34408c"),
        new("Synthwave", "#0d0620", "#6b1050", "#0e6b8a", "#140a2b8c"),
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
