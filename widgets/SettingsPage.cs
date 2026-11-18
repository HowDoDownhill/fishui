using System;
using System.Collections.Generic;
using Godot;

namespace FishUI;

/// <summary>
/// A settings layout: sections in a panel on the left, the focused section's rows on the right,
/// and a help line under them for the focused row. Add it to a Screen, add sections, then call
/// Open. The screen forwards FocusDefault and OnBack to it.
/// </summary>
public partial class SettingsPage : HBoxContainer
{
    private readonly List<(string Name, Func<List<SettingRow>> Build)> _sections = new();
    private readonly List<Button> _sectionButtons = new();
    private readonly List<SettingRow> _rows = new();
    private VBoxContainer _sectionList;
    private VBoxContainer _list;
    private Label _help;
    private int _section = -1;

    /// <summary>The section showing, so a screen can reopen on it.</summary>
    public int Section => _section;

    public SettingsPage()
    {
        Ui.Fill(this);
        AddThemeConstantOverride("separation", 0);

        _sectionList = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
        _sectionList.AddThemeConstantOverride("separation", 5);
        var sectionPanel = Ui.Pad(Ui.Panel(_sectionList, 10, 12), Ui.PanelGap, Ui.PanelGap, 0, 0);
        sectionPanel.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        AddChild(sectionPanel);

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 8);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(_list);
        FocusScroller.KeepVisible(scroll, _list, 70, vertical: true);
        right.AddChild(scroll);
        _help = Ui.Label("", 22, variation: "CaptionLabel");
        _help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _help.CustomMinimumSize = new Vector2(0, 60);
        right.AddChild(Ui.Pad(_help, 20, 0, 20, 0));
        var optionPanel = Ui.Panel(right, 10, 12);
        optionPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(Ui.Pad(optionPanel, Ui.PanelGap, Ui.PanelGap, Ui.PanelGap, 0));
    }

    /// <summary>Adds a section. Its rows are built each time the section is shown or rebuilt.</summary>
    public void AddSection(string name, Func<List<SettingRow>> build)
    {
        var index = _sections.Count;
        _sections.Add((name, build));
        var button = Ui.Button(name);
        button.ThemeTypeVariation = "TitleButton";
        button.Alignment = HorizontalAlignment.Left;
        button.FocusEntered += () => ShowSection(index);
        _sectionList.AddChild(button);
        _sectionButtons.Add(button);
    }

    /// <summary>Call after the last AddSection, with the page in the tree. Shows a section without taking focus.</summary>
    public void Open(int section = 0)
    {
        if (_sections.Count == 0) { return; }
        Ui.ConfineVertical(_sectionButtons);
        foreach (var button in _sectionButtons) { button.FocusNeighborLeft = new NodePath(); }
        ShowSection(Math.Clamp(section, 0, _sections.Count - 1));
    }

    /// <summary>Focuses the open section's button.</summary>
    public void FocusDefault()
    {
        if (_sectionButtons.Count > 0) { _sectionButtons[Math.Max(0, _section)].GrabFocus(); }
    }

    /// <summary>B from a row returns to the section list and reports true. B from the list reports false.</summary>
    public bool OnBack()
    {
        if (GetViewport().GuiGetFocusOwner() is not SettingRow || _section < 0) { return false; }
        _sectionButtons[_section].GrabFocus();
        return true;
    }

    private void ShowSection(int index)
    {
        if (index == _section) { return; }
        _section = index;
        Rebuild();
    }

    /// <summary>Builds the current section's rows again, keeping focus on the same row.</summary>
    public void Rebuild()
    {
        if (_section < 0) { return; }
        var focused = _rows.IndexOf(GetViewport()?.GuiGetFocusOwner() as SettingRow);
        foreach (var child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }
        _rows.Clear();
        _rows.AddRange(_sections[_section].Build());
        _help.Text = "";
        MarkSection();

        var sectionButton = _sectionButtons[_section];
        foreach (var row in _rows)
        {
            _list.AddChild(row);
            row.FocusEntered += () => _help.Text = row.Help ?? "";
        }
        // Up and Down run through the rows and stop at the ends. Left from a row without choices
        // returns to the section list.
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            row.FocusNeighborTop = _rows[Math.Max(0, i - 1)].GetPath();
            row.FocusNeighborBottom = _rows[Math.Min(_rows.Count - 1, i + 1)].GetPath();
            row.FocusNeighborRight = row.GetPath();
            row.FocusNeighborLeft = row.HasChoices ? row.GetPath() : sectionButton.GetPath();
        }
        foreach (var button in _sectionButtons)
        {
            button.FocusNeighborRight = _rows.Count > 0 ? _rows[0].GetPath() : button.GetPath();
        }
        if (focused >= 0 && _rows.Count > 0) { _rows[Math.Min(focused, _rows.Count - 1)].GrabFocus(); }
    }

    /// <summary>
    /// Gives the open section an accent box, so it stays marked while focus is among its options.
    /// The gliding highlight shows where focus is. The box shows which section the options belong to.
    /// Call it again after the palette changes.
    /// </summary>
    public void MarkSection()
    {
        for (var i = 0; i < _sectionButtons.Count; i++)
        {
            if (i == _section) { _sectionButtons[i].AddThemeStyleboxOverride("normal", Ui.Box(Ui.AccentDim, 10, padX: 18, padY: 8)); }
            else { _sectionButtons[i].RemoveThemeStyleboxOverride("normal"); }
        }
    }
}
