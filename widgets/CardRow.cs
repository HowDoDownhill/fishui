using System;
using System.Collections.Generic;
using Godot;

namespace FishUI;

/// <summary>A titled row of cards that scrolls sideways to follow focus.</summary>
public partial class CardRow : VBoxContainer
{
    public List<Card> Cards { get; } = new();

    public string Title { get; }

    private Card _lastFocused;

    /// <summary>The card focus lands on when it enters this row: the one it left from, or the first.</summary>
    public Card Entry => IsInstanceValid(_lastFocused) ? _lastFocused : Cards[0];

    public CardRow() { }

    public CardRow(string title, IEnumerable<Card> cards, Action<Card> pressed)
    {
        Title = title;
        AddThemeConstantOverride("separation", 6);
        AddChild(Ui.Pad(Ui.Label(title, 30), Ui.Margin, 0, 0, 0));

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", 28);
        // Room above and below for the selected card's shadow to fade out inside the scroll area.
        var content = Ui.Pad(box, Ui.Margin, Card.ShadowAbove, Ui.Margin, Card.ShadowBelow);
        scroll.AddChild(content);
        AddChild(scroll);
        // The focused card keeps the page margin on both sides, so the first card returns to its starting spot.
        FocusScroller.KeepVisible(scroll, content, Ui.Margin, vertical: false);

        foreach (var card in cards)
        {
            card.Pressed += () => pressed?.Invoke(card);
            card.FocusEntered += () => _lastFocused = card;
            box.AddChild(card);
            Cards.Add(card);
        }
    }
}
