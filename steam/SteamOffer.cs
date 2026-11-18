using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace FishUI;

/// <summary>
/// Offers to add the app to Steam as a non-Steam game and start it from there: close Steam if a
/// shortcut has to be written, write it with artwork and Steam Input turned on, then start the
/// shortcut through Steam and quit this copy. It asks through AppShell's menus and toasts.
/// </summary>
public static class SteamOffer
{
    private const double StatusVisibleSeconds = 2.0;

    /// <summary>Where the app's settings offer this again, named in the messages.</summary>
    public static string SettingsLocation { get; set; } = "Settings > System";

    private static string App => SteamShortcuts.ShortcutName;

    private static string Benefits =>
        $"Open {App} from your Steam library, in Big Picture and in Game Mode on a Steam Deck. Steam Input makes any controller work, and the Steam overlay is available.";

    private static string TryAgain => $"then choose Steam library in {SettingsLocation}.";

    /// <summary>
    /// Shows the offer when it can help: a desktop build started outside Steam, with Steam installed.
    /// </summary>
    /// <param name="enabled">The app's saved setting. False once the user picked Don't Ask Again.</param>
    /// <param name="dontAskAgain">Called when the user picks Don't Ask Again, so the app can save that.</param>
    public static async Task OfferIfUsefulAsync(bool enabled, Action dontAskAgain)
    {
        if (!SteamShortcuts.IsSupportedPlatform || SteamShortcuts.IsLaunchedThroughSteam || !enabled) { return; }
        var state = SteamShortcuts.GetState();
        if (state == SteamShortcutState.SteamNotFound) { return; }

        var (title, accept, how) = state switch
        {
            SteamShortcutState.Current => ($"Open {App} from Steam?", "Restart Through Steam", $"{App} is already in your Steam library. It will close and reopen from Steam."),
            SteamShortcutState.Outdated => ($"Fix {App}'s Steam shortcut?", "Fix and Restart Through Steam", $"Your Steam shortcut is out of date. If Steam is open, it will restart. {App} will then reopen from Steam."),
            _ => ($"Add {App} to Steam?", "Add to Steam and Restart", $"If Steam is open, it will restart. {App} will then reopen from Steam."),
        };
        var choice = await AppShell.I.ChooseAsync(title, new[] { accept, "Not Now", "Don't Ask Again" }, $"{Benefits}\n\n{how}");
        if (choice == 0) { await StartThroughSteamAsync(); }
        else if (choice == 2)
        {
            dontAskAgain?.Invoke();
            GD.Print("[Steam] the user chose not to be asked about starting through Steam again.");
        }
    }

    /// <summary>Adds or repairs the shortcut as needed, then restarts this app through Steam.</summary>
    public static async Task StartThroughSteamAsync()
    {
        if (!SteamShortcuts.IsSupportedPlatform) { return; }
        var state = SteamShortcuts.GetState();
        if (state == SteamShortcutState.SteamNotFound)
        {
            await TellAsync("Steam wasn't found", $"Install Steam and sign in, {TryAgain}");
            return;
        }

        if (state == SteamShortcutState.Current)
        {
            // Artwork and Steam Input can only be written while Steam is closed.
            if (!SteamShortcuts.IsSteamRunning()) { SteamShortcuts.AddOrUpdateThisApp(true); }
            await RelaunchAsync();
            return;
        }

        if (SteamShortcuts.IsSteamRunning())
        {
            var restart = await AppShell.I.ChooseAsync("Restart Steam?", new[] { "Restart Steam", "Not Now" },
                $"Steam only reads new library entries when it starts. {App} will close Steam, add itself, and reopen from Steam.");
            if (restart != 0) { return; }
            AppShell.I.Toast("Closing Steam…");
            await SteamShortcuts.CloseSteam();
            if (SteamShortcuts.IsSteamRunning())
            {
                await TellAsync("Steam didn't close", $"Close Steam yourself, {TryAgain}");
                return;
            }
        }

        AppShell.I.Toast($"Adding {App} to Steam…");
        var result = SteamShortcuts.AddOrUpdateThisApp(true);
        if (result is SteamShortcutResult.SteamNotFound or SteamShortcutResult.Failed)
        {
            await TellAsync(result == SteamShortcutResult.SteamNotFound ? "Steam wasn't found" : $"{App} couldn't add itself to Steam",
                result == SteamShortcutResult.SteamNotFound
                    ? $"Install Steam and sign in, {TryAgain}"
                    : "Add it yourself: in Steam, choose Games > Add a Non-Steam Game to My Library and pick this app. Then open its Properties > Controller and choose Enable Steam Input.");
            SteamShortcuts.OpenSteamLibrary();
            return;
        }
        await RelaunchAsync();
    }

    private static async Task RelaunchAsync()
    {
        AppShell.I.Toast($"Starting {App} through Steam…");
        SteamShortcuts.LaunchThroughSteam();
        await Task.Delay((int)(StatusVisibleSeconds * 1000));
        GD.Print("[Steam] quitting so Steam's copy takes over.");
        await AppShell.I.QuitAsync();
    }

    private static Task TellAsync(string title, string body) => AppShell.I.ChooseAsync(title, new List<string> { "OK" }, body);

    public static string DescribeStatus()
    {
        if (SteamShortcuts.IsLaunchedThroughSteam) { return "Running Through Steam"; }
        return SteamShortcuts.GetState() switch
        {
            SteamShortcutState.Current => "In Your Steam Library",
            SteamShortcutState.Outdated => "Steam Shortcut Out of Date",
            SteamShortcutState.SteamNotFound => "Steam Not Found",
            _ => "Not in Your Steam Library",
        };
    }

    public static string DescribeAction() => SteamShortcuts.GetState() switch
    {
        SteamShortcutState.Current => "Restart through Steam",
        SteamShortcutState.Outdated => "Fix the Steam shortcut",
        _ => "Add to Steam",
    };
}
