using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace FishUI;

/// <summary>One controller input that can be part of a chord: a button, or a trigger pulled past halfway.</summary>
public readonly record struct PadInput(JoyButton? Button, JoyAxis? Axis)
{
    public static readonly PadInput LB = new(JoyButton.LeftShoulder, null);
    public static readonly PadInput RB = new(JoyButton.RightShoulder, null);
    public static readonly PadInput LT = new(null, JoyAxis.TriggerLeft);
    public static readonly PadInput RT = new(null, JoyAxis.TriggerRight);
    public static readonly PadInput L3 = new(JoyButton.LeftStick, null);
    public static readonly PadInput R3 = new(JoyButton.RightStick, null);
    public static readonly PadInput Select = new(JoyButton.Back, null);
    public static readonly PadInput Start = new(JoyButton.Start, null);
}

/// <summary>
/// Watches for chords: two or more controller inputs pressed together, such as LB and RB. A chord
/// runs its action once and the presses that made it go no further. AppShell has one. An app with
/// its own scenes adds one as the last child of its root, so it sees input first.
/// </summary>
public partial class ChordWatcher : Node
{
    private sealed record Chord(PadInput[] Parts, Key? Key, Action Action, string Label = null, Func<bool> Shown = null);

    private const float TriggerDown = 0.5f;

    private readonly List<Chord> _chords = new();
    private readonly Dictionary<PadInput, double> _downSince = new();
    private readonly HashSet<PadInput> _used = new();
    private readonly List<(PadInput Part, InputEvent Event, double At)> _heldBack = new();
    private bool _replaying;

    /// <summary>How far apart the presses of a chord may land, in seconds.</summary>
    public double Window = 0.3;

    /// <summary>
    /// Whether a press that could start a chord waits for the window to pass before it reaches
    /// the app. Turn this on, with a short window, when the chord's buttons act the moment they
    /// go down; a chord then never triggers the single-button action first. Leave it off when
    /// those buttons act on release.
    /// </summary>
    public bool HoldsBackSingles;

    /// <summary>Checked before a chord runs. Null means always.</summary>
    public Func<bool> Enabled;

    /// <param name="key">A keyboard key that runs the same action, or null.</param>
    public void Add(Action action, Key? key, params PadInput[] parts) => _chords.Add(new Chord(parts, key, action));

    /// <summary>Adds a chord that has a footer prompt.</summary>
    /// <param name="label">The prompt's words, such as "Sidebar".</param>
    /// <param name="shown">Whether the prompt shows right now. Null means always.</param>
    public void Add(string label, Func<bool> shown, Action action, Key? key, params PadInput[] parts) =>
        _chords.Add(new Chord(parts, key, action, label, shown));

    /// <summary>A footer prompt for each named chord that is available now, in the order added.</summary>
    public IEnumerable<Button> BuildPrompts()
    {
        if (Enabled != null && !Enabled()) { yield break; }
        foreach (var chord in _chords)
        {
            if (chord.Label == null || (chord.Shown != null && !chord.Shown())) { continue; }
            yield return Ui.ChordHint(chord.Label, () => { if (Enabled == null || Enabled()) { chord.Action(); } }, chord.Parts);
        }
    }

    public override void _Input(InputEvent e)
    {
        if (_replaying || (Enabled != null && !Enabled())) { return; }

        if (e is InputEventKey { Pressed: true, Echo: false } pressedKey)
        {
            var byKey = _chords.FirstOrDefault(chord => chord.Key == pressedKey.PhysicalKeycode);
            if (byKey == null) { return; }
            GetViewport().SetInputAsHandled();
            byKey.Action();
            return;
        }

        if (!TryRead(e, out var part, out var pressed) || !_chords.Any(chord => chord.Parts.Contains(part))) { return; }
        var now = Time.GetTicksMsec() / 1000.0;

        if (!pressed)
        {
            _downSince.Remove(part);
            if (_used.Remove(part)) { GetViewport().SetInputAsHandled(); }
            else { Replay(part); }
            return;
        }

        _downSince[part] = now;
        var made = _chords.FirstOrDefault(chord => chord.Parts.All(p => _downSince.TryGetValue(p, out var at) && now - at <= Window));
        if (made != null)
        {
            GetViewport().SetInputAsHandled();
            foreach (var p in made.Parts) { _used.Add(p); }
            _heldBack.RemoveAll(held => made.Parts.Contains(held.Part));
            made.Action();
        }
        else if (HoldsBackSingles)
        {
            GetViewport().SetInputAsHandled();
            _heldBack.Add((part, e, now));
        }
    }

    public override void _Process(double delta)
    {
        if (_heldBack.Count == 0) { return; }
        var now = Time.GetTicksMsec() / 1000.0;
        foreach (var part in _heldBack.Where(held => now - held.At > Window).Select(held => held.Part).ToArray()) { Replay(part); }
    }

    /// <summary>Sends on a press that turned out to be a single press.</summary>
    private void Replay(PadInput part)
    {
        var index = _heldBack.FindIndex(held => held.Part == part);
        if (index < 0) { return; }
        var e = _heldBack[index].Event;
        _heldBack.RemoveAt(index);
        _replaying = true;
        Input.ParseInputEvent(e);
        Input.FlushBufferedEvents();
        _replaying = false;
    }

    private bool TryRead(InputEvent e, out PadInput part, out bool pressed)
    {
        part = default;
        pressed = false;
        if (e is InputEventJoypadButton button)
        {
            part = new PadInput(button.ButtonIndex, null);
            pressed = button.Pressed;
            return true;
        }
        if (e is InputEventJoypadMotion { Axis: JoyAxis.TriggerLeft or JoyAxis.TriggerRight } motion)
        {
            part = new PadInput(null, motion.Axis);
            pressed = motion.AxisValue >= TriggerDown;
            // A trigger sends a stream of values. Only crossing the halfway point counts. The
            // values between belong to a press that is part of a chord or still held back.
            if (pressed != (_downSince.ContainsKey(part) || _used.Contains(part))) { return true; }
            var claimed = _used.Contains(part);
            foreach (var held in _heldBack) { claimed |= held.Part == part; }
            if (claimed) { GetViewport().SetInputAsHandled(); }
            return false;
        }
        return false;
    }
}
