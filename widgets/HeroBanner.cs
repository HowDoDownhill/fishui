using Godot;

namespace FishUI;

/// <summary>
/// A banner for the top of a page that describes whatever has focus: a small line above, a large
/// title, a line below, and artwork fading in from the right. Put it above a RowPage and call
/// Display from the page's CardFocused event.
/// </summary>
public partial class HeroBanner : Control
{
    public const int Height = 290;
    private const float ArtStart = 0.38f;
    private const double FadeOut = 0.07;
    private const double FadeIn = 0.18;

    private const string ArtShader = @"shader_type canvas_item;
void fragment() {
    vec4 c = texture(TEXTURE, UV);
    float side = smoothstep(0.0, 0.6, UV.x);
    float floor = smoothstep(1.0, 0.45, UV.y);
    float ceiling = smoothstep(0.0, 0.12, UV.y);
    COLOR = vec4(c.rgb, c.a * side * floor * ceiling);
}";

    private readonly Control _content;
    private readonly TextureRect _art;
    private readonly Label _eyebrow;
    private readonly Label _title;
    private readonly Label _subtitle;
    private Tween _tween;
    private Tween _artTween;
    private string _shown;

    public HeroBanner()
    {
        CustomMinimumSize = new Vector2(0, Height);
        MouseFilter = MouseFilterEnum.Ignore;
        ClipContents = true;

        _content = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_content);
        Ui.Fill(_content);

        _art = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = new Shader { Code = ArtShader } },
        };
        _content.AddChild(_art);
        Ui.Fill(_art);
        _art.AnchorLeft = ArtStart;

        var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        column.AddThemeConstantOverride("separation", 2);
        _eyebrow = Ui.Label("", 22, variation: "AccentLabel");
        _title = Ui.Label("", variation: "DisplayLabel");
        _title.ClipText = true;
        _title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _subtitle = Ui.Label("", 26, variation: "CaptionLabel");
        _subtitle.ClipText = true;
        _subtitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        column.AddChild(_eyebrow);
        column.AddChild(_title);
        column.AddChild(_subtitle);
        _content.AddChild(column);
        Ui.Fill(column);
        column.OffsetLeft = Ui.Margin;
        column.AnchorRight = 0.6f;
    }

    /// <summary>Shows an item, fading from the one before it.</summary>
    /// <param name="eyebrow">A short line above the title, such as the row the item is in.</param>
    public void Display(string title, string subtitle, Texture2D art = null, string eyebrow = null)
    {
        var key = $"{eyebrow}\n{title}\n{subtitle}";
        if (key == _shown) { return; }
        var first = _shown == null;
        _shown = key;

        void Swap()
        {
            _eyebrow.Text = eyebrow ?? "";
            _eyebrow.Visible = !string.IsNullOrEmpty(eyebrow);
            _title.Text = title ?? "";
            _subtitle.Text = subtitle ?? "";
            _artTween?.Kill();
            _art.Texture = art;
            _art.Modulate = Colors.White;
        }

        _tween?.Kill();
        if (first || !IsInsideTree())
        {
            Swap();
            _content.Modulate = Colors.White;
            return;
        }

        _tween = CreateTween();
        _tween.TweenProperty(_content, "modulate:a", 0f, FadeOut);
        _tween.TweenCallback(Callable.From(Swap));
        _tween.TweenProperty(_content, "modulate:a", 1f, FadeIn);
    }

    /// <summary>
    /// Swaps the artwork alone, leaving the text in place. For artwork that arrives after Display,
    /// such as a backdrop still downloading when the card took focus.
    /// </summary>
    public void SetArt(Texture2D art)
    {
        if (_art.Texture == art) { return; }
        _artTween?.Kill();
        _art.Texture = art;
        if (!IsInsideTree()) { return; }
        _art.Modulate = new Color(1, 1, 1, 0);
        _artTween = CreateTween();
        _artTween.TweenProperty(_art, "modulate:a", 1f, FadeIn);
    }
}
