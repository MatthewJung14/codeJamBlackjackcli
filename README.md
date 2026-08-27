# Blackjack Brawl

Standard blackjack rules, but instead of betting money you're betting HP against the Seminole Dealer.

## Libraries used

This is a .NET 10 console app with two NuGet package references (see [codeJamBlackjackcli.csproj](codeJamBlackjackcli/codeJamBlackjackcli.csproj)):

### [Spectre.Console](https://spectreconsole.net/) (v0.57.2)

All console rendering and user input goes through Spectre.Console instead of raw `Console.Write`/`Console.ReadLine`. Used in [Display.cs](codeJamBlackjackcli/Display.cs) and [Game.cs](codeJamBlackjackcli/Game.cs):

- **`AnsiConsole.MarkupLine` / `Markup`** — colored text output, e.g. `Display.Line` and the HP bar text. `Markup.Escape` is used so dynamic strings (taunts, damage messages) can't accidentally be interpreted as markup.
- **`Panel`** — bordered boxes for card faces, the dealer's speech-bubble taunts, and the title screen's gator/Seminole portraits.
- **`Grid` / `GridColumn`** — lays a hand's cards out side by side in a single row, and lays the two title-screen portrait panels out next to each other.
- **`Rule`** — the horizontal divider under the title banner ("vs. THE SEMINOLE DEALER").
- **`FigletText`** — the large ASCII-art "BLACKJACK BRAWL" logo on the title screen.
- **`SelectionPrompt<T>`** — every arrow-key menu: difficulty selection, Hit/Stand/Reroll, the dice-bet choice, the side-bet outcome choice, and the upgrade picker.
- **`TextPrompt<int>`** (with `.Validate`) — numeric entry with inline validation for the HP wager and the side-bet stake.
- **`AnsiConsole.Confirm`** — yes/no prompts for optional mini-games and side bets.

### [Spectre.Console.ImageSharp](https://spectreconsole.net/appendix/canvasimage) (v0.57.2)

Adds `CanvasImage`, used in `Display.PrintTitle()` to render `Assets/gator.jpg` and `Assets/seminole.jpg` directly in the terminal as true-color block art, instead of hand-drawn ASCII. This package pulls in **SixLabors.ImageSharp** as a transitive dependency, which does the actual image decoding — the game code never calls it directly.
