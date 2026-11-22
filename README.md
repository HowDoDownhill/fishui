# FishUI

A controller-first interface library for Godot 4.7 and C#. It holds the interface RomBox and Padfin
share: the Glass and Simple styles, colour palettes, animated backgrounds, frosted overlays, the
gliding selection highlight, controller glyphs, interface sounds, and an app shell with screens,
overlays and a settings layout.

Everything is in the `FishUI` namespace.

## Adding it to an app

FishUI is a Godot addon that lives at `res://addons/fishui`. Mount this repository there as a git
submodule:

```
git submodule add <this repository's URL> addons/fishui
```

Godot compiles the C# into the app's own assembly, so there is nothing else to reference.

The app also needs:

1. **Controller Icons** at `res://addons/controller_icons`, with its `ControllerIcons` autoload.
   FishUI draws button prompts with it.
2. **The theme and font** in `project.godot`:

   ```
   [gui]
   theme/custom="res://addons/fishui/assets/themes/theme.tres"
   theme/custom_font="res://addons/fishui/assets/fonts/InterVariable.woff2"
   ```

3. A 1920 by 1080 viewport with `canvas_items` stretch. Sizes in the library are 1080p units.

## The app shell

Subclass `AppShell`, put the subclass on the root `Control` of the main scene, and override the
hooks the app needs.

```csharp
using FishUI;

public partial class App : AppShell
{
    protected override IReadOnlyList<ShellTab> Tabs { get; } = new[]
    {
        new ShellTab("home", "Home", () => new HomeScreen()),
        new ShellTab("settings", "Settings", () => new SettingsScreen()),
    };

    protected override void ApplyAppearance() =>
        Appearance.Apply(Config.Style, Config.Background, Config.Theme);

    protected override Task StartAsync(string[] args)
    {
        SwitchTab("home");
        return Task.CompletedTask;
    }
}
```

The shell draws the background, a top bar, the sidebar built from `Tabs`, and a footer of button
prompts. It keeps a stack of screens and a stack of modal overlays, and sends input to the top one.

| Call | Effect |
|---|---|
| `Push`, `Pop`, `Replace`, `ResetTo` | Move through the screen stack. |
| `SwitchTab(id)` | Open a sidebar page. LB and RB step through the pages. |
| `ShowMenu`, `ChooseAsync` | A modal list of choices. `ChooseAsync` completes with the index, or -1. |
| `ShowJump` | A grid of tiles for picking one of many. |
| `ConfirmHoldAsync` | A question whose risky answer must be held down. True only after the hold. |
| `PromptAsync` | Text entry with the on-screen keyboard. Null when cancelled. |
| `OpenOverlay`, `CloseOverlay` | Any `Overlay` subclass, such as `ProgressOverlay`. |
| `Toast` | A short message at the top of the window. |
| `ReapplyAppearance` | Apply changed appearance settings and rebuild the current page. |
| `QuitAsync` | Exit, after the top screen's `OnQuitAsync`. |

Hooks to override: `Tabs`, `StartMenu`, `ApplyAppearance`, `InterfaceSize`, `StartFullscreen`, `WindowedSize`,
`OnFullscreenChanged`, `TopBarTitle`, `TopBarAvailable`, `BuildTopBarStart`, `BuildTopBarEnd`,
`OnChromeRefreshed`, `StartAsync`, `QuitAsync`.

### Screens

A `Screen` is one full page. Build its controls in `OnEnter`.

| Member | Purpose |
|---|---|
| `Tab` | The sidebar page this screen is. Null for pushed screens, which hide the sidebar. |
| `Title`, `SetInfo` | The page name and a status line in the top bar. |
| `Hints` | The footer prompts, as (input action, label) pairs. |
| `ShowChrome`, `ShowTopBar` | Hide the bars, for full-window content. |
| `FocusDefault` | Focus the starting control. |
| `OnInput`, `OnRawInput` | Input the focused control did not use, and input before focus sees it. |
| `OnBack` | B. Return true to stay open. |
| `OnShoulderHold` | LB or RB held. |
| `PageMenu` | This page's own group in the Start menu. |
| `OnTrigger`, `TriggerLabel` | LT or RT pressed on its own, and the words for its footer prompt. |
| `OnResume`, `OnQuitAsync` | A screen above closed; the app is exiting. |

### Input

`Actions` names the input actions: `Accept`, `Back`, `X`, `Y`, `Lb`, `Rb`, `Start`, `Fullscreen`.
The shell registers them with keyboard keys beside the controller buttons (F2, F3, Page Up,
Page Down, F1, F11) and repeats a held D-pad or stick direction.

Conventions the apps follow, so the same press means the same thing everywhere:

- LB and RB step between pages or sections. A tap steps once.
- Holding RB opens a list to jump with. `OnShoulderHold` reports a hold of either shoulder, and
  an app gives the jump list to RB. Holding LB does nothing more than its tap.
- A trigger on its own does nothing unless the screen gives it a job with `OnTrigger`. Triggers
  never step between pages: LT with RT is the quick actions chord, and a trigger that also paged
  would move the page on the way into the chord.
- A scroll bar shows position only. Lists scroll by moving focus.

## The look

`Appearance.Apply(style, background, palette)` sets everything: the background shader, panel
material, theme colours, selection highlight and accent. Read `Appearance.Accent`, `Text`,
`CaptionText`, `IsLight` and `IsSimple` when drawing, and subscribe to `Appearance.Changed` to
redraw.

- `Palettes.All` holds the built-in palettes. An app can add its own, or pass a `Palette` straight
  to `Apply`.
- `Palettes.Backgrounds` names the animated backgrounds.
- `Appearance.Styles` is Glass and Simple. Both share one layout: bars that run edge to edge, flat
  panels inside pages, and an accent ring on whatever has focus. Glass adds the animated background
  and frost on overlays. Simple draws flat colours and turns on Godot's low-processor mode.
- Scroll bars are a thin rounded thumb with no track and no arrows. The thumb is faint at rest
  and takes the accent colour under the pointer. `Appearance` styles every `VScrollBar` and
  `HScrollBar` this way, so a list needs no styling of its own. To hide the bar on one list, set
  its scroll mode to `ShowNever`; the list still scrolls with focus.
- `Ui.Panel` is the flat panel for content inside a page. `Ui.MicaPanel` is the frosted one, for
  things that float over moving content such as a toast or controls over a video. `Ui.Chrome` is
  the wash behind a bar that runs to the window edge.

Theme type variations in `theme.tres`: `DisplayLabel`, `TitleLabel`, `CaptionLabel`, `MutedLabel`,
`AccentLabel`, `WarningLabel`, `DetailValueLabel`, `ListButton`, `TitleButton`, `WarningListButton`,
`FlatButton`, `DisplayButton`.

Other pieces:

| Class | Purpose |
|---|---|
| `Ui` | Builders for labels, list buttons, flat and frosted panels, prompts, padding and focus confinement. |
| `SelectionHighlighter` | The highlight that glides between focused list buttons. Other controls call `Set`. |
| `UiPanel` | A panel with a frosted body, shadow and open and close transitions, usable from scenes. |
| `UiSounds` | Move, Select, Back and Switch sounds. Set `Enabled` from the app's settings. |
| `ControllerGlyph` | Puts the glyph for an input action on a button. |
| `InterfaceScale` | Applies the interface size setting, with an automatic size for handhelds. |
| `MicaShadow`, `FocusHighlight`, `FocusCycler` | Shadow for frosted panels, highlight styles, focus stepping. |

An app that has its own root node can use the look without `AppShell`: call `Appearance.Apply`,
`SelectionHighlighter.Attach(GetViewport())`, add a `UiSounds` node, and put
`Appearance.BackgroundMaterial` on a full-window `ColorRect`.

## Widgets

| Class | Purpose |
|---|---|
| `Card`, `CardRow`, `RowPage` | Artwork tiles, a sideways-scrolling row of them, and a page of rows. |
| `CardGrid` | A wall of cards that wraps into rows, for a whole library on one page. `StepLetter` jumps through the alphabet from a screen's `OnTrigger`, `NearEnd` asks for the next page of cards, and `LoadsImagesNearView` keeps artwork loaded only around the view. |
| `HeroBanner` | A banner above a `RowPage` that describes the focused card. Call `Display` from `RowPage.CardFocused`. |
| `SideMenu`, `MenuGroup` | The Start menu: a panel at the right edge with choices in headed groups. `AppShell` opens it on Start from every page with the bars, built from the page's `PageMenu` and the app's `StartMenu`, and adds its prompt to the footer. A page that handles Start itself keeps it. |
| `QuickPanel` | A panel that slides in from the right with sliders, toggles and actions, for use over a game or video. |
| `Ui.NavIcon` | The library's line icons for sidebar pages. Giving every `ShellTab` an icon makes the sidebar narrow to a strip of icons while focus is in the page. |
| `OverlayHost` | The layer that holds open overlays and gives focus back when one closes. `AppShell` has one. An app with its own scenes adds one with `RoutesInput` on and gets every overlay without the rest of the shell. |
| `ChordWatcher` | Runs an action when controller inputs are pressed together. `AppShell` has one with two chords: LB with RB moves focus to the sidebar and back, and LT with RT opens the ring of `QuickActions`. `HoldsBackSingles` suits apps whose shoulder buttons act the moment they go down. A chord added with a label gets a prompt at the left of the footer; `BuildPrompts` and `Ui.ChordHint` make the same prompts for an app with its own footer. |
| `RadialMenu` | A ring of up to eight choices picked by stick or D-pad direction. `AppShell.ShowRadial` opens one. |
| `HoldButton`, `ConfirmOverlay` | A button that runs only after being held, and the question built on it. |
| `SettingsPage`, `SettingRow` | Sections on the left, rows on the right, help for the focused row. |
| `CarouselButton` | A value stepped left and right, as used in setting rows. |
| `MenuOverlay`, `JumpOverlay`, `TextPrompt`, `ProgressOverlay` | The modal overlays behind the shell's calls. |
| `KeyboardGrid` | The on-screen keyboard, for use outside `TextPrompt`. |
| `FocusScroller` | Scrolls a `ScrollContainer` to follow focus with a margin, or to align a section. |
| `SeekBar` | A position bar with marks and a scrub preview. |

## Steam

`SteamShortcuts` adds the app to Steam as a non-Steam game with artwork and Steam Input turned on.
`SteamOffer` asks the user and restarts the app through Steam.

```csharp
SteamShortcuts.Configure("Padfin", "padfin");
await SteamOffer.OfferIfUsefulAsync(Config.OfferSteamLaunch, () => Config.OfferSteamLaunch = false);
```

Artwork is read from `res://assets/steam/` by default: `capsule.png`, `wide_capsule.png`,
`hero.png` and `logo.png`.

## Developer arguments

`AppShell` reads these after `--` on the Godot command line.

| Argument | Effect |
|---|---|
| `--windowed` | Start in a window. |
| `--keys ui_down,hold:fish_rb,joy:0,chord:lt+rt,quit` | Press input actions in order. `hold:` holds one down. `joy:N` presses controller button N. `chord:` presses buttons or triggers together, and `chord:rt` is one trigger. `quit` exits. |
| `--shot <png> [seconds]` | Save a screenshot after a delay, then exit. |

## Licence

GPL-3.0. The Inter font is under the SIL Open Font License; see `assets/fonts/Inter-LICENSE.txt`.
