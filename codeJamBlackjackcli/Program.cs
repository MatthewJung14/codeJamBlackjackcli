// Blackjack Brawl
// Standard blackjack rules, but instead of betting money you're betting HP.
// Beat the Evil Dealer of the Garnet & Gold to zero HP before it does the same to you.

namespace BlackjackBrawl;

// Entry point: starts a single Game.
class Program
{
    // Starts and runs a single game session.
    static void Main()
    {
        new Game().Run();
    }
}
