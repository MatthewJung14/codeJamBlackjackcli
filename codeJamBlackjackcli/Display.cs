using System;
using System.Collections.Generic;

namespace BlackjackBrawl;

static class Display
{
    public const ConsoleColor Garnet = ConsoleColor.DarkRed;
    public const ConsoleColor Gold = ConsoleColor.Yellow;

    public static void WriteColored(string text, ConsoleColor color, bool newLine = true)
    {
        Console.ForegroundColor = color;
        if (newLine) Console.WriteLine(text);
        else Console.Write(text);
        Console.ResetColor();
    }

    public static void ClearScreen()
    {
        try { Console.Clear(); } catch { /* redirected output, ignore */ }
    }

    public static void Beat(int ms = 350) => System.Threading.Thread.Sleep(ms);

    // ---------- Card art ----------

    private static string[] CardLines(Card card, bool faceDown)
    {
        if (faceDown)
        {
            return new[]
            {
                "┌─────┐",
                "│░░░░░│",
                "│░░░░░│",
                "│░░░░░│",
                "└─────┘",
            };
        }

        string rank = card.RankName;
        return new[]
        {
            "┌─────┐",
            $"│{rank,-2}   │",
            $"│  {card.SuitSymbol}  │",
            $"│   {rank,2}│",
            "└─────┘",
        };
    }

    public static void PrintHand(IReadOnlyList<Card> cards, bool hideLast = false)
    {
        var lines = new string[cards.Count][];
        var colors = new ConsoleColor[cards.Count];

        for (int i = 0; i < cards.Count; i++)
        {
            bool faceDown = hideLast && i == cards.Count - 1;
            lines[i] = CardLines(cards[i], faceDown);
            colors[i] = faceDown ? ConsoleColor.DarkGray
                : cards[i].IsRed ? ConsoleColor.Red
                : ConsoleColor.White;
        }

        for (int row = 0; row < 5; row++)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                WriteColored(lines[i][row], colors[i], newLine: false);
                Console.Write(" ");
            }
            Console.WriteLine();
        }
    }

    // ---------- HP bars ----------

    public static void PrintHpBar(string label, int hp, int maxHp, ConsoleColor color)
    {
        int clamped = Math.Clamp(hp, 0, maxHp);
        int filled = (int)Math.Round(20.0 * clamped / maxHp);
        string bar = new string('█', filled) + new string('░', 20 - filled);
        Console.Write($"{label,-6} HP: [");
        WriteColored(bar, color, newLine: false);
        Console.WriteLine($"] {clamped}/{maxHp}");
    }

    // ---------- Banners ----------

    public static void PrintTitle()
    {
        WriteColored(@"
 ____  _               _    _            _      ____                  _
| __ )| | __ _  ___ _  | | _(_) __ _  ___| | __ | __ ) _ __ __ ___      _| |
|  _ \| |/ _` |/ __| |/ / |/ / |/ _` |/ __| |/ / |  _ \| '__/ _` \ \ /\ / / |
| |_) | | (_| | (__|   <|   <| | (_| | (__|   <  | |_) | | | (_| |\ V  V /| |
|____/|_|\__,_|\___|_|\_\_|\_\_|\__,_|\___|_|\_\ |____/|_|  \__,_| \_/\_/ |_|
", ConsoleColor.Magenta);

        WriteColored("           vs. THE EVIL DEALER OF THE GARNET & GOLD", Garnet);
        Console.WriteLine();
        WriteColored(@"                 .-'---`-.
              ,'          `.
             /   O      O   \      ""Chop. Chop. Chop.""
            |      ____      |
             \    '----'    /       Every card he flips
              `.          ,'        bleeds garnet and gold.
                `--------'
", Gold);

        Console.WriteLine("You and the Dealer each start with HP instead of chips.");
        Console.WriteLine("Win hands to deal damage. Bust or lose hands and you take damage.");
        Console.WriteLine("Blackjacks deal double damage. First to 0 HP loses.\n");
    }

    private static readonly string[] Taunts =
    {
        "The dealer gives a slow tomahawk chop and grins.",
        "\"Garnet and gold don't fold,\" the dealer sneers.",
        "The dealer lets out a war chant as the cards fly.",
        "\"You're facing an unbeaten streak, mortal,\" the dealer warns.",
        "The dealer flips a spear-tipped card with a smirk.",
        "\"This deck bleeds garnet,\" the dealer mutters.",
        "The dealer's eyes flash gold as the shoe is cut.",
        "\"Nobody leaves Doak Campbell with a win,\" the dealer taunts.",
    };

    private static readonly Random TauntRng = new();

    public static void PrintTaunt()
    {
        WriteColored(Taunts[TauntRng.Next(Taunts.Length)], Garnet);
    }
}
