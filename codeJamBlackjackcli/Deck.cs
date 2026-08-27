using System;
using System.Collections.Generic;

namespace BlackjackBrawl;

// A shuffled 52-card deck that deals one card at a time and
// reshuffles itself automatically before it runs out.
class Deck
{
    private readonly List<Card> _cards = new();
    private readonly Random _rng = new();

    public Deck() => Reset();

    public void Reset()
    {
        _cards.Clear();
        foreach (Suit suit in Enum.GetValues<Suit>())
            for (int rank = 1; rank <= 13; rank++)
                _cards.Add(new Card(rank, suit));

        Shuffle();
    }

    public void Shuffle()
    {
        // Fisher-Yates
        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
        }
    }

    public Card Draw()
    {
        if (_cards.Count < 10) Reset(); // reshuffle before running dry
        var card = _cards[^1];
        _cards.RemoveAt(_cards.Count - 1);
        return card;
    }
}
