namespace Asteroids;

internal enum Language { English, German, French, Spanish }

internal enum CoinMode { FreePlay, OneCoinTwoPlays, OneCoinOnePlay, TwoCoinsOnePlay }

internal enum CoinDoorConfig { OneDenomination, TwoDenominations, Unused, ThreeDenominations }

/// <summary>Models the two toggle-switch banks documented in Figure 7 of the Asteroids
/// Operation, Maintenance and Service Manual (TM-143): an 8-position switch on the game PCB for
/// language/ships/coinage, and a 4-position switch that describes the coin door's mechanism
/// denominations. Toggle index 0 is "toggle 1" in the manual's numbering, index 7 is "toggle 8".
/// True = ON (the manual's self-test screen would print "0" for that position; OFF prints "1").</summary>
internal sealed class DipSwitches
{
    // Manual's suggested factory settings (Figure 7 photo caption): toggles 1,2,4-7 ON, 3 and 8 OFF.
    public bool[] Switch8 = { true, true, false, true, true, true, true, false };

    // Manual's default coin-door wiring: toggles 1 and 2 ON (single denomination, one counter).
    public bool[] Switch4 = { true, true, false, false };

    public Language Language => (Switch8[0], Switch8[1]) switch
    {
        (true, true) => Language.English,
        (true, false) => Language.German,
        (false, true) => Language.French,
        (false, false) => Language.Spanish,
    };

    public int CenterCoinMechMultiplier => Switch8[2] ? 1 : 2;

    public int ShipsPerGame => Switch8[3] ? 4 : 3;

    public int RightCoinMechMultiplier => (Switch8[4], Switch8[5]) switch
    {
        (true, true) => 1,
        (true, false) => 4,
        (false, true) => 5,
        (false, false) => 6,
    };

    public CoinMode CoinMode => (Switch8[6], Switch8[7]) switch
    {
        (true, true) => CoinMode.FreePlay,
        (true, false) => CoinMode.OneCoinTwoPlays,
        (false, true) => CoinMode.OneCoinOnePlay,
        (false, false) => CoinMode.TwoCoinsOnePlay,
    };

    public CoinDoorConfig CoinDoor => (Switch4[0], Switch4[1]) switch
    {
        (true, true) => CoinDoorConfig.OneDenomination,
        (false, true) => CoinDoorConfig.TwoDenominations,
        (true, false) => CoinDoorConfig.Unused,
        (false, false) => CoinDoorConfig.ThreeDenominations,
    };

    /// <summary>Credits granted for one pulse of the left coin mechanism - always the ×1 reference
    /// mechanism per the manual.</summary>
    public int LeftCoinCredits => CoinsToCredits(1);

    public int CenterCoinCredits => CoinsToCredits(CenterCoinMechMultiplier);

    public int RightCoinCredits => CoinsToCredits(RightCoinMechMultiplier);

    /// <summary>Converts a number of "coin units" (after the mechanism's own multiplier) into
    /// credits, honoring the coinage toggle (e.g. 2 Coins for 1 Play needs two units per credit).</summary>
    private int CoinsToCredits(int mechMultiplier)
    {
        return CoinMode switch
        {
            CoinMode.OneCoinTwoPlays => mechMultiplier * 2,
            CoinMode.TwoCoinsOnePlay => mechMultiplier / 2,
            _ => mechMultiplier, // 1 Coin for 1 Play (and Free Play, where credits aren't consulted)
        };
    }
}

/// <summary>Cabinet text in the four languages selectable via DIP toggles 1 and 2. The self-test /
/// setup screen itself always stays in English, exactly like the real self-test mode - only the
/// attract-mode and in-game messages a player sees are localized.</summary>
internal static class Strings
{
    private static readonly Dictionary<string, string[]> Table = new()
    {
        //                        English            German                  French                 Spanish
        ["PUSH_START"] = new[] { "PUSH START", "START DRÜCKEN", "APPUYEZ SUR START", "PULSE START" },
        ["GAME_OVER"] = new[] { "GAME OVER", "SPIEL ZU ENDE", "FIN DE PARTIE", "FIN DEL JUEGO" },
        ["PLAYER_1"] = new[] { "PLAYER 1", "SPIELER 1", "JOUEUR 1", "JUGADOR 1" },
        ["PLAYER_2"] = new[] { "PLAYER 2", "SPIELER 2", "JOUEUR 2", "JUGADOR 2" },
        ["HIGH_SCORE"] = new[] { "HIGH SCORE", "HÖCHSTPUNKTZAHL", "MEILLEUR SCORE", "PUNTUACION MAXIMA" },
        ["INSERT_COINS"] = new[] { "INSERT COINS", "MÜNZEN EINWERFEN", "INTRODUIRE DES PIECES", "INTRODUZCA MONEDAS" },
        ["CREDITS"] = new[] { "CREDITS", "GUTHABEN", "CREDITS", "CREDITOS" },
        ["FREE_PLAY"] = new[] { "FREE PLAY", "FREISPIEL", "PARTIE GRATUITE", "JUEGO GRATIS" },
        ["1_PLAYER"] = new[] { "1 PLAYER", "1 SPIELER", "1 JOUEUR", "1 JUGADOR" },
        ["2_PLAYERS"] = new[] { "2 PLAYERS", "2 SPIELER", "2 JOUEURS", "2 JUGADORES" },
        ["BONUS_SHIP"] = new[] { "BONUS SHIP AT", "BONUSSCHIFF BEI", "VAISSEAU BONUS A", "NAVE BONUS EN" },
    };

    public static string Get(string key, Language lang)
    {
        return Table.TryGetValue(key, out var arr) ? arr[(int)lang] : key;
    }
}
