using Godot;
using System;
using System.Collections.Generic;

namespace FishUI;

// Plays the interface sounds: a tick when the selection moves, rising and falling notes for
// Select and Back, and a sweep when the page changes. A move only sounds shortly after the
// player pressed something, so focus changes made by code at startup or on a reload stay quiet.
public partial class UiSounds : Node
{
    public enum Sound { Move, Select, Back, Switch }

    private const float VolumeDb = -8.0f;
    private const ulong UserInputWindowMilliseconds = 400;
    private const ulong MinimumMoveGapMilliseconds = 35;
    // After Select, Back or Switch, the focus change that follows would tick over the first sound.
    private const ulong QuietAfterActionMilliseconds = 200;

    private static readonly Dictionary<Sound, string> Paths = new Dictionary<Sound, string>
    {
        { Sound.Move, FishPaths.Sounds + "move.wav" },
        { Sound.Select, FishPaths.Sounds + "select.wav" },
        { Sound.Back, FishPaths.Sounds + "back.wav" },
        { Sound.Switch, FishPaths.Sounds + "switch.wav" }
    };

    private static UiSounds instance;

    // The user's setting. An app sets it from its saved settings.
    public static bool Enabled { get; set; } = true;

    // Silences the interface for a while, such as while a video is playing.
    public static bool Muted { get; set; }

    // Input actions that sound Select and Back. An app with its own actions adds them here.
    public static readonly List<string> SelectActions = new List<string> { "ui_accept" };
    public static readonly List<string> BackActions = new List<string> { "ui_cancel" };

    // Returns true while input belongs to something else, such as a game this app started.
    public static Func<bool> InputBelongsElsewhere { get; set; }

    private readonly Dictionary<Sound, AudioStreamPlayer> players = new Dictionary<Sound, AudioStreamPlayer>();
    private ulong lastInputAt;
    private ulong lastMoveAt;
    private ulong lastActionAt;

    public UiSounds()
    {
        Name = "UiSounds";
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Ready()
    {
        instance = this;

        foreach (var entry in Paths)
        {
            var player = new AudioStreamPlayer { Stream = GD.Load<AudioStream>(entry.Value), VolumeDb = VolumeDb };
            AddChild(player);
            players[entry.Key] = player;
        }

        // The window sees every event before any node can mark it handled.
        GetTree().Root.WindowInput += OnWindowInput;
        GetViewport().GuiFocusChanged += _ => Play(Sound.Move);
    }

    public override void _ExitTree()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static void Play(Sound sound)
    {
        instance?.PlaySound(sound);
    }

    private void OnWindowInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseMotion || !inputEvent.IsPressed() || IsInputMeantForAnotherProgram())
        {
            return;
        }

        lastInputAt = Time.GetTicksMsec();

        if (inputEvent.IsEcho())
        {
            return;
        }

        if (IsAnyPressed(inputEvent, SelectActions))
        {
            PlaySound(Sound.Select);
        }
        else if (IsAnyPressed(inputEvent, BackActions))
        {
            PlaySound(Sound.Back);
        }
    }

    private static bool IsAnyPressed(InputEvent inputEvent, List<string> actions)
    {
        foreach (string action in actions)
        {
            if (InputMap.HasAction(action) && inputEvent.IsActionPressed(action))
            {
                return true;
            }
        }

        return false;
    }

    // Controller presses still arrive while another program is in front.
    private bool IsInputMeantForAnotherProgram()
    {
        return InputBelongsElsewhere?.Invoke() == true || !GetWindow().HasFocus();
    }

    private void PlaySound(Sound sound)
    {
        if (!Enabled || Muted || !players.TryGetValue(sound, out AudioStreamPlayer player))
        {
            return;
        }

        ulong now = Time.GetTicksMsec();

        if (sound == Sound.Move)
        {
            if (now - lastInputAt > UserInputWindowMilliseconds || now - lastActionAt < QuietAfterActionMilliseconds || now - lastMoveAt < MinimumMoveGapMilliseconds)
            {
                return;
            }

            lastMoveAt = now;
        }
        else
        {
            if (now - lastInputAt > UserInputWindowMilliseconds)
            {
                return;
            }

            lastActionAt = now;
        }

        player.Play();
    }
}
