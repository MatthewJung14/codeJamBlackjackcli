using System;
using System.Collections.Generic;
using System.Linq;

namespace BlackjackBrawl;

enum PokerHandRank
{
    HighCard, Pair, TwoPair, ThreeOfAKind, Straight, Flush, FullHouse, FourOfAKind, StraightFlush
}

// Side gambles the dealer occasionally offers, independent of the main hand.
// Winning grants an upgrade instead of HP; losing costs the HP staked to play.
static class MiniGames
{
    public enum RewardTier { None, Common, Full, Premium }

    // ---------- Five-card poker ----------

    // Ranks a 5-card hand according to standard poker hand rankings.
    public static PokerHandRank EvaluatePokerHand(IReadOnlyList<Card> cards)
    {
        var rankCounts = cards.GroupBy(c => c.Rank).Select(g => g.Count()).OrderByDescending(c => c).ToList();
        bool isFlush = cards.Select(c => c.Suit).Distinct().Count() == 1;

        // Check straight both with Ace low (wheel: A-2-3-4-5) and Ace high (broadway: 10-J-Q-K-A).
        var rawRanks = cards.Select(c => c.Rank).Distinct().OrderBy(r => r).ToList();
        var highRanks = cards.Select(c => c.Rank == 1 ? 14 : c.Rank).Distinct().OrderBy(r => r).ToList();
        bool IsRun(List<int> ranks) => ranks.Count == 5 && ranks[^1] - ranks[0] == 4;
        bool isStraight = IsRun(rawRanks) || IsRun(highRanks);

        if (isStraight && isFlush) return PokerHandRank.StraightFlush;
        if (rankCounts[0] == 4) return PokerHandRank.FourOfAKind;
        if (rankCounts[0] == 3 && rankCounts.Count > 1 && rankCounts[1] == 2) return PokerHandRank.FullHouse;
        if (isFlush) return PokerHandRank.Flush;
        if (isStraight) return PokerHandRank.Straight;
        if (rankCounts[0] == 3) return PokerHandRank.ThreeOfAKind;
        if (rankCounts[0] == 2 && rankCounts.Count > 1 && rankCounts[1] == 2) return PokerHandRank.TwoPair;
        if (rankCounts[0] == 2) return PokerHandRank.Pair;
        return PokerHandRank.HighCard;
    }

    // Maps a poker hand's strength to the upgrade reward tier it pays out.
    public static RewardTier TierFor(PokerHandRank rank) => rank switch
    {
        PokerHandRank.HighCard => RewardTier.None,
        PokerHandRank.Pair => RewardTier.Common,
        PokerHandRank.TwoPair or PokerHandRank.ThreeOfAKind => RewardTier.Full,
        _ => RewardTier.Premium, // Straight or better
    };

    // Display name for a poker hand rank.
    public static string Describe(PokerHandRank rank) => rank switch
    {
        PokerHandRank.HighCard => "High Card",
        PokerHandRank.Pair => "Pair",
        PokerHandRank.TwoPair => "Two Pair",
        PokerHandRank.ThreeOfAKind => "Three of a Kind",
        PokerHandRank.Straight => "Straight",
        PokerHandRank.Flush => "Flush",
        PokerHandRank.FullHouse => "Full House",
        PokerHandRank.FourOfAKind => "Four of a Kind",
        PokerHandRank.StraightFlush => "Straight Flush",
        _ => rank.ToString(),
    };

    // ---------- Dice ----------

    public enum DiceBet { Under, Exactly, Over }

    // Rolls two six-sided dice.
    public static (int First, int Second) RollDice(Random rng) => (rng.Next(1, 7), rng.Next(1, 7));

    // Checks a dice bet against the rolled total and returns the reward tier, if any.
    public static RewardTier TierForDiceResult(DiceBet bet, int total)
    {
        bool hit = bet switch
        {
            DiceBet.Under => total < 7,
            DiceBet.Over => total > 7,
            DiceBet.Exactly => total == 7,
            _ => false,
        };
        if (!hit) return RewardTier.None;
        return bet == DiceBet.Exactly ? RewardTier.Premium : RewardTier.Common;
    }

    // Display name for a dice bet option.
    public static string Describe(DiceBet bet) => bet switch
    {
        DiceBet.Under => "Under 7",
        DiceBet.Exactly => "Exactly 7 (rare, best reward)",
        DiceBet.Over => "Over 7",
        _ => bet.ToString(),
    };
}
