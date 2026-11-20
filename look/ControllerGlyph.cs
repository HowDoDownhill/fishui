using Godot;
using System;
using System.Collections.Generic;

namespace FishUI;

public static class ControllerGlyph
{
    private const int GlyphSize = 48;
    private const int GlyphSpacing = 10;
    private const int CombinedIconGap = 12;
    private const string IconAssetRoot = "res://addons/controller_icons/assets/";

    private static readonly Dictionary<int, string> NeutralIconPaths = new Dictionary<int, string>
    {
        { (int)JoyButton.A, "positional/south" },
        { (int)JoyButton.B, "positional/east" },
        { (int)JoyButton.X, "positional/west" },
        { (int)JoyButton.Y, "positional/north" },
        { (int)JoyButton.DpadUp, "xbox360/dpad_up" },
        { (int)JoyButton.DpadDown, "xbox360/dpad_down" },
        { (int)JoyButton.DpadLeft, "xbox360/dpad_left" },
        { (int)JoyButton.DpadRight, "xbox360/dpad_right" }
    };

    private static readonly Dictionary<int, string[]> NeutralCombinedIconPaths = new Dictionary<int, string[]>
    {
        { (int)JoyButton.Start, new[] { "xboxone/menu", "ps5/options", "switch/plus" } },
        { (int)JoyButton.Back, new[] { "xboxone/view", "ps5/share", "switch/minus" } }
    };

    private static readonly Dictionary<PadInput, string> ChordIconPaths = new Dictionary<PadInput, string>
    {
        { PadInput.LB, "xbox360/lb" },
        { PadInput.RB, "xbox360/rb" },
        { PadInput.LT, "xbox360/lt" },
        { PadInput.RT, "xbox360/rt" },
        { PadInput.L3, "xbox360/l_stick_click" },
        { PadInput.R3, "xbox360/r_stick_click" },
        { PadInput.Select, "xboxone/view" },
        { PadInput.Start, "xboxone/menu" }
    };

    private class GlyphButton
    {
        public WeakReference<Button> Button;
        public string ActionName;
        public PadInput[] Parts;
        public ControllerIconTexture Glyph;
    }

    private static readonly List<GlyphButton> glyphButtons = new List<GlyphButton>();
    private static readonly Dictionary<string, Texture2D> combinedIcons = new Dictionary<string, Texture2D>();
    private static bool showsNeutralPrompts;
    private static bool isFollowingLastController;

    public static ControllerIconTexture For(string actionName)
    {
        var glyph = new ControllerIconTexture();
        glyph.path = actionName;
        glyph.show_mode = ControllerIcons.EShowMode.CONTROLLER;
        return glyph;
    }

    public static void Apply(Button button, string actionName)
    {
        FollowLastController();

        var glyphButton = new GlyphButton
        {
            Button = new WeakReference<Button>(button),
            ActionName = actionName,
            Glyph = For(actionName)
        };

        Track(button, glyphButton);
    }

    /// <summary>Puts the glyphs for a chord's inputs on a button, side by side. They show while a controller is in use.</summary>
    public static void ApplyChord(Button button, params PadInput[] parts)
    {
        FollowLastController();
        Track(button, new GlyphButton { Button = new WeakReference<Button>(button), Parts = parts });
    }

    private static void Track(Button button, GlyphButton glyphButton)
    {
        button.ExpandIcon = false;
        button.IconAlignment = HorizontalAlignment.Left;
        button.AddThemeConstantOverride("h_separation", GlyphSpacing);
        glyphButtons.Add(glyphButton);
        TintIcon(button);
        ShowPrompt(glyphButton);

        if (!followsAppearance)
        {
            followsAppearance = true;
            Appearance.Changed += TintAllIcons;
        }
    }

    private static bool followsAppearance;

    // The glyphs are drawn white for dark screens; on a light palette they take the icon tint.
    private static void TintIcon(Button button)
    {
        Color tint = Appearance.IconTint;
        foreach (string state in new[] { "icon_normal_color", "icon_focus_color", "icon_hover_color", "icon_pressed_color", "icon_hover_pressed_color" })
        {
            button.AddThemeColorOverride(state, tint);
        }
        button.AddThemeColorOverride("icon_disabled_color", new Color(tint, 0.4f));
    }

    private static void TintAllIcons()
    {
        glyphButtons.RemoveAll(entry => !entry.Button.TryGetTarget(out Button button) || !GodotObject.IsInstanceValid(button));

        foreach (GlyphButton glyphButton in glyphButtons)
        {
            if (glyphButton.Button.TryGetTarget(out Button button))
            {
                TintIcon(button);
            }
        }
    }

    private static void FollowLastController()
    {
        if (isFollowingLastController || ControllerIcons.CI == null)
        {
            return;
        }

        isFollowingLastController = true;
        showsNeutralPrompts = ControllerIcons.CI.LastInputType == ControllerIcons.EInputType.CONTROLLER;
        ControllerIcons.CI.InputTypeChanged += (inputType, controller) => ShowNeutralPrompts(inputType == (int)ControllerIcons.EInputType.CONTROLLER);
    }

    private static void ShowNeutralPrompts(bool neutral)
    {
        if (neutral == showsNeutralPrompts)
        {
            return;
        }

        showsNeutralPrompts = neutral;
        glyphButtons.RemoveAll(entry => !entry.Button.TryGetTarget(out Button button) || !GodotObject.IsInstanceValid(button));

        foreach (GlyphButton glyphButton in glyphButtons)
        {
            ShowPrompt(glyphButton);
        }
    }

    private static void ShowPrompt(GlyphButton glyphButton)
    {
        if (!glyphButton.Button.TryGetTarget(out Button button) || !GodotObject.IsInstanceValid(button))
        {
            return;
        }

        if (glyphButton.Parts != null)
        {
            Texture2D chordIcon = showsNeutralPrompts ? LoadChordIcon(glyphButton.Parts) : null;
            button.Icon = chordIcon;
            if (chordIcon != null)
            {
                button.AddThemeConstantOverride("icon_max_width", chordIcon.GetWidth() * GlyphSize / chordIcon.GetHeight());
            }
            return;
        }

        int buttonIndex = showsNeutralPrompts ? ReadJoypadButton(glyphButton.ActionName) : -1;
        Texture2D combinedIcon = NeutralCombinedIconPaths.TryGetValue(buttonIndex, out string[] combinedPaths)
            ? LoadCombinedIcon(buttonIndex.ToString(), combinedPaths)
            : null;

        if (combinedIcon != null)
        {
            button.Icon = combinedIcon;
            button.AddThemeConstantOverride("icon_max_width", combinedIcon.GetWidth() * GlyphSize / combinedIcon.GetHeight());
            return;
        }

        glyphButton.Glyph.path = NeutralIconPaths.TryGetValue(buttonIndex, out string neutralPath) ? neutralPath : glyphButton.ActionName;
        glyphButton.Glyph.force_controller_icon_style = showsNeutralPrompts ? ControllerSettings.Devices.XBOX360 : ControllerSettings.Devices.NONE;
        button.Icon = glyphButton.Glyph;
        button.AddThemeConstantOverride("icon_max_width", GlyphSize);
    }

    private static int ReadJoypadButton(string actionName)
    {
        if (!InputMap.HasAction(actionName))
        {
            return -1;
        }

        foreach (InputEvent actionEvent in InputMap.ActionGetEvents(actionName))
        {
            if (actionEvent is InputEventJoypadButton joypadButton)
            {
                return (int)joypadButton.ButtonIndex;
            }
        }

        return -1;
    }

    private static Texture2D LoadChordIcon(PadInput[] parts)
    {
        var iconPaths = new List<string>();

        foreach (PadInput part in parts)
        {
            if (ChordIconPaths.TryGetValue(part, out string iconPath))
            {
                iconPaths.Add(iconPath);
            }
        }

        return LoadCombinedIcon(string.Join("+", iconPaths), iconPaths.ToArray(), true);
    }

    /// <param name="joinedByPlus">Draws a plus sign between the icons, for inputs pressed together.</param>
    private static Texture2D LoadCombinedIcon(string cacheKey, string[] iconPaths, bool joinedByPlus = false)
    {
        if (combinedIcons.TryGetValue(cacheKey, out Texture2D cachedIcon))
        {
            return cachedIcon;
        }

        var images = new List<Image>();

        foreach (string iconPath in iconPaths)
        {
            Image image = GD.Load<Texture2D>(IconAssetRoot + iconPath + ".png")?.GetImage();

            if (image == null)
            {
                continue;
            }

            if (image.IsCompressed())
            {
                image.Decompress();
            }

            image.Convert(Image.Format.Rgba8);
            images.Add(image);
        }

        if (images.Count == 0)
        {
            return null;
        }

        int iconHeight = images[0].GetHeight();

        foreach (Image image in images)
        {
            image.Resize(image.GetWidth() * iconHeight / image.GetHeight(), iconHeight);
        }

        if (joinedByPlus)
        {
            for (int index = images.Count - 1; index > 0; index--)
            {
                images.Insert(index, DrawPlus(iconHeight));
            }
        }

        int combinedWidth = CombinedIconGap * (images.Count - 1);

        foreach (Image image in images)
        {
            combinedWidth += image.GetWidth();
        }

        Image combinedImage = Image.CreateEmpty(combinedWidth, iconHeight, false, Image.Format.Rgba8);
        int offset = 0;

        foreach (Image image in images)
        {
            combinedImage.BlitRect(image, new Rect2I(0, 0, image.GetWidth(), image.GetHeight()), new Vector2I(offset, 0));
            offset += image.GetWidth() + CombinedIconGap;
        }

        Texture2D combinedIcon = ImageTexture.CreateFromImage(combinedImage);
        combinedIcons[cacheKey] = combinedIcon;
        return combinedIcon;
    }

    // A white plus sign as tall as the icons it sits between, so the button's tint colours it with them.
    private static Image DrawPlus(int iconHeight)
    {
        int armLength = iconHeight * 3 / 10;
        int thickness = Math.Max(2, iconHeight / 14);
        Image plus = Image.CreateEmpty(armLength, iconHeight, false, Image.Format.Rgba8);
        plus.FillRect(new Rect2I(0, (iconHeight - thickness) / 2, armLength, thickness), Colors.White);
        plus.FillRect(new Rect2I((armLength - thickness) / 2, (iconHeight - armLength) / 2, thickness, armLength), Colors.White);
        return plus;
    }
}
