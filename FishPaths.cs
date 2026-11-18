namespace FishUI;

/// <summary>Where the addon's resources live. Apps mount the repository at res://addons/fishui.</summary>
public static class FishPaths
{
    public const string Root = "res://addons/fishui/";
    public const string Assets = Root + "assets/";

    public const string Theme = Assets + "themes/theme.tres";
    public const string Font = Assets + "fonts/InterVariable.woff2";

    public const string BackgroundMaterial = Assets + "materials/moving_background.tres";
    public const string MicaMaterial = Assets + "materials/mica_panel.tres";
    public const string LogoMaterial = Assets + "materials/logo_adapt.tres";

    public const string MicaShader = Assets + "shaders/mica_panel.gdshader";
    public const string FlatPanelShader = Assets + "shaders/flat_panel.gdshader";
    public const string Backgrounds = Assets + "shaders/backgrounds/";
    public const string Sounds = Assets + "sounds/";
}
