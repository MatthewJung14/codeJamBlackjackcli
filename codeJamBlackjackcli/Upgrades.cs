namespace BlackjackBrawl;

enum UpgradeType { Shield, Peek, DoubleDamage, Heal, Reroll }

static class UpgradeInfo
{
    public static string NameOf(UpgradeType t) => t switch
    {
        UpgradeType.Shield => "Shield",
        UpgradeType.Peek => "Card Peek",
        UpgradeType.DoubleDamage => "Empowered Strike",
        UpgradeType.Heal => "Field Medicine",
        UpgradeType.Reroll => "Second Chance",
        _ => t.ToString()
    };

    public static string DescriptionOf(UpgradeType t) => t switch
    {
        UpgradeType.Shield => "Completely absorb the next hit you'd take.",
        UpgradeType.Peek => "See the dealer's hidden card at the start of a round.",
        UpgradeType.DoubleDamage => "Your next winning strike deals 1.5x damage.",
        UpgradeType.Heal => "Instantly restore 15 HP.",
        UpgradeType.Reroll => "Discard your opening hand and redraw once.",
        _ => ""
    };
}

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
