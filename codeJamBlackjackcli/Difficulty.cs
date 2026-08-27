namespace BlackjackBrawl;

enum Difficulty { Easy, Normal, Hard }

record DifficultySettings(
    string Name,
    int StartingHp,
    double PlayerDamageMultiplier,
    double DealerDamageMultiplier,
    int DealerStandValue,
    bool DealerHitsSoftSeventeen,
    bool DealerCurse) // Hard mode: dealer periodically heals itself
{
    public static DifficultySettings For(Difficulty d) => d switch
    {
        Difficulty.Easy => new DifficultySettings(
            Name: "Easy",
            StartingHp: 55,
            PlayerDamageMultiplier: 1.25,
            DealerDamageMultiplier: 0.75,
            DealerStandValue: 17,
            DealerHitsSoftSeventeen: false,
            DealerCurse: false),

        Difficulty.Hard => new DifficultySettings(
            Name: "Nightmare",
            StartingHp: 40,
            PlayerDamageMultiplier: 0.9,
            DealerDamageMultiplier: 1.25,
            DealerStandValue: 17,
            DealerHitsSoftSeventeen: true,
            DealerCurse: true),

        _ => new DifficultySettings(
            Name: "Normal",
            StartingHp: 45,
            PlayerDamageMultiplier: 1.0,
            DealerDamageMultiplier: 1.0,
            DealerStandValue: 17,
            DealerHitsSoftSeventeen: false,
            DealerCurse: false),
    };
}
