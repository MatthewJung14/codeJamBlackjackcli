using System;
using System.Linq;

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
        Display.WriteColored($"Difficulty: {_difficulty.Name}", ConsoleColor.Cyan);
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
                    Display.WriteColored($"\nThe dealer channels dark Seminole magic, healing {healed} HP!", Display.Garnet);
                }
            }
        }

        Console.WriteLine();
        if (_player.Hp <= 0)
            Display.WriteColored("The Evil Dealer drains the last of your HP. GAME OVER.", ConsoleColor.Red);
        else
            Display.WriteColored("The Evil Dealer collapses in a pile of cards. YOU WIN!", ConsoleColor.Green);

        Console.WriteLine($"\nSurvived {_round} round(s).");
    }

    private Difficulty ChooseDifficulty()
    {
        Console.WriteLine("Choose your difficulty:");
        Console.WriteLine("  1) Easy      - you hit harder, the dealer hits softer");
        Console.WriteLine("  2) Normal    - standard rules, even fight");
        Console.WriteLine("  3) Nightmare - dealer hits soft 17, hits harder, and heals over time");

        while (true)
        {
            Console.Write("> ");
            switch (Console.ReadLine()?.Trim())
            {
                case "1": return Difficulty.Easy;
                case "2": return Difficulty.Normal;
                case "3": return Difficulty.Hard;
                default: Console.WriteLine("Enter 1, 2, or 3."); break;
            }
        }
    }

    private void PlayRound()
    {
        Display.ClearScreen();
        Display.WriteColored($"===== Round {_round} =====", ConsoleColor.Cyan);
        Display.PrintHpBar("You", _player.Hp, _maxHp, ConsoleColor.Green);
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
            Display.WriteColored($"[Card Peek] The hidden dealer card is worth {_peekedDealerCardValue}.", ConsoleColor.Cyan);
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
            Console.Write(canReroll ? "Hit, Stand, or Reroll? (h/s/r): " : "Hit or Stand? (h/s): ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (input == "h")
            {
                var card = _deck.Draw();
                player.Add(card);
                Console.WriteLine($"You drew {card}.");
                Display.PrintHand(player.Cards);

                if (player.IsBust)
                {
                    Display.WriteColored("You busted!", ConsoleColor.Red);
                    break;
                }
            }
            else if (input == "s")
            {
                break;
            }
            else if (input == "r" && canReroll)
            {
                _player.RerollCharges--;
                hasRerolled = true;
                player.Clear();
                player.Add(_deck.Draw());
                player.Add(_deck.Draw());
                Display.WriteColored("[Second Chance] You discard your hand and redraw.", ConsoleColor.Cyan);
                Display.PrintHand(player.Cards);

                if (player.IsBlackjack)
                {
                    RevealDealer(dealer);
                    ResolveRound(player, dealer);
                    WaitForContinue();
                    return;
                }
            }
            else
            {
                Console.WriteLine(canReroll ? "Type 'h' to hit, 's' to stand, or 'r' to reroll." : "Type 'h' to hit or 's' to stand.");
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
        ConsoleColor color;

        if (playerDealtDamage)
        {
            damage = (int)Math.Round(rawDamage * _difficulty.PlayerDamageMultiplier);
            if (_player.DoubleDamageCharges > 0)
            {
                _player.DoubleDamageCharges--;
                damage = (int)Math.Round(damage * 1.5);
                Display.WriteColored("[Empowered Strike] Your attack hits harder!", ConsoleColor.Cyan);
            }
            _dealerHp = Math.Clamp(_dealerHp - damage, 0, _maxHp);
            color = ConsoleColor.Green;
        }
        else
        {
            damage = (int)Math.Round(rawDamage * _difficulty.DealerDamageMultiplier);
            if (_player.ShieldCharges > 0)
            {
                _player.ShieldCharges--;
                Display.WriteColored("[Shield] Your shield absorbs the entire blow!", ConsoleColor.Cyan);
                damage = 0;
            }
            _player.Hp = Math.Clamp(_player.Hp - damage, 0, _maxHp);
            color = ConsoleColor.Red;
        }

        Display.WriteColored(string.Format(messageTemplate, damage), color);
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
        Display.WriteColored($"*** {_player.TotalWins} wins! Choose an upgrade: ***", ConsoleColor.Yellow);

        var options = Enum.GetValues<UpgradeType>().OrderBy(_ => _rng.Next()).Take(3).ToList();
        for (int i = 0; i < options.Count; i++)
        {
            Console.WriteLine($"  {i + 1}) {UpgradeInfo.NameOf(options[i])} - {UpgradeInfo.DescriptionOf(options[i])}");
        }

        while (true)
        {
            Console.Write("> ");
            if (int.TryParse(Console.ReadLine()?.Trim(), out int choice) && choice >= 1 && choice <= options.Count)
            {
                ApplyUpgrade(options[choice - 1]);
                return;
            }
            Console.WriteLine($"Enter a number from 1 to {options.Count}.");
        }
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
                Display.WriteColored($"You patch yourself up for {healed} HP.", ConsoleColor.Green);
                break;
        }

        Display.WriteColored($"Acquired: {UpgradeInfo.NameOf(upgrade)}!", ConsoleColor.Yellow);
    }
}
