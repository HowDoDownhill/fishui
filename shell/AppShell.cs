using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace FishUI;

/// <summary>One page in the navigation sidebar.</summary>
public sealed record ShellTab(string Id, string Label, Func<Screen> Create);

/// <summary>
/// The root node of a controller-first app: the background, the screen stack, modal overlays,
/// input routing, the top bar, sidebar and footer prompts, and toasts. An app subclasses it, puts
/// the subclass on its main scene's root Control, and overrides the hooks it needs.
/// </summary>
public partial class AppShell : Control
{
    public static AppShell I { get; private set; }

    private readonly List<Screen> _stack = new();
    private readonly List<Overlay> _overlays = new();
    private readonly List<Button> _tabButtons = new();
    private IReadOnlyList<ShellTab> _tabs = Array.Empty<ShellTab>();
    private Control _screenHost;
    private Control _overlayHost;
    private HBoxContainer _hints;
    private Control _footer;
    private Control _sidebar;
    private Control _topBar;
    private Label _appTitle;
    private Label _pageTitle;
    private Label _pageInfo;
    private Label _clock;
    private double _clockIn;
    private string _clockFormat = "system";
    private PanelContainer _toast;
    private Label _toastLabel;
    private Tween _toastTween;

    // A shoulder button held this long goes to the screen. A shorter press switches sidebar page.
    private const double ShoulderHoldSeconds = 0.4;
    private string _shoulder;
    private double _shoulderFor;

    // Held D-pad or stick repeats, since Godot only repeats keyboard keys.
    private string _heldAction;
    private double _heldFor;
    private double _repeatIn;

    public Screen Current => _stack.Count > 0 ? _stack[^1] : null;
    public int StackDepth => _stack.Count;
    public Overlay TopOverlay => _overlays.Count > 0 ? _overlays[^1] : null;

    // ---- Hooks for the app ----

    /// <summary>The pages in the navigation sidebar, in order. Empty leaves the sidebar out.</summary>
    protected virtual IReadOnlyList<ShellTab> Tabs => Array.Empty<ShellTab>();

    /// <summary>Applies the style, palette and background from the app's saved settings.</summary>
    protected virtual void ApplyAppearance() =>
        Appearance.Apply(Appearance.GlassStyle, Palettes.DefaultBackground, Palettes.Find(null));

    /// <summary>The saved interface size: "Auto" or a percentage such as "115%".</summary>
    protected virtual string InterfaceSize => InterfaceScale.Automatic;

    /// <summary>Whether the window starts fullscreen. "-- --windowed" on the command line overrides it.</summary>
    protected virtual bool StartFullscreen => true;

    /// <summary>The window size when not fullscreen.</summary>
    protected virtual Vector2I WindowedSize => new(1600, 900);

    /// <summary>Called after the user toggles fullscreen, so the app can save the choice.</summary>
    protected virtual void OnFullscreenChanged(bool fullscreen) { }

    /// <summary>The name at the left of the top bar. Read again on every chrome refresh.</summary>
    protected virtual string TopBarTitle => ProjectSettings.GetSetting("application/config/name").AsString();

    /// <summary>Whether the top bar shows for a screen that allows it.</summary>
    protected virtual bool TopBarAvailable => true;

    /// <summary>Adds controls before the title in the top bar, such as a logo.</summary>
    protected virtual void BuildTopBarStart(HBoxContainer row) { }

    /// <summary>Adds controls between the page status and the clock, such as a user name.</summary>
    protected virtual void BuildTopBarEnd(HBoxContainer row) { }

    /// <summary>Called after the top bar, sidebar and footer were updated for a screen.</summary>
    protected virtual void OnChromeRefreshed(Screen screen) { }

    /// <summary>Called once the shell is built. The app shows its first screen here.</summary>
    protected virtual Task StartAsync(string[] args) => Task.CompletedTask;

    // ---- Setup ----

    public override void _EnterTree()
    {
        I = this;
        Actions.Register();
    }

    public override void _ExitTree()
    {
        if (I == this) { I = null; }
        SelectionHighlighter.ReleaseAll();
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ApplyAppearance();
        InterfaceScale.Apply(GetTree().Root, InterfaceSize);
        SelectionHighlighter.Attach(GetViewport());
        AddChild(new UiSounds());

        // Closing the window goes through QuitAsync, so the top screen can save its state first.
        GetTree().AutoAcceptQuit = false;

        var args = OS.GetCmdlineUserArgs();
        if (StartFullscreen && !args.Contains("--windowed")) { DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen); }
        else { DisplayServer.WindowSetSize(WindowedSize); }

        var background = new ColorRect { Material = Appearance.BackgroundMaterial, MouseFilter = MouseFilterEnum.Ignore };
        Ui.Fill(background);
        AddChild(background);

        var layout = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        layout.AddThemeConstantOverride("separation", 0);
        Ui.Fill(layout);
        AddChild(layout);

        BuildTopBar(layout);

        var body = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 0);
        layout.AddChild(body);
        BuildSidebar(body);

        _screenHost = new Control { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(_screenHost);

        BuildFooter(layout);

        _overlayHost = new Control { MouseFilter = MouseFilterEnum.Ignore };
        Ui.Fill(_overlayHost);
        AddChild(_overlayHost);

        BuildToast();
        RunDeveloperArguments(args);
        _ = StartAsync(args);
    }

    /// <summary>
    /// Developer aids, passed after "--" on the command line. "--shot &lt;png&gt; [seconds]" saves a
    /// screenshot after a delay and exits. "--keys a,b,c" presses input actions in order:
    /// "hold:action" holds one down, "joy:N" presses controller button N, "quit" exits.
    /// </summary>
    private void RunDeveloperArguments(string[] args)
    {
        var shot = Array.IndexOf(args, "--shot");
        if (shot >= 0 && shot + 1 < args.Length)
        {
            var wait = shot + 2 < args.Length && double.TryParse(args[shot + 2], out var seconds) ? seconds : 4;
            GetTree().CreateTimer(wait).Timeout += () =>
            {
                GetViewport().GetTexture().GetImage().SavePng(args[shot + 1]);
                _ = QuitAsync();
            };
        }

        var keys = Array.IndexOf(args, "--keys");
        if (keys >= 0 && keys + 1 < args.Length) { _ = PressKeysAsync(args[keys + 1].Split(',')); }
    }

    private async Task PressKeysAsync(string[] actions)
    {
        await Task.Delay(2500);
        foreach (var entry in actions)
        {
            var action = entry.Trim();
            if (action == "quit")
            {
                await QuitAsync();
                return;
            }
            if (action.StartsWith("hold:"))
            {
                Input.ParseInputEvent(new InputEventAction { Action = action[5..], Pressed = true });
                await Task.Delay(700);
                Input.ParseInputEvent(new InputEventAction { Action = action[5..], Pressed = false });
            }
            else if (action.StartsWith("joy:"))
            {
                // A real controller button, so prompts switch to controller glyphs.
                var button = (JoyButton)int.Parse(action[4..]);
                Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true });
                Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false });
            }
            else
            {
                Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true });
                Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
            }
            await Task.Delay(700);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) { _ = QuitAsync(); }
    }

    /// <summary>Exits the app, after the top screen has had the chance to save its state.</summary>
    public virtual async Task QuitAsync()
    {
        var screen = Current;
        if (screen != null) { await screen.OnQuitAsync(); }
        GetTree().Quit();
    }

    // ---- Navigation ----

    public void Push(Screen screen)
    {
        var previous = Current;
        if (previous != null)
        {
            previous.SaveFocus();
            previous.Visible = false;
        }
        _stack.Add(screen);
        Show(screen);
    }

    public void Pop()
    {
        if (_stack.Count <= 1) { return; }
        var top = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        top.QueueFree();
        var screen = Current;
        screen.Visible = true;
        RefreshChrome();
        screen.RestoreFocus();
        screen.OnResume();
    }

    /// <summary>Swaps the top screen for another.</summary>
    public void Replace(Screen screen)
    {
        if (_stack.Count > 0)
        {
            _stack[^1].QueueFree();
            _stack.RemoveAt(_stack.Count - 1);
        }
        _stack.Add(screen);
        Show(screen);
    }

    /// <summary>Closes every screen and overlay and shows this screen alone.</summary>
    public void ResetTo(Screen screen)
    {
        foreach (var overlay in _overlays.ToArray()) { CloseOverlay(overlay); }
        foreach (var old in _stack) { old.QueueFree(); }
        _stack.Clear();
        _stack.Add(screen);
        Show(screen);
    }

    private void Show(Screen screen)
    {
        _screenHost.AddChild(screen);
        screen.Modulate = new Color(1, 1, 1, 0);
        screen.CreateTween().TweenProperty(screen, "modulate:a", 1f, 0.15);
        RefreshChrome();
        screen.OnEnter();
        screen.FocusDefault();
    }

    /// <summary>B: the screen may handle it, a pushed screen closes, and a sidebar page gives focus to the sidebar.</summary>
    public void Back()
    {
        var screen = Current;
        if (screen == null || screen.OnBack()) { return; }
        if (_stack.Count > 1) { Pop(); }
        else if (screen.Tab != null) { FocusSidebar(); }
    }

    // ---- Overlays ----

    public void OpenOverlay(Overlay overlay)
    {
        overlay.PreviousFocus = GetViewport().GuiGetFocusOwner();
        _overlays.Add(overlay);
        _overlayHost.AddChild(overlay);
        overlay.Open();
        overlay.FocusDefault();
    }

    public void CloseOverlay(Overlay overlay)
    {
        if (!_overlays.Remove(overlay)) { return; }
        overlay.Closed += overlay.QueueFree;
        overlay.Close();
        var focus = overlay.PreviousFocus;
        if (focus != null && IsInstanceValid(focus) && focus.IsInsideTree() && focus.IsVisibleInTree()) { focus.GrabFocus(); }
        else if (TopOverlay != null) { TopOverlay.FocusDefault(); }
        else { GetViewport().GuiReleaseFocus(); Current?.RestoreFocus(); }
        // Last, because code waiting on the overlay's answer may open the next overlay right away,
        // and that one must take focus after this one has given it back.
        overlay.OnDismissed();
    }

    /// <summary>Opens a grid of tiles for picking one of many.</summary>
    public void ShowJump(string title, IReadOnlyList<(string Label, string Caption)> tiles, int selected, Action<int> chosen) =>
        OpenOverlay(new JumpOverlay(title, tiles, selected, chosen));

    public void ShowMenu(string title, IReadOnlyList<(string Label, Action Action)> items, int selected = 0) =>
        OpenOverlay(new MenuOverlay(title, items, selected));

    /// <summary>Shows a menu and completes with the index chosen, or -1 when it is dismissed.</summary>
    /// <param name="body">Optional text explaining the question, shown above the choices.</param>
    public Task<int> ChooseAsync(string title, IReadOnlyList<string> options, string body = null)
    {
        var result = new TaskCompletionSource<int>();
        var items = options.Select((label, index) => (label, (Action)(() => result.TrySetResult(index)))).ToList();
        OpenOverlay(new MenuOverlay(title, items, 0, () => result.TrySetResult(-1), body));
        return result.Task;
    }

    /// <summary>Asks for text with the on-screen keyboard. Null when cancelled.</summary>
    public Task<string> PromptAsync(string title, string initial = "", bool secret = false)
    {
        var prompt = new TextPrompt(title, initial, secret);
        OpenOverlay(prompt);
        return prompt.Result;
    }

    // ---- Chrome ----

    /// <summary>The bar across the top: the app's title, the page, its status, and the clock.</summary>
    private void BuildTopBar(Control layout)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);

        BuildTopBarStart(row);
        _appTitle = Ui.Label("", variation: "DisplayLabel");
        row.AddChild(_appTitle);
        _pageTitle = Ui.Label("", variation: "TitleLabel");
        _pageTitle.Modulate = new Color(1, 1, 1, 0.65f);
        row.AddChild(_pageTitle);
        row.AddChild(Ui.Spacer(expand: true));
        _pageInfo = Ui.Label("", variation: "CaptionLabel");
        row.AddChild(_pageInfo);
        BuildTopBarEnd(row);
        _clock = Ui.Label("", variation: "TitleLabel");
        row.AddChild(_clock);

        _topBar = Ui.Pad(Ui.MicaPanel(row, 20, 8), Ui.PanelGap, Ui.PanelGap, Ui.PanelGap, 0);
        _topBar.Visible = false;
        layout.AddChild(_topBar);
    }

    private void RefreshTopBar(Screen screen)
    {
        _topBar.Visible = screen is { ShowChrome: true, ShowTopBar: true } && TopBarAvailable;
        if (!_topBar.Visible) { return; }
        _appTitle.Text = TopBarTitle ?? "";
        // A sidebar page is already marked in the sidebar, so only other screens are named here.
        _pageTitle.Text = screen.Tab == null ? screen.Title ?? "" : "";
        _pageInfo.Text = screen.Info ?? "";
    }

    /// <summary>The top bar clock: "system" for the system's format, "12", "24", or "off".</summary>
    public string ClockFormat
    {
        get => _clockFormat;
        set
        {
            _clockFormat = value;
            RefreshClock();
        }
    }

    private void RefreshClock()
    {
        if (_clock == null) { return; }
        var format = _clockFormat switch { "12" => "h:mm tt", "24" => "HH:mm", _ => "t" };
        _clock.Visible = _clockFormat != "off";
        _clock.Text = DateTime.Now.ToString(format);
    }

    /// <summary>Updates the top bar's status text when the current screen changes it.</summary>
    public void RefreshInfo(Screen screen)
    {
        if (screen == Current && _pageInfo != null) { _pageInfo.Text = screen.Info ?? ""; }
    }

    private void BuildSidebar(Control body)
    {
        _tabs = Tabs ?? Array.Empty<ShellTab>();
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(250, 0) };
        column.AddThemeConstantOverride("separation", 5);
        foreach (var tab in _tabs)
        {
            var button = Ui.Button(tab.Label, () => SwitchTab(tab.Id));
            button.ThemeTypeVariation = "TitleButton";
            button.Alignment = HorizontalAlignment.Left;
            column.AddChild(button);
            _tabButtons.Add(button);
        }
        _sidebar = Ui.Pad(Ui.MicaPanel(column, 10, 12), Ui.PanelGap, Ui.PanelGap, 0, 0);
        _sidebar.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _sidebar.Visible = false;
        body.AddChild(_sidebar);

        // Up and down stay inside the sidebar. Right is left to Godot, which finds the page content.
        Ui.ConfineVertical(_tabButtons);
        foreach (var button in _tabButtons) { button.FocusNeighborRight = new NodePath(); }
    }

    private int TabIndex(string id) => id == null ? -1 : _tabs.ToList().FindIndex(tab => tab.Id == id);

    /// <summary>Opens a sidebar page. Choosing the page already showing moves focus into it.</summary>
    public void SwitchTab(string id)
    {
        if (Current?.Tab == id && _stack.Count == 1)
        {
            Current.FocusDefault();
            return;
        }
        var index = TabIndex(id);
        if (index >= 0) { ResetTo(_tabs[index].Create()); }
    }

    private void CycleTab(int direction)
    {
        var index = TabIndex(Current?.Tab);
        if (index < 0) { return; }
        UiSounds.Play(UiSounds.Sound.Switch);
        SwitchTab(_tabs[(index + direction + _tabs.Count) % _tabs.Count].Id);
    }

    public void FocusSidebar()
    {
        var index = TabIndex(Current?.Tab);
        if (index >= 0) { _tabButtons[index].GrabFocus(); }
    }

    private void BuildFooter(Control layout)
    {
        _hints = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End, MouseFilter = MouseFilterEnum.Ignore };
        _hints.AddThemeConstantOverride("separation", 32);
        _footer = Ui.Pad(Ui.MicaPanel(_hints, 24, 8), Ui.PanelGap, Ui.PanelGap, Ui.PanelGap, Ui.PanelGap);
        layout.AddChild(_footer);
    }

    /// <summary>Updates the top bar and sidebar and rebuilds the footer prompts from the current screen.</summary>
    public void RefreshChrome()
    {
        var screen = Current;
        _footer.Visible = screen?.ShowChrome ?? false;
        RefreshTopBar(screen);
        var tabIndex = TabIndex(screen?.Tab);
        _sidebar.Visible = tabIndex >= 0;
        for (var i = 0; i < _tabButtons.Count; i++)
        {
            // The page being shown keeps an accent box. Focus is the gliding highlight on top of it.
            if (i == tabIndex) { _tabButtons[i].AddThemeStyleboxOverride("normal", Ui.Box(Ui.AccentDim, 10, padX: 18, padY: 8)); }
            else { _tabButtons[i].RemoveThemeStyleboxOverride("normal"); }
        }
        foreach (var child in _hints.GetChildren())
        {
            _hints.RemoveChild(child);
            child.QueueFree();
        }
        if (screen != null)
        {
            foreach (var (action, label) in screen.Hints) { _hints.AddChild(Ui.Hint(action, label)); }
        }
        OnChromeRefreshed(screen);
    }

    /// <summary>Applies a changed style, palette or background and rebuilds the current sidebar page with it.</summary>
    public virtual void ReapplyAppearance()
    {
        ApplyAppearance();
        var index = TabIndex(Current?.Tab);
        if (index >= 0) { ResetTo(_tabs[index].Create()); }
        else { RefreshChrome(); }
    }

    private void BuildToast()
    {
        _toastLabel = Ui.Label("", 26);
        _toast = Ui.MicaPanel(_toastLabel, 28, 16);
        _toast.Visible = false;
        _toast.MouseFilter = MouseFilterEnum.Ignore;
        var holder = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        holder.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        holder.OffsetTop = 40;
        holder.OffsetBottom = 120;
        holder.AddChild(_toast);
        AddChild(holder);
    }

    /// <summary>Shows a short message at the top of the window for a few seconds.</summary>
    public void Toast(string message)
    {
        _toastLabel.Text = message;
        _toast.Visible = true;
        _toast.Modulate = Colors.White;
        _toastTween?.Kill();
        _toastTween = CreateTween();
        _toastTween.TweenInterval(3.5);
        _toastTween.TweenProperty(_toast, "modulate:a", 0f, 0.4);
        _toastTween.TweenCallback(Callable.From(() => _toast.Visible = false));
    }

    public static bool IsFullscreen =>
        DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;

    public void ToggleFullscreen()
    {
        var full = IsFullscreen;
        DisplayServer.WindowSetMode(full ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
        OnFullscreenChanged(!full);
    }

    // ---- Input ----

    public override void _Input(InputEvent e)
    {
        var overlay = TopOverlay;
        var handled = overlay != null ? overlay.OnRawInput(e) : Current?.OnRawInput(e) ?? false;
        if (handled) { GetViewport().SetInputAsHandled(); }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed(Actions.Fullscreen))
        {
            ToggleFullscreen();
            GetViewport().SetInputAsHandled();
            return;
        }

        var overlay = TopOverlay;
        if (overlay != null)
        {
            if (overlay.OnInput(e)) { GetViewport().SetInputAsHandled(); }
            else if (e.IsActionPressed(Actions.Back))
            {
                CloseOverlay(overlay);
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        var screen = Current;
        if (screen == null) { return; }
        if (screen.OnInput(e)) { GetViewport().SetInputAsHandled(); }
        else if (e.IsActionPressed(Actions.Back))
        {
            Back();
            GetViewport().SetInputAsHandled();
        }
        else if (e.IsActionPressed(Actions.Lb) || e.IsActionPressed(Actions.Rb))
        {
            // A tap switches sidebar page on release. A hold goes to the screen.
            _shoulder = e.IsActionPressed(Actions.Lb) ? Actions.Lb : Actions.Rb;
            _shoulderFor = 0;
            GetViewport().SetInputAsHandled();
        }
        else if (_shoulder != null && e.IsActionReleased(_shoulder))
        {
            var direction = _shoulder == Actions.Lb ? -1 : 1;
            _shoulder = null;
            if (screen.Tab != null) { CycleTab(direction); }
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        _clockIn -= delta;
        if (_clockIn <= 0)
        {
            _clockIn = 5;
            RefreshClock();
        }

        if (_shoulder != null)
        {
            _shoulderFor += delta;
            if (_shoulderFor >= ShoulderHoldSeconds)
            {
                _shoulder = null;
                if (TopOverlay == null) { Current?.OnShoulderHold(); }
            }
        }

        var held = HeldPadDirection();
        if (held == null || held != _heldAction)
        {
            _heldAction = held;
            _heldFor = 0;
            _repeatIn = 0.4;
            return;
        }
        _heldFor += delta;
        _repeatIn -= delta;
        if (_repeatIn > 0) { return; }
        _repeatIn = _heldFor > 1.5 ? 0.05 : 0.1;
        Input.ParseInputEvent(new InputEventAction { Action = held, Pressed = true });
    }

    private static string HeldPadDirection()
    {
        foreach (var pad in Input.GetConnectedJoypads())
        {
            var x = Input.GetJoyAxis(pad, JoyAxis.LeftX);
            var y = Input.GetJoyAxis(pad, JoyAxis.LeftY);
            if (Input.IsJoyButtonPressed(pad, JoyButton.DpadLeft) || x < -0.6f) { return "ui_left"; }
            if (Input.IsJoyButtonPressed(pad, JoyButton.DpadRight) || x > 0.6f) { return "ui_right"; }
            if (Input.IsJoyButtonPressed(pad, JoyButton.DpadUp) || y < -0.6f) { return "ui_up"; }
            if (Input.IsJoyButtonPressed(pad, JoyButton.DpadDown) || y > 0.6f) { return "ui_down"; }
        }
        return null;
    }
}
