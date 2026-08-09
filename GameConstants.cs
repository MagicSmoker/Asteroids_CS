using System.Numerics;

namespace Asteroids;

internal static class GameConstants
{
    // Bump on notable releases; log what changed in CHANGELOG.md alongside it.
    public const string Version = "1.1.0";

    // The real cabinet's Quadrascan X-Y monitor was a roughly 4:3 tube.
    public static readonly (int Width, int Height, string Label)[] Resolutions =
    {
        (760, 570, "SMALL"),
        (960, 720, "MEDIUM"),
        (1240, 930, "LARGE"),
    };
    public const int DefaultResolutionIndex = 1;

    public static int ScreenWidth { get; private set; } = Resolutions[DefaultResolutionIndex].Width;
    public static int ScreenHeight { get; private set; } = Resolutions[DefaultResolutionIndex].Height;
    public static Vector2 Center { get; private set; } = new(ScreenWidth / 2f, ScreenHeight / 2f);

    public static void ApplyResolution(int index)
    {
        var (w, h, _) = Resolutions[index];
        ScreenWidth = w;
        ScreenHeight = h;
        Center = new Vector2(w / 2f, h / 2f);
    }

    // Player ship - frictionless inertia, same as the original cabinet: nothing slows the ship
    // down except firing the thruster in another direction.
    public const float ShipRotationSpeed = 4.2f;   // rad/s
    public const float ShipThrust = 210f;          // px/s^2
    public const float ShipMaxSpeed = 340f;        // px/s
    public const float ShipRadius = 11f;
    public const float RespawnInvulnerable = 2.5f;
    public const float RespawnDelay = 1.3f;
    public const float HyperspaceCooldown = 0.7f;
    public const float HyperspaceDeathChance = 0.12f; // manual: "the ship may also explode on reentry"

    // Player weapon - the real board caps four live shots on screen at once.
    public const float BulletSpeed = 460f;
    public const float BulletLifetime = 0.85f;
    public const float FireCooldown = 0.18f;
    public const float BulletRadius = 2f;
    public const int MaxPlayerBullets = 4;

    // Asteroids
    public const float LargeAsteroidRadius = 42f;
    public const float MediumAsteroidRadius = 22f;
    public const float SmallAsteroidRadius = 11f;
    public const float LargeAsteroidSpeedMin = 18f;
    public const float LargeAsteroidSpeedMax = 42f;
    public const float MediumAsteroidSpeedMin = 40f;
    public const float MediumAsteroidSpeedMax = 80f;
    public const float SmallAsteroidSpeedMin = 75f;
    public const float SmallAsteroidSpeedMax = 130f;
    public const int ScoreLargeAsteroid = 20;
    public const int ScoreMediumAsteroid = 50;
    public const int ScoreSmallAsteroid = 100;

    // Wave sizes per the manual: "four large asteroids... six... eight... thereafter ten"
    public static readonly int[] WaveAsteroidCounts = { 4, 6, 8, 10 };

    // Flying saucers
    public const float LargeSaucerRadius = 20f;
    public const float SmallSaucerRadius = 11f;
    public const float LargeSaucerSpeed = 70f;
    public const float SmallSaucerSpeed = 100f;
    public const float SaucerBulletSpeed = 340f;
    public const float SaucerFireInterval = 1.35f;
    public const float SaucerSpawnMin = 12f;
    public const float SaucerSpawnMax = 22f;
    public const int ScoreLargeSaucer = 200;
    public const int ScoreSmallSaucer = 1000;

    public const int ExtraShipScore = 10000;
}
