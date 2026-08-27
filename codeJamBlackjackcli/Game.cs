using System;
using System.Linq;
using Spectre.Console;

namespace BlackjackBrawl;

class Game
{
    private readonly Deck _deck = new();
    private readonly Random _rng = new();
    private readonly PlayerState _player = new();
    private DifficultySettings _difficulty = DifficultySettings.For(Difficulty.Normal);

    private int _dealerHp;
    private int _maxHp;
    private int _round;

    // Set when the Peek upgrade auto-fires at the start of a round.
    private int? _peekedDealerCardValue;

    public void Run()
    {
        Display.PrintTitle();
        _difficulty = DifficultySettings.For(ChooseDifficulty());

        _maxHp = _difficulty.StartingHp;
        _player.Hp = _maxHp;
        _dealerHp = _maxHp;

        Console.WriteLine();
        Display.Line($"Difficulty: {_difficulty.Name}", "cyan");
        Display.Beat(600);

        while (_player.Hp > 0 && _dealerHp > 0)
        {
            _round++;
            PlayRound();

            if (_difficulty.DealerCurse && _dealerHp > 0 && _round % 4 == 0)
            {
                int healed = Math.Min(5, _maxHp - _dealerHp);
                if (healed > 0)
                {
                    _dealerHp += healed;
                    Display.Line($"\nThe dealer channels dark Seminole magic, healing {healed} HP!", Display.Garnet);
                }
            }
        }

        Console.WriteLine();
        if (_player.Hp <= 0)
            Display.Line("The Evil Dealer drains the last of your HP. GAME OVER.", "red");
        else
            Display.Line("The Evil Dealer collapses in a pile of cards. YOU WIN!", "green");

        Console.WriteLine($"\nSurvived {_round} round(s).");
    }

    private Difficulty ChooseDifficulty()
    {
        const string easy = "Easy      - you hit harder, the dealer hits softer";
        const string normal = "Normal    - standard rules, even fight";
        const string hard = "Nightmare - dealer hits soft 17, hits harder, and heals over time";

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"Choose your [{Display.Garnet} bold]difficulty[/]:")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(easy, normal, hard));

        return choice == easy ? Difficulty.Easy
            : choice == hard ? Difficulty.Hard
            : Difficulty.Normal;
    }

    private void PlayRound()
    {
        Display.ClearScreen();
        Display.Line($"===== Round {_round} =====", "cyan");
        Display.PrintHpBar("You", _player.Hp, _maxHp, "green");
        Display.PrintHpBar("Dealer", _dealerHp, _maxHp, Display.Garnet);
        Console.WriteLine();
        Display.PrintTaunt();
        Console.WriteLine();

        var player = new Hand();
        var dealer = new Hand();

        player.Add(_deck.Draw());
        dealer.Add(_deck.Draw());
        player.Add(_deck.Draw());
        dealer.Add(_deck.Draw());

        _peekedDealerCardValue = null;
        if (_player.PeekCharges > 0)
        {
            _player.PeekCharges--;
            _peekedDealerCardValue = dealer.Cards[1].BaseValue;
            Display.Line($"[Card Peek] The hidden dealer card is worth {_peekedDealerCardValue}.", "cyan");
        }

        Console.WriteLine("Your hand:");
        Display.PrintHand(player.Cards);
        Console.WriteLine("Dealer shows:");
        Display.PrintHand(dealer.Cards, hideLast: true);

        // Natural blackjack check
        if (player.IsBlackjack || dealer.IsBlackjack)
        {
            RevealDealer(dealer);
            ResolveRound(player, dealer);
            WaitForContinue();
            return;
        }

        // Player's turn
        bool hasRerolled = false;
        while (true)
        {
            bool canReroll = !hasRerolled && _player.RerollCharges > 0 && player.Cards.Count == 2;
            var choices = canReroll ? new[] { "Hit", "Stand", "Reroll" } : new[] { "Hit", "Stand" };
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Your move:")
                    .HighlightStyle(new Style(Color.Gold1))
                    .AddChoices(choices));

            if (action == "Hit")
            {
                var card = _deck.Draw();
                player.Add(card);
                Console.WriteLine($"You drew {card}.");
                Display.PrintHand(player.Cards);

                if (player.IsBust)
                {
                    Display.Line("You busted!", "red");
                    break;
                }
            }
            else if (action == "Stand")
            {
                break;
            }
            else // Reroll
            {
                _player.RerollCharges--;
                hasRerolled = true;
                player.Clear();
                player.Add(_deck.Draw());
                player.Add(_deck.Draw());
                Display.Line("[Second Chance] You discard your hand and redraw.", "cyan");
                Display.PrintHand(player.Cards);

                if (player.IsBlackjack)
                {
                    RevealDealer(dealer);
                    ResolveRound(player, dealer);
                    WaitForContinue();
                    return;
                }
            }
        }

        RevealDealer(dealer);

        // Dealer's turn (only draws if player didn't already bust)
        if (!player.IsBust)
        {
            while (DealerShouldHit(dealer))
            {
                var card = _deck.Draw();
                dealer.Add(card);
                Console.WriteLine($"Dealer draws {card}.");
                Display.PrintHand(dealer.Cards);
            }
        }

        ResolveRound(player, dealer);
        WaitForContinue();
    }

    private static void WaitForContinue()
    {
        Console.WriteLine();
        Console.Write("Press Enter to continue...");
        Console.ReadLine();
    }

    private bool DealerShouldHit(Hand dealer)
    {
        if (dealer.Value < _difficulty.DealerStandValue) return true;
        if (_difficulty.DealerHitsSoftSeventeen && dealer.Value == 17 && dealer.IsSoft) return true;
        return false;
    }

    private void RevealDealer(Hand dealer)
    {
        Display.Beat();
        Console.WriteLine("Dealer reveals:");
        Display.PrintHand(dealer.Cards);
    }

    private void ResolveRound(Hand player, Hand dealer)
    {
        int rawDamage;
        bool playerDealtDamage;
        string message;

        if (player.IsBust && dealer.IsBust)
        {
            Console.WriteLine("Both bust! No damage dealt.");
            return;
        }

        if (player.IsBust)
        {
            rawDamage = ScaleDamage(dealer.Value - player.Value, dealer.IsBlackjack);
            playerDealtDamage = false;
            message = $"You busted. The Evil Dealer hits you for {{0}} damage!";
        }
        else if (dealer.IsBust)
        {
            rawDamage = ScaleDamage(player.Value - dealer.Value, player.IsBlackjack);
            playerDealtDamage = true;
            message = $"Dealer busted! You strike for {{0}} damage!";
        }
        else if (player.Value > dealer.Value)
        {
            rawDamage = ScaleDamage(player.Value - dealer.Value, player.IsBlackjack);
            playerDealtDamage = true;
            message = player.IsBlackjack
                ? "BLACKJACK! You unleash a critical hit for {0} damage!"
                : "You win the hand! Dealer takes {0} damage!";
        }
        else if (dealer.Value > player.Value)
        {
            rawDamage = ScaleDamage(dealer.Value - player.Value, dealer.IsBlackjack);
            playerDealtDamage = false;
            message = dealer.IsBlackjack
                ? "Dealer hits BLACKJACK! You take a critical {0} damage!"
                : "Dealer wins the hand. You take {0} damage!";
        }
        else
        {
            Console.WriteLine("Push (tie). No damage dealt.");
            return;
        }

        ApplyDamage(rawDamage, playerDealtDamage, message);
        CheckForUpgradeOffer(playerDealtDamage);
    }

    private void ApplyDamage(int rawDamage, bool playerDealtDamage, string messageTemplate)
    {
        int damage;
        string color;

        if (playerDealtDamage)
        {
            damage = (int)Math.Round(rawDamage * _difficulty.PlayerDamageMultiplier);
            if (_player.DoubleDamageCharges > 0)
            {
                _player.DoubleDamageCharges--;
                damage = (int)Math.Round(damage * 1.5);
                Display.Line("[Empowered Strike] Your attack hits harder!", "cyan");
            }
            _dealerHp = Math.Clamp(_dealerHp - damage, 0, _maxHp);
            color = "green";
        }
        else
        {
            damage = (int)Math.Round(rawDamage * _difficulty.DealerDamageMultiplier);
            if (_player.ShieldCharges > 0)
            {
                _player.ShieldCharges--;
                Display.Line("[Shield] Your shield absorbs the entire blow!", "cyan");
                damage = 0;
            }
            _player.Hp = Math.Clamp(_player.Hp - damage, 0, _maxHp);
            color = "red";
        }

        Display.Line(string.Format(messageTemplate, damage), color);
    }

    // Margin of victory becomes damage, with a floor/ceiling so rounds never feel
    // pointless (too small) or instantly lethal (too big). Blackjack doubles it.
    private static int ScaleDamage(int margin, bool isBlackjack)
    {
        int baseDamage = Math.Clamp(Math.Abs(margin) + 5, 8, 25);
        return isBlackjack ? baseDamage * 2 : baseDamage;
    }

    private void CheckForUpgradeOffer(bool playerWonHand)
    {
        if (!playerWonHand) return;

        _player.TotalWins++;
        if (_player.TotalWins % 3 != 0) return;

        OfferUpgrade();
    }

    private void OfferUpgrade()
    {
        Console.WriteLine();

        var options = Enum.GetValues<UpgradeType>().OrderBy(_ => _rng.Next()).Take(3).ToList();
        var pick = AnsiConsole.Prompt(
            new SelectionPrompt<UpgradeType>()
                .Title($"[{Display.Gold} bold]*** {_player.TotalWins} wins! Choose an upgrade: ***[/]")
                .HighlightStyle(new Style(Color.Gold1))
                .UseConverter(u => $"{UpgradeInfo.NameOf(u)} - {UpgradeInfo.DescriptionOf(u)}")
                .AddChoices(options));

        ApplyUpgrade(pick);
    }

    private void ApplyUpgrade(UpgradeType upgrade)
    {
        switch (upgrade)
        {
            case UpgradeType.Shield:
                _player.ShieldCharges++;
                break;
            case UpgradeType.Peek:
                _player.PeekCharges++;
                break;
            case UpgradeType.DoubleDamage:
                _player.DoubleDamageCharges++;
                break;
            case UpgradeType.Reroll:
                _player.RerollCharges++;
                break;
            case UpgradeType.Heal:
                int healed = Math.Min(15, _maxHp - _player.Hp);
                _player.Hp += healed;
                Display.Line($"You patch yourself up for {healed} HP.", "green");
                break;
        }

        Display.Line($"Acquired: {UpgradeInfo.NameOf(upgrade)}!", "yellow");
    }
}
