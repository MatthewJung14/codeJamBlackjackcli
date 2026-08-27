using System.Collections.Generic;
using System.Linq;

namespace BlackjackBrawl;

// A player's or dealer's set of held cards, with blackjack scoring
// (Ace reduction, bust/blackjack/soft-hand checks) built in.
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
