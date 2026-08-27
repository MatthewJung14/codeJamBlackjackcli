namespace BlackjackBrawl;

// The player's persistent state across rounds: HP and how many
// charges of each upgrade they're currently holding.
class PlayerState
{
    public int Hp;
    public int ShieldCharges;
    public int PeekCharges;
    public int DoubleDamageCharges;
    public int RerollCharges;
    public int TotalWins;

    // Momentum tracking
    public int WinStreak;   // consecutive hands won, resets to 0 on any loss
    public int BustStreak;  // consecutive hands busted, resets to 0 on any non-bust
}
