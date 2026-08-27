namespace BlackjackBrawl;

// Display strings (name + description) for each UpgradeType, shown
// when the player is offered a choice of upgrades.
static class UpgradeInfo
{
    // Short display name for an upgrade, shown in menus and pickup messages.
    public static string NameOf(UpgradeType t) => t switch
    {
        UpgradeType.Shield => "Shield",
        UpgradeType.Peek => "Card Peek",
        UpgradeType.DoubleDamage => "Empowered Strike",
        UpgradeType.Heal => "Field Medicine",
        UpgradeType.Reroll => "Second Chance",
        _ => t.ToString()
    };

    // One-line explanation of what an upgrade does, shown next to its name in the picker.
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
