using System;
using System.Linq;
using Spectre.Console;

namespace BlackjackBrawl;

// Orchestrates a full match: difficulty selection, the round loop,
// player/dealer turns, damage resolution, and upgrade offers.
class Game
{
    private readonly Deck _deck = new();
    private readonly Random _rng = new();
    private readonly PlayerState _player = new();
    private DifficultySettings _difficulty = DifficultySettings.For(Difficulty.Normal);
    private Difficulty _difficultyLevel;

    private int _dealerHp;
    private int _maxHp;
    private int _round;

    // Momentum: mirrors PlayerState.WinStreak, but for the dealer.
    private int _dealerWinStreak;

    // Enrage: permanent traits the dealer unlocks as its own HP drops.
    private bool _enragedAt50;
    private bool _enragedAt25;
    private bool _enrageSoft17Unlocked;
    private int _enrageDamageBonusPct;
    private bool _enrageHealBoostUnlocked;

    // At most one special event (mini-game offer or curse) fires per round.
    private CursedModifier? _activeCurse;

    private const double MiniGameChance = 0.20;   // ~1 in 5 rounds
    private const double CursedRoundChance = 0.15; // ~1 in 7 rounds
    private const int MiniGameStake = 8;

    private static readonly UpgradeType[] CommonUpgradePool = { UpgradeType.Peek, UpgradeType.Reroll };
    private static readonly UpgradeType[] PremiumUpgradePool = { UpgradeType.Shield, UpgradeType.DoubleDamage };
    private static readonly UpgradeType[] FullUpgradePool = Enum.GetValues<UpgradeType>();

    // Runs a full match from the title screen through round-by-round play
    // until either side's HP hits zero.
    public void Run()
    {
        Display.PrintTitle();
        _difficultyLevel = ChooseDifficulty();
        _difficulty = DifficultySettings.For(_difficultyLevel);

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

            if (_difficulty.DealerCurse && _dealerHp > 0)
            {
                int healInterval = _enrageHealBoostUnlocked ? 3 : 4;
                if (_round % healInterval == 0)
                {
                    int healAmount = _enrageHealBoostUnlocked ? 10 : 5;
                    Heal(ref _dealerHp, _maxHp, healAmount,
                        "\nThe dealer channels dark Seminole magic, healing {0} HP!", Display.Garnet);
                }
            }
        }

        Console.WriteLine();
        if (_player.Hp <= 0)
            Display.Line("The Seminole Dealer drains the last of your HP. GAME OVER.", "red");
        else
            Display.Line("The Seminole Dealer collapses in a pile of cards. YOU WIN!", "green");

        Console.WriteLine($"\nSurvived {_round} round(s).");
    }

    // Prompts the player to pick a difficulty level.
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

    // Plays one full round: status/taunt display, special events, wagering,
    // dealing, player and dealer turns, and resolving the outcome.
    private void PlayRound()
    {
        Display.ClearScreen();
        Display.Line($"===== Round {_round} =====", "cyan");
        Display.PrintHpBar("You", _player.Hp, _maxHp, "green");
        Display.PrintHpBar("Dealer", _dealerHp, _maxHp, Display.Garnet);

        if (_player.WinStreak > 0)
            Display.Line($"On fire! {_player.WinStreak}-win streak (+{10 * Math.Min(_player.WinStreak, 3)}% damage dealt).", Display.Gold);
        if (_dealerWinStreak > 0)
            Display.Line($"The dealer is on a {_dealerWinStreak}-win streak (+{10 * Math.Min(_dealerWinStreak, 3)}% damage taken).", Display.Garnet);
        if (_player.BustStreak >= 2)
            Display.Line("You're rattled from back-to-back busts. The dealer hits harder while you're tilted.", Display.Garnet);

        Console.WriteLine();
        Display.PrintTaunt();
        Console.WriteLine();

        RollSpecialEvent();

        // A mini-game loss can drain the player's HP to 0 before the hand is
        // even dealt - if that happened, skip straight to the game-over check
        // in Run() instead of playing out a hand with no HP left to wager.
        if (_player.Hp <= 0)
        {
            WaitForContinue();
            return;
        }

        int wager = ChooseWager();
        bool isAllIn = wager >= _player.Hp;
        Display.Line(isAllIn
            ? $"*** ALL IN! You wager your entire {wager} HP for a bigger payoff! ***"
            : $"You wager {wager} HP this round.", "cyan");

        var sideBet = MaybeOfferSideBet();
        Console.WriteLine();

        var player = new Hand();
        var dealer = new Hand();

        player.Add(_deck.Draw());
        dealer.Add(_deck.Draw());
        player.Add(_deck.Draw());
        dealer.Add(_deck.Draw());

        // Review note: this was previously a class field (_peekedDealerCardValue),
        // but it's only ever set and read right here within PlayRound, so it
        // doesn't need to persist across method calls or rounds. Making it a
        // local variable makes that scope clearer at a glance.
        if (_player.PeekCharges > 0)
        {
            _player.PeekCharges--;
            int peekedDealerCardValue = dealer.Cards[1].BaseValue;
            Display.Line($"[Card Peek] The hidden dealer card is worth {peekedDealerCardValue}.", "cyan");
        }

        Display.Line("Your hand:", "white");
        Display.PrintHand(player.Cards);
        Display.Line("Dealer shows:", "red");
        Display.PrintHand(dealer.Cards, hideLast: true);

        // Natural blackjack check
        if (player.IsBlackjack || dealer.IsBlackjack)
        {
            RevealDealer(dealer);
            ResolveRound(player, dealer, wager, isAllIn);
            ResolveSideBet(sideBet, player, dealer);
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
                    ResolveRound(player, dealer, wager, isAllIn);
                    ResolveSideBet(sideBet, player, dealer);
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

        ResolveRound(player, dealer, wager, isAllIn);
        ResolveSideBet(sideBet, player, dealer);
        WaitForContinue();
    }

    // Prompts for how much HP to wager this round, respecting the
    // High Stakes curse's minimum when it's active.
    private int ChooseWager()
    {
        int minWager = _activeCurse == CursedModifier.HighStakes
            ? Math.Min(_player.Hp, Math.Max(1, (int)Math.Ceiling(_player.Hp * 0.5)))
            : 1;
        int maxWager = Math.Max(minWager, _player.Hp);

        if (minWager == maxWager) return maxWager;

        return AnsiConsole.Prompt(
            new TextPrompt<int>($"How much HP do you want to wager? ({minWager}-{maxWager})")
                .Validate(w => w >= minWager && w <= maxWager
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"Enter a value between {minWager} and {maxWager}.")));
    }

    // Pauses at the end of a round until the player presses Enter.
    private static void WaitForContinue()
    {
        Console.WriteLine();
        Console.Write("Press Enter to continue...");
        Console.ReadLine();
    }

    // Decides whether the dealer takes another card, honoring the
    // configured stand value and any soft-17 rule (base or enrage-unlocked).
    private bool DealerShouldHit(Hand dealer)
    {
        if (dealer.Value < _difficulty.DealerStandValue) return true;
        if ((_difficulty.DealerHitsSoftSeventeen || _enrageSoft17Unlocked) && dealer.Value == 17 && dealer.IsSoft) return true;
        return false;
    }

    // Shows the dealer's full hand after the player's turn ends.
    private void RevealDealer(Hand dealer)
    {
        Display.Beat();
        Console.WriteLine("Dealer reveals:");
        Display.PrintHand(dealer.Cards);
    }

    // Determines who won the hand (bust/value/push), updates win/bust
    // streaks, and hands off to ApplyDamage for the actual HP change.
    //
    // Review note: previously this method built a message string containing a
    // literal "{0}" placeholder, then passed it into ApplyDamage, which filled
    // it in with string.Format once the final (post-multiplier) damage number
    // was known. That works, but it means the message text and the number that
    // fills it are assembled in two different methods, so you have to read both
    // to see how a line like "You take 12 damage!" actually gets built. Below,
    // ApplyDamage now just returns the computed damage, and the message is
    // built here in one place, right where the outcome (win/loss/blackjack) is
    // already being decided.
    private void ResolveRound(Hand player, Hand dealer, int wager, bool isAllIn)
    {
        // Bust-streak tracking is independent of who wins the hand.
        _player.BustStreak = player.IsBust ? _player.BustStreak + 1 : 0;

        int rawDamage;
        bool playerDealtDamage;
        bool isCriticalHit; // true when the damage-dealing side won with a blackjack

        if (player.IsBust && dealer.IsBust)
        {
            Console.WriteLine("Both bust! No damage dealt.");
            return;
        }

        if (player.IsBust)
        {
            rawDamage = ScaleDamage(wager, dealer.IsBlackjack);
            playerDealtDamage = false;
            isCriticalHit = false;
        }
        else if (dealer.IsBust)
        {
            rawDamage = ScaleDamage(wager, player.IsBlackjack);
            playerDealtDamage = true;
            isCriticalHit = false;
        }
        else if (player.Value > dealer.Value)
        {
            rawDamage = ScaleDamage(wager, player.IsBlackjack);
            playerDealtDamage = true;
            isCriticalHit = player.IsBlackjack;
        }
        else if (dealer.Value > player.Value)
        {
            rawDamage = ScaleDamage(wager, dealer.IsBlackjack);
            playerDealtDamage = false;
            isCriticalHit = dealer.IsBlackjack;
        }
        else
        {
            Console.WriteLine("Push (tie). No damage dealt.");
            return;
        }

        if (playerDealtDamage)
        {
            _player.WinStreak++;
            _dealerWinStreak = 0;
        }
        else
        {
            _dealerWinStreak++;
            _player.WinStreak = 0;
        }

        int damage = ApplyDamage(rawDamage, playerDealtDamage, isAllIn);
        string message = BuildResultMessage(player, dealer, playerDealtDamage, isCriticalHit, damage);
        Display.Line(message, playerDealtDamage ? "green" : "red");

        CheckForUpgradeOffer(playerDealtDamage);
        CheckEnrage();
    }

    // Builds the round-result line shown to the player, given the outcome and
    // the final (post-multiplier) damage amount.
    private static string BuildResultMessage(Hand player, Hand dealer, bool playerDealtDamage, bool isCriticalHit, int damage)
    {
        if (player.IsBust)
            return $"You busted. The Seminole Dealer hits you for {damage} damage!";
        if (dealer.IsBust)
            return $"Dealer busted! You strike for {damage} damage!";

        if (playerDealtDamage)
            return isCriticalHit
                ? $"BLACKJACK! You unleash a critical hit for {damage} damage!"
                : $"You win the hand! Dealer takes {damage} damage!";

        return isCriticalHit
            ? $"Dealer hits BLACKJACK! You take a critical {damage} damage!"
            : $"Dealer wins the hand. You take {damage} damage!";
    }

    // Applies all damage modifiers (momentum, all-in, curses, upgrades,
    // enrage, tilt) to the raw wager damage, updates HP accordingly, and
    // returns the final damage amount actually dealt.
    private int ApplyDamage(int rawDamage, bool playerDealtDamage, bool isAllIn)
    {
        int damage;

        if (playerDealtDamage)
        {
            double multiplier = _difficulty.PlayerDamageMultiplier * MomentumBonus(_player.WinStreak, desperate: _player.Hp <= _maxHp / 4);
            if (isAllIn) multiplier *= 1.5;
            if (_activeCurse == CursedModifier.RecklessRound) multiplier *= 2;
            damage = (int)Math.Round(rawDamage * multiplier);

            if (_player.DoubleDamageCharges > 0 && _activeCurse != CursedModifier.GlassCannon)
            {
                _player.DoubleDamageCharges--;
                damage = (int)Math.Round(damage * 1.5);
                Display.Line("[Empowered Strike] Your attack hits harder!", "cyan");
            }
            _dealerHp = Math.Clamp(_dealerHp - damage, 0, _maxHp);

            if (_activeCurse == CursedModifier.FortunesFavor)
            {
                Heal(ref _player.Hp, _maxHp, 5, "[Fortune's Favor] Victory heals you for {0} HP!", "green");
            }
        }
        else
        {
            double multiplier = _difficulty.DealerDamageMultiplier * MomentumBonus(_dealerWinStreak, desperate: false);
            if (_player.BustStreak >= 2) multiplier *= 1.15; // Tilt
            if (_enrageDamageBonusPct > 0) multiplier *= 1 + _enrageDamageBonusPct / 100.0;
            if (_activeCurse == CursedModifier.RecklessRound) multiplier *= 2;
            damage = (int)Math.Round(rawDamage * multiplier);

            if (_player.ShieldCharges > 0 && _activeCurse != CursedModifier.GlassCannon)
            {
                _player.ShieldCharges--;
                Display.Line("[Shield] Your shield absorbs the entire blow!", "cyan");
                damage = 0;
            }
            _player.Hp = Math.Clamp(_player.Hp - damage, 0, _maxHp);
        }

        return damage;
    }

    // Converts a win streak (and optional "desperate" low-HP state) into a damage multiplier.
    private static double MomentumBonus(int streak, bool desperate)
    {
        double bonus = 0.10 * Math.Min(streak, 3);
        if (desperate) bonus += 0.15;
        return 1 + bonus;
    }

    // Damage dealt equals whatever HP the player wagered going into the round.
    // A blackjack doubles it.
    private static int ScaleDamage(int wager, bool isBlackjack)
        => isBlackjack ? wager * 2 : wager;

    // Review note: this "clamp a heal to maxHp, apply it, print a message if
    // any healing actually happened" pattern showed up four separate times
    // (dealer self-heal in Run, Fortune's Favor, the mini-game premium bonus,
    // and the Heal upgrade). Pulling it into one helper means there's a
    // single place to change if the capping behavior ever needs to change.
    // Returns the amount actually healed, in case a caller needs it.
    private static int Heal(ref int hp, int maxHp, int amount, string messageTemplate, string color)
    {
        int healed = Math.Min(amount, maxHp - hp);
        if (healed > 0)
        {
            hp += healed;
            Display.Line(string.Format(messageTemplate, healed), color);
        }
        return healed;
    }

    // After a player win, grants a streak gift every 3rd consecutive win
    // and offers a regular upgrade every 3rd total win.
    private void CheckForUpgradeOffer(bool playerWonHand)
    {
        if (!playerWonHand) return;

        _player.TotalWins++;

        if (_player.WinStreak > 0 && _player.WinStreak % 3 == 0)
            GrantStreakGift();

        if (_player.TotalWins % 3 != 0) return;

        OfferUpgrade();
    }

    // Awards a free upgrade from the full pool as a reward for a 3-win streak.
    private void GrantStreakGift()
    {
        var upgrade = RandomUpgradeFrom(MiniGames.RewardTier.Full);
        Console.WriteLine();
        Display.Line($"[Momentum] {_player.WinStreak}-win streak! The crowd roars and gifts you a free upgrade!", Display.Gold);
        ApplyUpgrade(upgrade);
    }

    // ---------- Dealer enrage phases ----------

    // Triggers the dealer's 50%- and 25%-HP enrage phases the first time
    // its HP crosses each threshold.
    private void CheckEnrage()
    {
        if (_dealerHp <= 0) return;

        if (!_enragedAt50 && _dealerHp <= _maxHp / 2)
        {
            _enragedAt50 = true;
            TriggerEnrage(phase: 50);
        }
        if (!_enragedAt25 && _dealerHp <= _maxHp / 4)
        {
            _enragedAt25 = true;
            TriggerEnrage(phase: 25);
        }
    }

    // Enrage traits scale with difficulty: Easy barely notices, Nightmare escalates hardest.
    private void TriggerEnrage(int phase)
    {
        switch (_difficultyLevel)
        {
            case Difficulty.Easy:
                if (phase == 25)
                {
                    _enrageSoft17Unlocked = true;
                    Display.Line("\n[Enrage] Wounded, the dealer starts hitting soft 17s!", Display.Garnet);
                }
                break;

            case Difficulty.Hard:
                if (phase == 50)
                {
                    _enrageDamageBonusPct += 10;
                    Display.Line("\n[Enrage] Dark power surges through the dealer! (+10% damage)", Display.Garnet);
                }
                else
                {
                    _enrageHealBoostUnlocked = true;
                    Display.Line("\n[Enrage] The Seminole curse grows stronger, doubling the dealer's healing!", Display.Garnet);
                }
                break;

            default: // Normal
                if (phase == 50)
                {
                    _enrageSoft17Unlocked = true;
                    Display.Line("\n[Enrage] Growing desperate, the dealer starts hitting soft 17s!", Display.Garnet);
                }
                else
                {
                    _enrageDamageBonusPct += 10;
                    Display.Line("\n[Enrage] The dealer's strikes grow fiercer! (+10% damage)", Display.Garnet);
                }
                break;
        }
    }

    // ---------- Cursed rounds & mini-games (mutually exclusive per round) ----------

    // Randomly decides whether this round offers a mini-game or applies a
    // curse; at most one of the two can happen per round.
    private void RollSpecialEvent()
    {
        _activeCurse = null;
        if (_round <= 1) return;

        double roll = _rng.NextDouble();
        if (roll < MiniGameChance)
        {
            OfferMiniGame();
        }
        else if (roll < MiniGameChance + CursedRoundChance)
        {
            var pool = Enum.GetValues<CursedModifier>();
            _activeCurse = pool[_rng.Next(pool.Length)];
            AnnounceCurse(_activeCurse.Value);
        }
    }

    // Prints the flavor text explaining which curse is active this round.
    private void AnnounceCurse(CursedModifier curse)
    {
        string text = curse switch
        {
            CursedModifier.RecklessRound => "CURSED ROUND: Reckless Round! All damage this round is doubled, for better or worse.",
            CursedModifier.HighStakes => "CURSED ROUND: High Stakes! You must wager at least half your current HP this round.",
            CursedModifier.GlassCannon => "CURSED ROUND: Glass Cannon! Shield and Empowered Strike are disabled this round.",
            CursedModifier.FortunesFavor => "CURSED ROUND: Fortune's Favor! Winning this hand also heals you 5 HP.",
            _ => "",
        };
        Display.Line($"\n{text}", "purple");
    }

    // Offers the player an optional poker, dice, or high-low side wager,
    // staking HP for a chance at a free upgrade (and a bonus heal on a
    // premium result).
    private void OfferMiniGame()
    {
        int kind = _rng.Next(3);
        string pitch = kind switch
        {
            0 => "The dealer leans in: \"Care for a side wager? A quick five-card poker hand?\"",
            1 => "The dealer leans in: \"Care for a side wager? Roll the dice with me?\"",
            _ => "The dealer leans in: \"Care for a side wager? Call the next card, higher or lower?\"",
        };
        Console.WriteLine();
        Display.Line(pitch, Display.Gold);

        if (!AnsiConsole.Confirm("Play the mini-game?", false)) return;

        int stake = Math.Min(MiniGameStake, _player.Hp);
        var tier = kind switch
        {
            0 => PlayPokerMiniGame(),
            1 => PlayDiceMiniGame(),
            _ => PlayHighLowMiniGame(),
        };

        if (tier == MiniGames.RewardTier.None)
        {
            _player.Hp = Math.Clamp(_player.Hp - stake, 0, _maxHp);
            Display.Line($"No luck this time. You lose {stake} HP.", "red");
            return;
        }

        var upgrade = RandomUpgradeFrom(tier);
        Display.Line($"You win! Gained: {UpgradeInfo.NameOf(upgrade)}!", "green");

        if (tier == MiniGames.RewardTier.Premium)
        {
            Heal(ref _player.Hp, _maxHp, 5, "Incredible result! You also recover {0} HP.", "green");
        }

        ApplyUpgrade(upgrade);
    }

    // Deals a 5-card poker hand from a fresh deck and returns its reward tier.
    private MiniGames.RewardTier PlayPokerMiniGame()
    {
        var miniDeck = new Deck();
        var hand = new[] { miniDeck.Draw(), miniDeck.Draw(), miniDeck.Draw(), miniDeck.Draw(), miniDeck.Draw() };

        Console.WriteLine("Your poker hand:");
        Display.PrintHand(hand);

        var rank = MiniGames.EvaluatePokerHand(hand);
        Display.Line($"That's a {MiniGames.Describe(rank)}!", "cyan");
        return MiniGames.TierFor(rank);
    }

    // Lets the player pick an over/under/exact bet, rolls two dice, and returns the reward tier.
    private MiniGames.RewardTier PlayDiceMiniGame()
    {
        var bet = AnsiConsole.Prompt(
            new SelectionPrompt<MiniGames.DiceBet>()
                .Title("Bet on the roll:")
                .HighlightStyle(new Style(Color.Gold1))
                .UseConverter(MiniGames.Describe)
                .AddChoices(Enum.GetValues<MiniGames.DiceBet>()));

        var (d1, d2) = MiniGames.RollDice(_rng);
        int total = d1 + d2;
        Console.WriteLine($"You rolled {d1} + {d2} = {total}");

        return MiniGames.TierForDiceResult(bet, total);
    }

    // Streak-based higher/lower guessing game: each correct guess raises the
    // reward tier (Common -> Full -> Premium), and the player may bank it or
    // push their luck for a bigger one until they either cash out or miss.
    private MiniGames.RewardTier PlayHighLowMiniGame()
    {
        var tiers = new[] { MiniGames.RewardTier.Common, MiniGames.RewardTier.Full, MiniGames.RewardTier.Premium };
        var miniDeck = new Deck();
        var current = miniDeck.Draw();
        Console.WriteLine("Your card:");
        Display.PrintHand(new[] { current });

        for (int streak = 0; streak < tiers.Length; streak++)
        {
            var guess = AnsiConsole.Prompt(
                new SelectionPrompt<MiniGames.HighLowGuess>()
                    .Title($"Next card higher or lower than {current}?")
                    .HighlightStyle(new Style(Color.Gold1))
                    .UseConverter(MiniGames.Describe)
                    .AddChoices(Enum.GetValues<MiniGames.HighLowGuess>()));

            var next = miniDeck.Draw();
            Console.WriteLine($"Next card: {next}");

            if (!MiniGames.EvaluateHighLowGuess(guess, current.Rank, next.Rank))
            {
                Display.Line("Wrong call! The streak ends with nothing.", "red");
                return MiniGames.RewardTier.None;
            }

            Display.Line($"Correct! ({streak + 1} in a row)", "green");
            current = next;

            bool isLastStreak = streak == tiers.Length - 1;
            if (!isLastStreak && !AnsiConsole.Confirm($"Push your luck for a bigger prize? (declining banks the {tiers[streak]} reward now)", true))
                return tiers[streak];
        }

        return tiers[^1];
    }

    // Picks a random upgrade from the pool matching the given reward tier.
    private UpgradeType RandomUpgradeFrom(MiniGames.RewardTier tier) => tier switch
    {
        MiniGames.RewardTier.Common => CommonUpgradePool[_rng.Next(CommonUpgradePool.Length)],
        MiniGames.RewardTier.Premium => PremiumUpgradePool[_rng.Next(PremiumUpgradePool.Length)],
        _ => FullUpgradePool[_rng.Next(FullUpgradePool.Length)],
    };

    // ---------- Side bets ----------

    // Optionally lets the player place a side bet on this hand's outcome
    // before cards are dealt.
    private SideBet? MaybeOfferSideBet()
    {
        if (!AnsiConsole.Confirm("Place a side bet on this hand?", false)) return null;

        // Review note: previously built `new SideBet(o, 0).Description` here,
        // constructing a whole record with a placeholder 0 stake just to read
        // display text that doesn't actually depend on Stake. SideBet.DescriptionFor
        // avoids the throwaway object and the "why 0?" question at the call site.
        var outcome = AnsiConsole.Prompt(
            new SelectionPrompt<SideBetOutcome>()
                .Title("Bet on:")
                .HighlightStyle(new Style(Color.Gold1))
                .UseConverter(SideBet.DescriptionFor)
                .AddChoices(Enum.GetValues<SideBetOutcome>()));

        int maxStake = Math.Max(1, _player.Hp);
        int stake = AnsiConsole.Prompt(
            new TextPrompt<int>($"Side bet stake? (1-{maxStake})")
                .Validate(s => s >= 1 && s <= maxStake
                    ? ValidationResult.Success()
                    : ValidationResult.Error($"Enter a value between 1 and {maxStake}.")));

        return new SideBet(outcome, stake);
    }

    // Settles a placed side bet against the finished hands, dealing bonus
    // damage on a hit or costing the player HP on a miss.
    private void ResolveSideBet(SideBet? bet, Hand player, Hand dealer)
    {
        if (bet is not { } sideBet) return;

        if (sideBet.Hit(player, dealer))
        {
            int bonus = (int)Math.Round(sideBet.Stake * sideBet.Payout);
            _dealerHp = Math.Clamp(_dealerHp - bonus, 0, _maxHp);
            Display.Line($"[Side Bet] \"{sideBet.Description}\" hits! Extra {bonus} damage to the dealer!", "cyan");
        }
        else
        {
            _player.Hp = Math.Clamp(_player.Hp - sideBet.Stake, 0, _maxHp);
            Display.Line($"[Side Bet] \"{sideBet.Description}\" misses. You lose {sideBet.Stake} HP.", "red");
        }
    }

    // Presents 3 random upgrade choices and applies whichever the player picks.
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

    // Grants the effect of a single upgrade: adds a charge, or for Heal,
    // restores HP immediately.
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
                Heal(ref _player.Hp, _maxHp, 15, "You patch yourself up for {0} HP.", "green");
                break;
        }

        Display.Line($"Acquired: {UpgradeInfo.NameOf(upgrade)}!", "yellow");
    }
}