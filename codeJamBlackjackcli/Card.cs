namespace BlackjackBrawl;

// A single playing card: its rank and suit, plus the blackjack-specific
// values and display strings derived from them.
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

    // Short display form, e.g. "A♠" or "10♦".
    public override string ToString() => $"{RankName}{SuitSymbol}";
}
