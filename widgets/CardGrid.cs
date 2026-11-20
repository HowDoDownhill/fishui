using System;
using System.Collections.Generic;
using Godot;

namespace FishUI;

/// <summary>
/// A wall of cards that wraps into rows and scrolls up and down to follow focus, for a whole
/// library on one page. LT and RT step through the alphabet with <see cref="StepLetter"/>, and a
/// large letter shows where focus landed. Cards can be added a page at a time: <see cref="NearEnd"/>
/// asks for the next page.
/// </summary>
public partial class CardGrid : Control
{
    private const int EdgeMargin = 60;
    private const double LetterSeconds = 0.9;

    private readonly ScrollContainer _scroll;
    private readonly HFlowContainer _flow;
    private Control _letterBox;
    private Label _letter;
    private Tween _letterTween;

    public List<Card> Cards { get; } = new();

    /// <summary>Raised when focus lands on a card.</summary>
    public event Action<Card> CardFocused;

    /// <summary>Raised when focus comes within <see cref="NearEndCards"/> of the last card, so the app can add more.</summary>
    public event Action NearEnd;

    public int NearEndCards = 30;

    /// <summary>
    /// True loads artwork only for cards near the view, through each card's ImageSource, and drops
    /// it for cards far away. A long library then keeps a few screens of images in video memory.
    /// </summary>
    public bool LoadsImagesNearView;

    /// <summary>
    /// The letter a card files under, for <see cref="StepLetter"/>. The default is the first
    /// character of its title, with "#" for anything that is not a letter.
    /// </summary>
    public Func<Card, string> LetterOf = card => Initial(card.Title);

    public CardGrid() : this(30, 26) { }

    public CardGrid(int columnGap, int rowGap)
    {
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.ShowNever,
        };
        AddChild(_scroll);
        Ui.Fill(_scroll);

        _flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _flow.AddThemeConstantOverride("h_separation", columnGap);
        _flow.AddThemeConstantOverride("v_separation", rowGap);
        var content = Ui.Pad(_flow, Ui.Margin, Card.ShadowAbove, Ui.Margin, EdgeMargin);
        _scroll.AddChild(content);
        FocusScroller.KeepVisible(_scroll, content, EdgeMargin, vertical: true);
        _scroll.GetVScrollBar().ValueChanged += _ => UpdateImages();

        BuildLetter();
    }

    /// <summary>The first character of a name as a filing letter: A to Z, or "#".</summary>
    public static string Initial(string name)
    {
        foreach (var character in name ?? "")
        {
            if (char.IsWhiteSpace(character)) { continue; }
            return char.IsLetter(character) ? char.ToUpperInvariant(character).ToString() : "#";
        }
        return "#";
    }

    public void Add(Card card, Action<Card> pressed = null)
    {
        if (pressed != null) { card.Pressed += () => pressed(card); }
        var index = Cards.Count;
        card.FocusEntered += () => OnCardFocused(card, index);
        _flow.AddChild(card);
        Cards.Add(card);
        // Layout settles at the end of the frame, which is when card positions are known.
        if (LoadsImagesNearView) { Callable.From(UpdateImages).CallDeferred(); }
    }

    /// <summary>Removes every card and returns to the top.</summary>
    public void Clear()
    {
        foreach (var card in Cards)
        {
            _flow.RemoveChild(card);
            card.QueueFree();
        }
        Cards.Clear();
        _scroll.ScrollVertical = 0;
    }

    public void Focus(int index = 0)
    {
        if (Cards.Count > 0) { Cards[Math.Clamp(index, 0, Cards.Count - 1)].GrabFocus(); }
    }

    /// <summary>Index of the focused card, or -1 when focus is elsewhere.</summary>
    public int FocusedIndex
    {
        get
        {
            var owner = GetViewport()?.GuiGetFocusOwner();
            return owner is Card card ? Cards.IndexOf(card) : -1;
        }
    }

    /// <summary>How many cards sit in the first row.</summary>
    public int Columns
    {
        get
        {
            if (Cards.Count == 0) { return 1; }
            var top = Cards[0].Position.Y;
            var count = 1;
            while (count < Cards.Count && Mathf.IsEqualApprox(Cards[count].Position.Y, top)) { count++; }
            return count;
        }
    }

    /// <summary>
    /// Moves focus to the first card of the next letter, or back to the start of this letter and
    /// then the one before. Returns false when no loaded card answers, which for a grid still
    /// loading means the next letter is on a later page.
    /// </summary>
    public bool StepLetter(int direction)
    {
        if (Cards.Count == 0) { return false; }
        var current = Math.Max(0, FocusedIndex);
        var letter = LetterOf(Cards[current]);
        int target;

        if (direction > 0)
        {
            target = current + 1;
            while (target < Cards.Count && LetterOf(Cards[target]) == letter) { target++; }
            if (target >= Cards.Count) { return false; }
        }
        else
        {
            target = FirstOfLetter(current);
            if (target == current)
            {
                if (current == 0) { return false; }
                target = FirstOfLetter(current - 1);
            }
        }

        Focus(target);
        ShowLetter(LetterOf(Cards[target]));
        return true;
    }

    private int FirstOfLetter(int index)
    {
        var letter = LetterOf(Cards[index]);
        while (index > 0 && LetterOf(Cards[index - 1]) == letter) { index--; }
        return index;
    }

    /// <summary>Moves focus up or down by as many rows as fit in the view.</summary>
    public void StepPage(int direction)
    {
        if (Cards.Count == 0) { return; }
        var current = Math.Max(0, FocusedIndex);
        var rowHeight = Cards[0].Size.Y + _flow.GetThemeConstant("v_separation");
        var rows = Math.Max(1, (int)(_scroll.Size.Y / Math.Max(1, rowHeight)));
        Focus(current + direction * rows * Columns);
    }

    /// <summary>Shows a large letter over the grid for a moment.</summary>
    public void ShowLetter(string letter)
    {
        _letter.Text = letter;
        _letterBox.Visible = true;
        _letterBox.Modulate = Colors.White;
        _letterTween?.Kill();
        _letterTween = CreateTween();
        _letterTween.TweenInterval(LetterSeconds);
        _letterTween.TweenProperty(_letterBox, "modulate:a", 0f, 0.25);
        _letterTween.TweenCallback(Callable.From(() => _letterBox.Visible = false));
    }

    private void BuildLetter()
    {
        _letter = Ui.Label("", 120);
        _letter.HorizontalAlignment = HorizontalAlignment.Center;
        _letter.CustomMinimumSize = new Vector2(150, 0);
        var panel = Ui.MicaPanel(_letter, 36, 12);
        panel.MouseFilter = MouseFilterEnum.Ignore;
        var holder = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        holder.AddChild(panel);
        AddChild(holder);
        Ui.Fill(holder);
        _letterBox = holder;
    }

    private void OnCardFocused(Card card, int index)
    {
        Link(card, index);
        CardFocused?.Invoke(card);
        if (index >= Cards.Count - NearEndCards) { NearEnd?.Invoke(); }
    }

    /// <summary>
    /// Points Up and Down from a card at the cards above and below. A row scrolled out of view is
    /// clipped, and Godot's own search for a focus neighbour skips clipped controls. Down from
    /// above a short last row lands on its last card.
    /// </summary>
    private void Link(Card card, int index)
    {
        var columns = Columns;
        var below = index + columns;
        var lastRowStart = (Cards.Count - 1) / columns * columns;
        if (below >= Cards.Count) { below = index < lastRowStart ? Cards.Count - 1 : index; }
        card.FocusNeighborTop = Cards[index >= columns ? index - columns : index].GetPath();
        card.FocusNeighborBottom = Cards[below].GetPath();
    }

    private void UpdateImages()
    {
        if (!LoadsImagesNearView || !IsInsideTree()) { return; }
        var view = _scroll.GetGlobalRect();
        var near = view.Grow(view.Size.Y * 1.5f);
        var far = view.Grow(view.Size.Y * 4f);
        foreach (var card in Cards)
        {
            var rect = card.GetGlobalRect();
            if (near.Intersects(rect)) { card.LoadImage(); }
            else if (!far.Intersects(rect)) { card.UnloadImage(); }
        }
    }
}
