using System.Numerics;

namespace Asteroids;

internal static class Wrap
{
    public static void Position(ref Vector2 p)
    {
        if (p.X < 0) p.X += GameConstants.ScreenWidth;
        else if (p.X > GameConstants.ScreenWidth) p.X -= GameConstants.ScreenWidth;
        if (p.Y < 0) p.Y += GameConstants.ScreenHeight;
        else if (p.Y > GameConstants.ScreenHeight) p.Y -= GameConstants.ScreenHeight;
    }
}

internal sealed class Ship
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Angle;
    public bool Thrusting;
    public bool Alive = true;
    public float FireTimer;
    public float InvulnTimer;
    public float HyperspaceTimer;

    public Vector2 Forward => new(MathF.Sin(Angle), -MathF.Cos(Angle));
    public bool Invulnerable => InvulnTimer > 0f;
    public bool CanFire => Alive && FireTimer <= 0f;
    public bool CanHyperspace => Alive && HyperspaceTimer <= 0f;

    public void Reset(Vector2 pos)
    {
        Position = pos;
        Velocity = Vector2.Zero;
        Angle = 0f;
        Alive = true;
        FireTimer = 0f;
        HyperspaceTimer = 0f;
        InvulnTimer = GameConstants.RespawnInvulnerable;
    }

    /// <summary>No friction, matching the original cabinet: the ship coasts forever unless the
    /// thruster fires. Only a hard cap on top speed and the fire/hyperspace cooldown timers tick.</summary>
    public void Update(float dt, bool left, bool right, bool thrust)
    {
        if (!Alive) return;

        if (left) Angle -= GameConstants.ShipRotationSpeed * dt;
        if (right) Angle += GameConstants.ShipRotationSpeed * dt;

        Thrusting = thrust;
        if (thrust)
        {
            Velocity += Forward * GameConstants.ShipThrust * dt;
            float speed = Velocity.Length();
            if (speed > GameConstants.ShipMaxSpeed)
                Velocity = Velocity / speed * GameConstants.ShipMaxSpeed;
        }

        Position += Velocity * dt;
        Wrap.Position(ref Position);

        if (FireTimer > 0f) FireTimer -= dt;
        if (HyperspaceTimer > 0f) HyperspaceTimer -= dt;
        if (InvulnTimer > 0f) InvulnTimer -= dt;
    }

    public void Fire() => FireTimer = GameConstants.FireCooldown;

    /// <summary>Teleports to a random point per the manual's emergency-maneuver description; the
    /// caller rolls <see cref="GameConstants.HyperspaceDeathChance"/> separately for the "may also
    /// explode on reentry" risk.</summary>
    public void Hyperspace(Random rng)
    {
        Position = new Vector2((float)rng.NextDouble() * GameConstants.ScreenWidth, (float)rng.NextDouble() * GameConstants.ScreenHeight);
        Velocity = Vector2.Zero;
        HyperspaceTimer = GameConstants.HyperspaceCooldown;
    }
}

internal enum BulletOwner { Player, Saucer }

internal sealed class Bullet
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Life;
    public bool Dead;
    public readonly BulletOwner Owner;

    public Bullet(Vector2 pos, Vector2 vel, BulletOwner owner)
    {
        Position = pos;
        Velocity = vel;
        Owner = owner;
        Life = GameConstants.BulletLifetime;
    }

    public void Update(float dt)
    {
        Position += Velocity * dt;
        Wrap.Position(ref Position);
        Life -= dt;
        if (Life <= 0f) Dead = true;
    }
}

internal enum AsteroidSize { Large, Medium, Small }

/// <summary>A jagged rock rendered as an irregular closed polygon (a fixed per-instance radius
/// wobble around a circle) so it reads as hand-plotted vector art rather than a smooth circle,
/// exactly like the original cabinet's asteroids.</summary>
internal sealed class AsteroidObj
{
    public Vector2 Position;
    public Vector2 Velocity;
    public AsteroidSize Size;
    public float Radius;
    public float RotationAngle;
    public readonly float RotationSpeed;
    public readonly float[] Shape;
    public bool Dead;

    public AsteroidObj(Vector2 pos, Vector2 vel, AsteroidSize size, Random rng)
    {
        Position = pos;
        Velocity = vel;
        Size = size;
        Radius = size switch
        {
            AsteroidSize.Large => GameConstants.LargeAsteroidRadius,
            AsteroidSize.Medium => GameConstants.MediumAsteroidRadius,
            _ => GameConstants.SmallAsteroidRadius,
        };
        RotationSpeed = ((float)rng.NextDouble() * 2f - 1f) * 0.7f;

        const int points = 10;
        Shape = new float[points];
        for (int i = 0; i < points; i++)
            Shape[i] = 0.75f + (float)rng.NextDouble() * 0.5f;
    }

    public void Update(float dt)
    {
        Position += Velocity * dt;
        Wrap.Position(ref Position);
        RotationAngle += RotationSpeed * dt;
    }

    public int Score => Size switch
    {
        AsteroidSize.Large => GameConstants.ScoreLargeAsteroid,
        AsteroidSize.Medium => GameConstants.ScoreMediumAsteroid,
        _ => GameConstants.ScoreSmallAsteroid,
    };
}

internal enum SaucerSize { Large, Small }

internal sealed class Saucer
{
    public Vector2 Position;
    public Vector2 Velocity;
    public readonly SaucerSize Size;
    public float FireTimer;
    public float ZigzagTimer;

    public Saucer(Vector2 pos, Vector2 vel, SaucerSize size)
    {
        Position = pos;
        Velocity = vel;
        Size = size;
        FireTimer = GameConstants.SaucerFireInterval;
    }

    public float Radius => Size == SaucerSize.Large ? GameConstants.LargeSaucerRadius : GameConstants.SmallSaucerRadius;
    public int Score => Size == SaucerSize.Large ? GameConstants.ScoreLargeSaucer : GameConstants.ScoreSmallSaucer;

    public void Update(float dt)
    {
        Position += Velocity * dt;
        if (Position.Y < 0) Velocity.Y = MathF.Abs(Velocity.Y);
        if (Position.Y > GameConstants.ScreenHeight) Velocity.Y = -MathF.Abs(Velocity.Y);
        Position.Y = Math.Clamp(Position.Y, 0, GameConstants.ScreenHeight);
        FireTimer -= dt;
    }
}

internal enum ParticleKind { Spark, Dot }

internal sealed class Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Life;
    public float MaxLife;
    public Color ParticleColor;
    public ParticleKind Kind;

    public Particle(Vector2 pos, Vector2 vel, float life, Color color, ParticleKind kind = ParticleKind.Spark)
    {
        Position = pos;
        Velocity = vel;
        Life = life;
        MaxLife = life;
        ParticleColor = color;
        Kind = kind;
    }

    public bool Dead => Life <= 0f;

    public void Update(float dt)
    {
        Position += Velocity * dt;
        Wrap.Position(ref Position);
        Life -= dt;
    }

    public Color CurrentColor()
    {
        float t = Math.Clamp(Life / MaxLife, 0f, 1f);
        int a = (int)(ParticleColor.A * t);
        return Color.FromArgb(Math.Clamp(a, 0, 255), ParticleColor.R, ParticleColor.G, ParticleColor.B);
    }
}
