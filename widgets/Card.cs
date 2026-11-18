using System;
using System.Threading.Tasks;
using Godot;

namespace FishUI;

public enum CardShape { Poster, Landscape, Square }

/// <summary>
/// A focusable tile: artwork, title, subtitle, a progress bar and a corner badge. It is a rounded
/// dark panel with padded artwork, and when selected a drop shadow and the shared selection
/// highlight, which glides between cards. An app can subclass it to carry its own item.
/// </summary>
public partial class Card : Button
{
    private const int Padding = 12;
    private const int TextHeight = 62;
    private const int CornerRadius = 2;
    private const int SelectedCornerRadius = 2;
    private const int ShadowSize = 14;
    private const int ShadowDrop = 6;
    private const float LiftScale = 1.04f;
    private const double LiftSeconds = 0.12;

    /// <summary>
    /// How far the selected card's shadow reaches above and below it. A scrolling container needs
    /// at least this much padding around its cards, or it clips the shadow to a hard edge.
    /// </summary>
    public const int ShadowAbove = ShadowSize - ShadowDrop + 2;
    public const int ShadowBelow = ShadowSize + ShadowDrop + 2;

    /// <summary>Anything the app wants to find again when the card is pressed.</summary>
    public object Payload { get; set; }

    /// <summary>Fetches the artwork. LoadImage awaits it; a null result leaves the placeholder initial.</summary>
    public Func<Task<Texture2D>> ImageSource { get; set; }

    public string Title { get; private set; }
    public string Subtitle { get; private set; }

    /// <summary>The artwork now showing, or null while the placeholder initial is.</summary>
    public Texture2D Image => IsInstanceValid(_image) ? _image.Texture : null;

    // Everything the card draws sits in here, so the selected card can grow without its container
    // resetting the scale.
    private Control _body;
    private Tween _lift;

    private TextureRect _image;
    private Panel _panel;
    private Panel _shadow;
    private bool _requested;

    public Card() { }

    /// <param name="width">Artwork width in 1080p units. The height follows from the shape.</param>
    /// <param name="progress">A percentage from 0 to 100 drawn as a bar along the artwork's lower edge. Zero hides it.</param>
    /// <param name="badge">Short text for the artwork's top right corner, such as a count.</param>
    /// <param name="showText">False leaves out the title and subtitle, for a denser wall of artwork.</param>
    public Card(string title, string subtitle, CardShape shape, int width, double progress = 0, string badge = null, bool showText = true)
    {
        Build(title, subtitle, shape, width, progress, badge, showText);
    }

    public static Vector2 ImageSize(CardShape shape, int width) => shape switch
    {
        CardShape.Poster => new Vector2(width, width * 1.5f),
        CardShape.Landscape => new Vector2(width, width * 9f / 16f),
        _ => new Vector2(width, width),
    };

    /// <summary>Builds the card. A subclass with its own constructor calls this once.</summary>
    protected void Build(string title, string subtitle, CardShape shape, int width, double progress = 0, string badge = null, bool showText = true)
    {
        Title = title;
        Subtitle = subtitle;
        var imageSize = ImageSize(shape, width);
        CustomMinimumSize = imageSize + new Vector2(Padding * 2, Padding * 2 + (showText ? TextHeight : 0));
        FocusMode = FocusModeEnum.All;
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
        {
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }

        _body = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_body);
        Ui.Fill(_body);

        _shadow = new Panel { Visible = false, ShowBehindParent = true, MouseFilter = MouseFilterEnum.Ignore };
        // The selection box has slightly rounder corners than the card, and the shadow sits under the box.
        var shadowStyle = Ui.Box(new Color(0, 0, 0, 0), SelectedCornerRadius);
        shadowStyle.ShadowColor = new Color(0, 0, 0, Appearance.IsLight ? 0.2f : 0.45f);
        shadowStyle.ShadowSize = ShadowSize;
        shadowStyle.ShadowOffset = new Vector2(0, ShadowDrop);
        _shadow.AddThemeStyleboxOverride("panel", shadowStyle);
        _body.AddChild(_shadow);
        Ui.Fill(_shadow);

        _panel = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        _panel.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0, 0, 0, Appearance.IsLight ? 0.07f : 0.25f), CornerRadius));
        _body.AddChild(_panel);
        Ui.Fill(_panel);

        var origin = new Vector2(Padding, Padding);
        var initial = Ui.Label(string.IsNullOrEmpty(title) ? "" : title[..1].ToUpperInvariant(), 72, variation: "MutedLabel");
        initial.Position = origin;
        initial.Size = imageSize;
        initial.Modulate = new Color(1, 1, 1, 0.35f);
        initial.HorizontalAlignment = HorizontalAlignment.Center;
        initial.VerticalAlignment = VerticalAlignment.Center;
        _body.AddChild(initial);

        _image = new TextureRect
        {
            Position = origin,
            Size = imageSize,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _body.AddChild(_image);

        if (showText)
        {
            _body.AddChild(TextLine(title, 22, null, Padding + imageSize.Y + 6, width));
            _body.AddChild(TextLine(subtitle, 19, "CaptionLabel", Padding + imageSize.Y + 34, width));
        }

        if (progress > 0)
        {
            var bar = new Vector2(Padding, Padding + imageSize.Y - 8);
            _body.AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.6f), Position = bar, Size = new Vector2(width, 8), MouseFilter = MouseFilterEnum.Ignore });
            _body.AddChild(new ColorRect { Color = Ui.Accent, Position = bar, Size = new Vector2(width * (float)(Math.Min(progress, 100) / 100.0), 8), MouseFilter = MouseFilterEnum.Ignore });
        }
        if (!string.IsNullOrEmpty(badge)) { _body.AddChild(Badge(badge, width)); }

        FocusEntered += () => SetSelected(true);
        FocusExited += () => SetSelected(false);
    }

    /// <summary>
    /// The selected card gains a drop shadow and an accent ring, and grows a little. The ring
    /// follows the card as it grows.
    /// </summary>
    private void SetSelected(bool selected)
    {
        _shadow.Visible = selected;
        SelectionHighlighter.Set(this, _body, selected, 0, FocusHighlight.RingGap);

        _body.PivotOffset = Size / 2.0f;
        _lift?.Kill();
        _lift = CreateTween().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        _lift.TweenProperty(_body, "scale", selected ? new Vector2(LiftScale, LiftScale) : Vector2.One, LiftSeconds);
    }

    private static Label TextLine(string text, int size, string variation, float y, int width)
    {
        // Clipping goes first: an unclipped label refuses to be narrower than its text.
        var label = Ui.Label(text, size, variation: variation);
        label.ClipText = true;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.Position = new Vector2(Padding, y);
        label.Size = new Vector2(width, size + 10);
        return label;
    }

    private static Control Badge(string text, int width)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0, 0, 0, 0.75f), Ui.Radius, 1, Ui.Accent, 8, 1));
        panel.AddChild(Ui.Label(text, 18, Colors.White));
        panel.Position = new Vector2(Padding + width - 8 - (24 + text.Length * 11), Padding + 8);
        return panel;
    }

    /// <summary>Shows artwork the app already has.</summary>
    public void SetImage(Texture2D texture)
    {
        if (IsInstanceValid(_image)) { _image.Texture = texture; }
    }

    /// <summary>Requests the artwork from ImageSource. Safe to call more than once.</summary>
    public async void LoadImage()
    {
        if (_requested || ImageSource == null) { return; }
        _requested = true;
        var texture = await ImageSource();
        if (!IsInstanceValid(this) || !_requested) { return; }
        _image.Texture = texture;
    }

    /// <summary>Drops the artwork so a long grid does not keep every image in video memory.</summary>
    public void UnloadImage()
    {
        if (!_requested) { return; }
        _requested = false;
        _image.Texture = null;
    }
}
