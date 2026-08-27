using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackjackBrawl;

enum Suit { Hearts, Diamonds, Clubs, Spades }

record Card(int Rank, Suit Suit)
{
    // Rank: 1 = Ace, 2-10 = number cards, 11 = Jack, 12 = Queen, 13 = King
    public int BaseValue => Rank switch
    {
        1 => 11,               // Ace defaults high; Hand fixes this if it busts
        >= 11 and <= 13 => 10, // face cards
        _ => Rank
    };

    public string RankName => Rank switch
    {
        1 => "A",
        11 => "J",
        12 => "Q",
        13 => "K",
        _ => Rank.ToString()
    };

    public string SuitSymbol => Suit switch
    {
        Suit.Hearts => "♥",
        Suit.Diamonds => "♦",
        Suit.Clubs => "♣",
        Suit.Spades => "♠",
        _ => "?"
    };

    public bool IsRed => Suit is Suit.Hearts or Suit.Diamonds;

    public override string ToString() => $"{RankName}{SuitSymbol}";
}

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

class Hand
{
    public List<Card> Cards { get; } = new();

    public void Add(Card c) => Cards.Add(c);
    public void Clear() => Cards.Clear();

    public int Value
    {
        get
        {
            int total = Cards.Sum(c => c.BaseValue);
            int aceCount = Cards.Count(c => c.Rank == 1);

            // Reduce Aces from 11 to 1 as needed to avoid busting
            while (total > 21 && aceCount > 0)
            {
                total -= 10;
                aceCount--;
            }
            return total;
        }
    }

    // A "soft" hand is one currently counting an Ace as 11 (standard soft-17 rule).
    public bool IsSoft
    {
        get
        {
            int totalLow = Cards.Sum(c => c.Rank == 1 ? 1 : c.BaseValue);
            bool hasAce = Cards.Any(c => c.Rank == 1);
            return hasAce && totalLow + 10 == Value;
        }
    }

    public bool IsBust => Value > 21;
    public bool IsBlackjack => Cards.Count == 2 && Value == 21;

    public override string ToString() => string.Join(" ", Cards) + $"  (={Value})";
}
