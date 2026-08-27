using System;
using System.Collections.Generic;
using System.Linq;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace BlackjackBrawl;

static class Display
{
    public const string Garnet = "maroon";
    public const string Gold = "gold1";

    public static void Line(string text, string color)
        => AnsiConsole.MarkupLine($"[{color}]{Markup.Escape(text)}[/]");

    public static void ClearScreen() => AnsiConsole.Clear();

    public static void Beat(int ms = 350) => System.Threading.Thread.Sleep(ms);

    // ---------- Card art ----------

    private static IRenderable CardPanel(Card card, bool faceDown)
    {
        if (faceDown)
        {
            var back = new Markup("[grey]░░░[/]\n[grey]░░░[/]").Centered();
            return new Panel(back)
                .Border(BoxBorder.Rounded)
                .BorderStyle(new Style(Color.Grey))
                .Padding(1, 0, 1, 0);
        }

        string color = card.IsRed ? "red" : "white";
        var content = new Markup(
            $"[{color} bold]{card.RankName}[/]\n[{color}]{card.SuitSymbol}[/]").Centered();

        return new Panel(content)
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(card.IsRed ? Color.Red : Color.White))
            .Padding(1, 0, 1, 0);
    }

    public static void PrintHand(IReadOnlyList<Card> cards, bool hideLast = false)
    {
        var grid = new Grid();
        for (int i = 0; i < cards.Count; i++)
            grid.AddColumn(new GridColumn().NoWrap().PadRight(1));

        var panels = cards
            .Select((c, i) => CardPanel(c, hideLast && i == cards.Count - 1))
            .ToArray();

        grid.AddRow(panels);
        AnsiConsole.Write(grid);
    }

    // ---------- HP bars ----------

    public static void PrintHpBar(string label, int hp, int maxHp, string color)
    {
        int clamped = Math.Clamp(hp, 0, maxHp);
        int filled = (int)Math.Round(20.0 * clamped / maxHp);
        string bar = new string('█', filled) + new string('░', 20 - filled);
        AnsiConsole.MarkupLine($"[bold]{label,-6}[/] HP [[[{color}]{bar}[/]]] [bold]{clamped}[/]/{maxHp}");
    }

    // ---------- Banners ----------

    public static void PrintTitle()
    {
        AnsiConsole.Write(
            new FigletText("BLACKJACK BRAWL")
                .Centered()
                .Color(Color.Fuchsia));

        AnsiConsole.Write(new Rule($"[{Garnet} bold]vs. THE EVIL DEALER OF THE GARNET & GOLD[/]").RuleStyle(Garnet));

        var face = new Markup(
            $"[{Gold}]     .-'---`-.\n" +
            "  ,'          `.\n" +
            " /   O      O   \\      \"Chop. Chop. Chop.\"\n" +
            "|      ____      |\n" +
            " \\    '----'    /       Every card he flips\n" +
            "  `.          ,'        bleeds garnet and gold.\n" +
            $"    `--------'[/]");

        AnsiConsole.Write(new Panel(face).Border(BoxBorder.Heavy).BorderStyle(new Style(Color.Maroon)));

        AnsiConsole.MarkupLine("You and the Dealer each start with HP instead of chips.");
        AnsiConsole.MarkupLine("Win hands to deal damage. Bust or lose hands and you take damage.");
        AnsiConsole.MarkupLine("Blackjacks deal double damage. First to 0 HP loses.\n");
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
        string taunt = Taunts[TauntRng.Next(Taunts.Length)];
        var panel = new Panel(new Markup($"[{Gold}]{Markup.Escape(taunt)}[/]"))
            .Header($"[{Garnet} bold] Evil Dealer [/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Maroon));
        AnsiConsole.Write(panel);
    }
}
