namespace BlackjackBrawl;

// Modifiers that can spice up an individual round. Only one special event
// (a mini-game offer or a curse) is ever active in a given round.
enum CursedModifier
{
    RecklessRound,  // all damage this round (either direction) is doubled
    HighStakes,     // minimum wager this round is raised to half your current HP
    GlassCannon,    // Shield and Empowered Strike can't be used this round (charges are preserved)
    FortunesFavor,  // winning this round's hand also heals you 5 HP
}

enum SideBetOutcome { DealerBusts, PlayerBlackjack, Push }

record SideBet(SideBetOutcome Outcome, int Stake)
{
    public double Payout => Outcome switch
    {
        SideBetOutcome.DealerBusts => 1.5,
        SideBetOutcome.PlayerBlackjack => 5.0,
        SideBetOutcome.Push => 3.0,
        _ => 0,
    };

    public string Description => Outcome switch
    {
        SideBetOutcome.DealerBusts => "Dealer busts (1.5x payout)",
        SideBetOutcome.PlayerBlackjack => "You draw a blackjack (5x payout)",
        SideBetOutcome.Push => "Hand is a push/tie (3x payout)",
        _ => Outcome.ToString(),
    };

    // Checks whether the finished hands satisfy this side bet's condition.
    public bool Hit(Hand player, Hand dealer) => Outcome switch
    {
        SideBetOutcome.DealerBusts => dealer.IsBust,
        SideBetOutcome.PlayerBlackjack => player.IsBlackjack,
        SideBetOutcome.Push => !player.IsBust && !dealer.IsBust && player.Value == dealer.Value,
        _ => false,
    };
}
