using Godot;

namespace FishUI;

/// <summary>
/// Input actions. Godot's built-in ui_accept and ui_cancel hold only keyboard keys, so A and B are
/// added to them here. The D-pad and left stick already drive ui_left/right/up/down.
/// </summary>
public static class Actions
{
    public const string Accept = "ui_accept";
    public const string Back = "ui_cancel";
    public const string X = "fish_x";
    public const string Y = "fish_y";
    public const string Lb = "fish_lb";
    public const string Rb = "fish_rb";
    public const string Start = "fish_start";
    public const string Fullscreen = "fish_fullscreen";

    /// <summary>Adds the actions to the input map. AppShell calls this; calling it again does nothing.</summary>
    public static void Register()
    {
        if (InputMap.HasAction(X)) { return; }
        InputMap.ActionAddEvent(Accept, new InputEventJoypadButton { ButtonIndex = JoyButton.A, Device = -1 });
        InputMap.ActionAddEvent(Back, new InputEventJoypadButton { ButtonIndex = JoyButton.B, Device = -1 });
        Add(X, JoyButton.X, Key.F2);
        Add(Y, JoyButton.Y, Key.F3);
        Add(Lb, JoyButton.LeftShoulder, Key.Pageup);
        Add(Rb, JoyButton.RightShoulder, Key.Pagedown);
        Add(Start, JoyButton.Start, Key.F1);
        Add(Fullscreen, null, Key.F11);
    }

    /// <summary>Adds an app's own action with a controller button, a key, or both.</summary>
    public static void Add(string name, JoyButton? button, Key? key)
    {
        if (!InputMap.HasAction(name)) { InputMap.AddAction(name); }
        if (button != null) { InputMap.ActionAddEvent(name, new InputEventJoypadButton { ButtonIndex = button.Value, Device = -1 }); }
        if (key != null) { InputMap.ActionAddEvent(name, new InputEventKey { PhysicalKeycode = key.Value }); }
    }
}
