using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace Asteroids;

internal enum GameState { Attract, Setup, Playing, Dying, GameOver, HighScoreEntry }
internal enum GameMode { OnePlayer, TwoPlayer }
internal enum AttractPage { Demo, HighScores }
internal enum SetupRow { MainSwitches, CoinDoorSwitches }

internal sealed class PlayerSlot
{
    public int Score;
    public int Lives;
    public bool Done;
}

internal sealed class HighScoreEntryRecord
{
    public string Initials = "AAA";
    public int Score;
}

internal sealed class GameForm : Form
{
    private readonly HashSet<Keys> _keys = new();
    private readonly HashSet<Keys> _pressedThisFrame = new();
    private readonly Random _rng = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastElapsed;
    private float _animClock;

    private readonly DipSwitches _dip = new();
    private int _credits;

    private GameState _state = GameState.Attract;
    private float _stateTimer;
    private AttractPage _attractPage = AttractPage.Demo;
    private float _attractPageTimer;

    private GameMode _mode = GameMode.OnePlayer;
    private readonly PlayerSlot[] _players = { new(), new() };
    private int _activePlayer;
    private int _wave;

    private readonly Ship _ship = new();
    private readonly List<Bullet> _bullets = new();
    private readonly List<AsteroidObj> _asteroids = new();
    private readonly List<Particle> _particles = new();
    private Saucer? _saucer;
    private float _saucerSpawnTimer;
    private bool _beatToggle;
    private float _beatTimer;
    private readonly int[] _extraShipWatermark = { 0, 0 };

    // Demo-mode decorative asteroids drift behind the attract screen, same as the real cabinet.
    private readonly List<AsteroidObj> _demoAsteroids = new();

    private readonly List<HighScoreEntryRecord> _highScores = new();
    private int _pendingInitialsPlayer;
    private readonly int[] _initialChars = { 0, 0, 0 }; // 0-25 = A-Z, 26 = blank
    private int _initialsIndex;

    // Setup (DIP switch) screen navigation
    private SetupRow _setupRow = SetupRow.MainSwitches;
    private int _setupCol;
    private static readonly int[] DisplayOrder8 = { 7, 6, 5, 4, 3, 2, 1, 0 }; // toggle 8 .. toggle 1, left to right
    private static readonly int[] DisplayOrder4 = { 3, 2, 1, 0 };             // toggle 4 .. toggle 1, left to right
    private readonly RectangleF[] _switch8Rects = new RectangleF[8];
    private readonly RectangleF[] _switch4Rects = new RectangleF[4];

    private int _resolutionIndex = GameConstants.DefaultResolutionIndex;

    public GameForm()
    {
        Text = "Asteroids";
        ClientSize = new Size(GameConstants.ScreenWidth, GameConstants.ScreenHeight);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        DoubleBuffered = true;
        BackColor = Color.Black;
        KeyPreview = true;

        SeedHighScores();
        SpawnDemoAsteroids();

        KeyDown += (_, e) =>
        {
            bool isNewPress = !_keys.Contains(e.KeyCode);
            _keys.Add(e.KeyCode);
            if (isNewPress) _pressedThisFrame.Add(e.KeyCode);
        };
        KeyUp += (_, e) => _keys.Remove(e.KeyCode);
        MouseDown += (_, e) => HandleSetupClick(e.Location);

        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        Audio.Init();
    }

    private void SeedHighScores()
    {
        // A fresh cabinet's default table - round numbers, descending, like the factory table.
        string[] names = { "ATR", "JBP", "GTD", "SCT", "RSK", "EFB", "DLM", "PXQ", "WVN", "HYU" };
        for (int i = 0; i < names.Length; i++)
            _highScores.Add(new HighScoreEntryRecord { Initials = names[i], Score = 10000 - i * 900 });
    }

    private void Tick()
    {
        double now = _clock.Elapsed.TotalSeconds;
        float dt = (float)Math.Min(now - _lastElapsed, 1.0 / 20.0);
        _lastElapsed = now;
        _animClock += dt;

        Update(dt);
        Invalidate();
        _pressedThisFrame.Clear();
    }

    private bool KeyJustPressed(Keys k) => _pressedThisFrame.Contains(k);
    private bool Held(params Keys[] keys) => keys.Any(_keys.Contains);

    private void Update(float dt)
    {
        foreach (var a in _demoAsteroids) a.Update(dt);

        switch (_state)
        {
            case GameState.Attract:
                UpdateAttract(dt);
                break;
            case GameState.Setup:
                UpdateSetup();
                break;
            case GameState.Playing:
                UpdatePlaying(dt);
                break;
            case GameState.Dying:
                UpdateWorld(dt);
                _stateTimer -= dt;
                if (_stateTimer <= 0f) RespawnPlayer();
                break;
            case GameState.GameOver:
                _stateTimer -= dt;
                if (_stateTimer <= 0f) BeginHighScoreEntry();
                break;
            case GameState.HighScoreEntry:
                UpdateHighScoreEntry();
                break;
        }

        bool thrustHeld = _state == GameState.Playing && _ship.Alive && Held(Keys.Up, Keys.W);
        Audio.SetThrust(thrustHeld);
    }

    // ----------------------------------------------------------------- Attract / coins

    private void UpdateAttract(float dt)
    {
        _attractPageTimer += dt;
        if (_attractPageTimer > 8f)
        {
            _attractPageTimer = 0f;
            _attractPage = _attractPage == AttractPage.Demo ? AttractPage.HighScores : AttractPage.Demo;
        }

        if (KeyJustPressed(Keys.D5) || KeyJustPressed(Keys.NumPad5)) InsertCoin(_dip.LeftCoinCredits);
        if (KeyJustPressed(Keys.D6) || KeyJustPressed(Keys.NumPad6)) InsertCoin(_dip.CenterCoinCredits);
        if (KeyJustPressed(Keys.D7) || KeyJustPressed(Keys.NumPad7)) InsertCoin(_dip.RightCoinCredits);

        if (KeyJustPressed(Keys.O)) { _state = GameState.Setup; return; }
        if (KeyJustPressed(Keys.R))
        {
            _resolutionIndex = (_resolutionIndex + 1) % GameConstants.Resolutions.Length;
            ApplyResolution();
        }

        bool freePlay = _dip.CoinMode == CoinMode.FreePlay;
        if (KeyJustPressed(Keys.D1) || KeyJustPressed(Keys.NumPad1))
        {
            if (freePlay || _credits >= 1) { if (!freePlay) _credits -= 1; StartNewGame(GameMode.OnePlayer); }
        }
        else if (KeyJustPressed(Keys.D2) || KeyJustPressed(Keys.NumPad2))
        {
            if (freePlay || _credits >= 2) { if (!freePlay) _credits -= 2; StartNewGame(GameMode.TwoPlayer); }
        }
    }

    private void InsertCoin(int credits)
    {
        if (_dip.CoinMode == CoinMode.FreePlay) return;
        _credits += Math.Max(credits, 0);
    }

    private void ApplyResolution()
    {
        GameConstants.ApplyResolution(_resolutionIndex);
        ClientSize = new Size(GameConstants.ScreenWidth, GameConstants.ScreenHeight);
        _demoAsteroids.Clear();
        SpawnDemoAsteroids();
    }

    // ----------------------------------------------------------------- Setup / DIP switches

    private void UpdateSetup()
    {
        if (KeyJustPressed(Keys.O) || KeyJustPressed(Keys.Escape)) { _state = GameState.Attract; return; }

        if (KeyJustPressed(Keys.Up) || KeyJustPressed(Keys.Down))
        {
            _setupRow = _setupRow == SetupRow.MainSwitches ? SetupRow.CoinDoorSwitches : SetupRow.MainSwitches;
            _setupCol = 0;
        }

        int count = _setupRow == SetupRow.MainSwitches ? 8 : 4;
        if (KeyJustPressed(Keys.Left)) _setupCol = (_setupCol - 1 + count) % count;
        if (KeyJustPressed(Keys.Right)) _setupCol = (_setupCol + 1) % count;

        if (KeyJustPressed(Keys.Enter) || KeyJustPressed(Keys.Space)) ToggleCurrentSwitch();
    }

    private void ToggleCurrentSwitch()
    {
        if (_setupRow == SetupRow.MainSwitches)
        {
            int idx = DisplayOrder8[_setupCol];
            _dip.Switch8[idx] = !_dip.Switch8[idx];
        }
        else
        {
            int idx = DisplayOrder4[_setupCol];
            _dip.Switch4[idx] = !_dip.Switch4[idx];
        }
    }

    private void HandleSetupClick(Point loc)
    {
        if (_state != GameState.Setup) return;
        for (int i = 0; i < _switch8Rects.Length; i++)
        {
            if (_switch8Rects[i].Contains(loc))
            {
                _setupRow = SetupRow.MainSwitches;
                _setupCol = i;
                ToggleCurrentSwitch();
                return;
            }
        }
        for (int i = 0; i < _switch4Rects.Length; i++)
        {
            if (_switch4Rects[i].Contains(loc))
            {
                _setupRow = SetupRow.CoinDoorSwitches;
                _setupCol = i;
                ToggleCurrentSwitch();
                return;
            }
        }
    }

    // ----------------------------------------------------------------- Game flow

    private void StartNewGame(GameMode mode)
    {
        _mode = mode;
        int lives = _dip.ShipsPerGame;
        _players[0] = new PlayerSlot { Score = 0, Lives = lives, Done = false };
        _players[1] = mode == GameMode.TwoPlayer
            ? new PlayerSlot { Score = 0, Lives = lives, Done = false }
            : new PlayerSlot { Score = 0, Lives = 0, Done = true };
        _activePlayer = 0;
        _wave = 0;
        _extraShipWatermark[0] = 0;
        _extraShipWatermark[1] = 0;

        _bullets.Clear(); _particles.Clear();
        _saucer = null;
        _ship.Reset(GameConstants.Center);
        StartWave();
        _state = GameState.Playing;
    }

    private void StartWave()
    {
        int count = GameConstants.WaveAsteroidCounts[Math.Min(_wave, GameConstants.WaveAsteroidCounts.Length - 1)];
        _asteroids.Clear();
        for (int i = 0; i < count; i++)
            SpawnLargeAsteroidAwayFromCenter();
        _wave++;
        _saucerSpawnTimer = GameConstants.SaucerSpawnMin + (float)_rng.NextDouble() * (GameConstants.SaucerSpawnMax - GameConstants.SaucerSpawnMin);
    }

    private void SpawnLargeAsteroidAwayFromCenter()
    {
        Vector2 pos;
        do
        {
            pos = new Vector2((float)_rng.NextDouble() * GameConstants.ScreenWidth, (float)_rng.NextDouble() * GameConstants.ScreenHeight);
        } while (Vector2.Distance(pos, GameConstants.Center) < 130f);

        float ang = (float)_rng.NextDouble() * MathF.Tau;
        float spd = GameConstants.LargeAsteroidSpeedMin + (float)_rng.NextDouble() * (GameConstants.LargeAsteroidSpeedMax - GameConstants.LargeAsteroidSpeedMin);
        var vel = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * spd;
        _asteroids.Add(new AsteroidObj(pos, vel, AsteroidSize.Large, _rng));
    }

    private void SpawnDemoAsteroids()
    {
        for (int i = 0; i < 7; i++)
        {
            var pos = new Vector2((float)_rng.NextDouble() * GameConstants.ScreenWidth, (float)_rng.NextDouble() * GameConstants.ScreenHeight);
            float ang = (float)_rng.NextDouble() * MathF.Tau;
            float spd = 15f + (float)_rng.NextDouble() * 25f;
            var vel = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * spd;
            var size = (AsteroidSize)_rng.Next(3);
            _demoAsteroids.Add(new AsteroidObj(pos, vel, size, _rng));
        }
    }

    private void RespawnPlayer()
    {
        _ship.Reset(GameConstants.Center);
        _state = GameState.Playing;
    }

    private void UpdatePlaying(float dt)
    {
        bool left = Held(Keys.Left, Keys.A);
        bool right = Held(Keys.Right, Keys.D);
        bool thrust = Held(Keys.Up, Keys.W);
        bool fire = Held(Keys.Space);
        bool hyperspace = KeyJustPressed(Keys.Down) || KeyJustPressed(Keys.S) || KeyJustPressed(Keys.LShiftKey) || KeyJustPressed(Keys.RShiftKey);

        _ship.Update(dt, left, right, thrust);

        if (fire && _ship.CanFire && _bullets.Count(b => b.Owner == BulletOwner.Player) < GameConstants.MaxPlayerBullets)
        {
            _ship.Fire();
            _bullets.Add(new Bullet(_ship.Position + _ship.Forward * (GameConstants.ShipRadius + 3f), _ship.Forward * GameConstants.BulletSpeed, BulletOwner.Player));
            Audio.Play(Sfx.Fire, 0.5f);
        }

        if (hyperspace && _ship.CanHyperspace)
        {
            _ship.Hyperspace(_rng);
            Audio.Play(Sfx.Hyperspace, 0.6f);
            if (_rng.NextDouble() < GameConstants.HyperspaceDeathChance)
            {
                KillPlayer();
                return;
            }
        }

        UpdateWorld(dt);
        UpdateSaucerSpawning(dt);
        HandleCollisions();

        if (_asteroids.Count == 0 && _saucer == null) StartWave();
    }

    private void UpdateWorld(float dt)
    {
        foreach (var b in _bullets) b.Update(dt);
        _bullets.RemoveAll(b => b.Dead);

        foreach (var a in _asteroids) a.Update(dt);

        if (_saucer != null)
        {
            UpdateSaucerAi(dt);
            _saucer.Update(dt);
            if (_saucer.Position.X < -60f || _saucer.Position.X > GameConstants.ScreenWidth + 60f) _saucer = null;
        }

        foreach (var p in _particles) p.Update(dt);
        _particles.RemoveAll(p => p.Dead);

        // The manual's "background heartbeat" - two alternating low tones that quicken as the
        // field clears out, driven purely by how many asteroids/saucers remain.
        int threats = _asteroids.Count + (_saucer != null ? 1 : 0);
        float interval = Math.Clamp(0.15f + threats * 0.07f, 0.15f, 1.0f);
        _beatTimer -= dt;
        if (_state == GameState.Playing && _beatTimer <= 0f)
        {
            Audio.Play(_beatToggle ? Sfx.Beat1 : Sfx.Beat2, 0.5f);
            _beatToggle = !_beatToggle;
            _beatTimer = interval;
        }
    }

    private void UpdateSaucerSpawning(float dt)
    {
        if (_saucer != null) return;
        _saucerSpawnTimer -= dt;
        if (_saucerSpawnTimer <= 0f)
        {
            bool fromLeft = _rng.Next(2) == 0;
            // Higher score -> more likely to draw the accurate, dangerous small saucer.
            var size = _players[_activePlayer].Score > 8000 && _rng.NextDouble() < 0.6 ? SaucerSize.Small : SaucerSize.Large;
            float speed = size == SaucerSize.Large ? GameConstants.LargeSaucerSpeed : GameConstants.SmallSaucerSpeed;
            var pos = new Vector2(fromLeft ? -30f : GameConstants.ScreenWidth + 30f, (float)_rng.NextDouble() * GameConstants.ScreenHeight);
            var vel = new Vector2(fromLeft ? speed : -speed, 0f);
            _saucer = new Saucer(pos, vel, size);
            _saucerSpawnTimer = GameConstants.SaucerSpawnMin + (float)_rng.NextDouble() * (GameConstants.SaucerSpawnMax - GameConstants.SaucerSpawnMin);
        }
    }

    private void UpdateSaucerAi(float dt)
    {
        if (_saucer == null) return;

        _saucer.ZigzagTimer -= dt;
        if (_saucer.ZigzagTimer <= 0f)
        {
            _saucer.ZigzagTimer = 0.6f + (float)_rng.NextDouble() * 0.8f;
            float dir = MathF.Sign(_saucer.Velocity.X);
            _saucer.Velocity.Y = ((float)_rng.NextDouble() * 2f - 1f) * (dir != 0 ? Math.Abs(dir) : 1f) * 60f;
        }

        if (_saucer.FireTimer <= 0f && _ship.Alive)
        {
            _saucer.FireTimer = GameConstants.SaucerFireInterval;
            Vector2 dir;
            if (_saucer.Size == SaucerSize.Small)
            {
                // The manual: the small saucer "shoots more accurately."
                dir = Vector2.Normalize(_ship.Position - _saucer.Position);
                float spread = ((float)_rng.NextDouble() - 0.5f) * 0.12f;
                dir = RotateVec(dir, spread);
            }
            else
            {
                float ang = (float)_rng.NextDouble() * MathF.Tau;
                dir = new Vector2(MathF.Cos(ang), MathF.Sin(ang));
            }
            _bullets.Add(new Bullet(_saucer.Position, dir * GameConstants.SaucerBulletSpeed, BulletOwner.Saucer));
            Audio.Play(Sfx.SaucerFire, 0.4f);
        }
    }

    private static Vector2 RotateVec(Vector2 v, float ang)
    {
        float c = MathF.Cos(ang), s = MathF.Sin(ang);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    private void HandleCollisions()
    {
        var active = _players[_activePlayer];

        foreach (var b in _bullets)
        {
            if (b.Dead) continue;

            if (b.Owner == BulletOwner.Player)
            {
                foreach (var a in _asteroids)
                {
                    if (a.Dead) continue;
                    if (Vector2.DistanceSquared(b.Position, a.Position) <= MathF.Pow(a.Radius + GameConstants.BulletRadius, 2))
                    {
                        b.Dead = true;
                        SplitAsteroid(a, active);
                        break;
                    }
                }
                if (b.Dead) continue;

                if (_saucer != null && Vector2.DistanceSquared(b.Position, _saucer.Position) <= MathF.Pow(_saucer.Radius + GameConstants.BulletRadius, 2))
                {
                    b.Dead = true;
                    active.Score += _saucer.Score;
                    SpawnBurst(_saucer.Position, 16, Color.White, 40f, 140f, 0.3f, 0.6f);
                    Audio.Play(_saucer.Size == SaucerSize.Large ? Sfx.SaucerBig : Sfx.SaucerSmall, 0.5f);
                    _saucer = null;
                    MaybeAwardExtraShip(_activePlayer);
                }
            }
            else // saucer bullet
            {
                if (_ship.Alive && !_ship.Invulnerable && Vector2.DistanceSquared(b.Position, _ship.Position) <= MathF.Pow(GameConstants.ShipRadius + GameConstants.BulletRadius, 2))
                {
                    b.Dead = true;
                    KillPlayer();
                }
            }
        }
        _bullets.RemoveAll(b => b.Dead);

        if (_state != GameState.Playing || _ship.Invulnerable || !_ship.Alive) return;

        foreach (var a in _asteroids)
        {
            if (a.Dead) continue;
            if (Vector2.DistanceSquared(a.Position, _ship.Position) <= MathF.Pow(a.Radius + GameConstants.ShipRadius, 2))
            {
                KillPlayer();
                return;
            }
        }

        if (_saucer != null && Vector2.DistanceSquared(_saucer.Position, _ship.Position) <= MathF.Pow(_saucer.Radius + GameConstants.ShipRadius, 2))
        {
            KillPlayer();
        }
    }

    private void SplitAsteroid(AsteroidObj a, PlayerSlot active)
    {
        a.Dead = true;
        active.Score += a.Score;
        Audio.Play(a.Size switch { AsteroidSize.Large => Sfx.BangLarge, AsteroidSize.Medium => Sfx.BangMedium, _ => Sfx.BangSmall }, 0.6f);
        SpawnBurst(a.Position, a.Size == AsteroidSize.Large ? 14 : a.Size == AsteroidSize.Medium ? 9 : 5, Color.White, 30f, 120f, 0.25f, 0.55f);
        MaybeAwardExtraShip(_activePlayer);

        if (a.Size == AsteroidSize.Small) { _asteroids.Remove(a); return; }

        var childSize = a.Size == AsteroidSize.Large ? AsteroidSize.Medium : AsteroidSize.Small;
        for (int i = 0; i < 2; i++)
        {
            float ang = (float)_rng.NextDouble() * MathF.Tau;
            var (min, max) = childSize == AsteroidSize.Medium
                ? (GameConstants.MediumAsteroidSpeedMin, GameConstants.MediumAsteroidSpeedMax)
                : (GameConstants.SmallAsteroidSpeedMin, GameConstants.SmallAsteroidSpeedMax);
            float spd = min + (float)_rng.NextDouble() * (max - min);
            var vel = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * spd;
            _asteroids.Add(new AsteroidObj(a.Position, vel, childSize, _rng));
        }
        _asteroids.Remove(a);
    }

    /// <summary>"The game awards an extra ship each time a player's score reaches multiples of
    /// 10,000" - tracked via a per-player watermark so crossing several thousand-point jumps in
    /// one hit (e.g. a small saucer worth 1000) can't be missed or double-counted.</summary>
    private void MaybeAwardExtraShip(int playerIndex)
    {
        int milestonesReached = _players[playerIndex].Score / GameConstants.ExtraShipScore;
        if (milestonesReached <= _extraShipWatermark[playerIndex]) return;
        _extraShipWatermark[playerIndex] = milestonesReached;
        _players[playerIndex].Lives++;
        Audio.Play(Sfx.ExtraShip, 0.6f);
    }

    private void KillPlayer()
    {
        SpawnBurst(_ship.Position, 20, Color.White, 40f, 160f, 0.3f, 0.7f);
        Audio.Play(Sfx.ShipExplode, 0.8f);
        _ship.Alive = false;

        var active = _players[_activePlayer];
        active.Lives--;
        if (active.Lives <= 0) active.Done = true;

        int other = 1 - _activePlayer;
        if (_mode == GameMode.TwoPlayer && !_players[other].Done)
        {
            _activePlayer = other;
            _state = GameState.Dying;
            _stateTimer = GameConstants.RespawnDelay;
        }
        else if (!active.Done)
        {
            _state = GameState.Dying;
            _stateTimer = GameConstants.RespawnDelay;
        }
        else
        {
            _state = GameState.GameOver;
            _stateTimer = 3f;
        }
    }

    private void SpawnBurst(Vector2 pos, int count, Color color, float speedMin, float speedMax, float lifeMin, float lifeMax)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = (float)_rng.NextDouble() * MathF.Tau;
            float spd = speedMin + (float)_rng.NextDouble() * (speedMax - speedMin);
            var vel = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * spd;
            float life = lifeMin + (float)_rng.NextDouble() * (lifeMax - lifeMin);
            _particles.Add(new Particle(pos, vel, life, color));
        }
    }

    // ----------------------------------------------------------------- High score initials

    private void BeginHighScoreEntry()
    {
        _pendingInitialsPlayer = 0;
        StartInitialsFor(0);
    }

    private void StartInitialsFor(int playerIndex)
    {
        _pendingInitialsPlayer = playerIndex;
        _initialChars[0] = 0; _initialChars[1] = 0; _initialChars[2] = 0;
        _initialsIndex = 0;
        _state = GameState.HighScoreEntry;
    }

    private void UpdateHighScoreEntry()
    {
        if (KeyJustPressed(Keys.Right) || KeyJustPressed(Keys.D))
            _initialChars[_initialsIndex] = (_initialChars[_initialsIndex] + 1) % 27;
        if (KeyJustPressed(Keys.Left) || KeyJustPressed(Keys.A))
            _initialChars[_initialsIndex] = (_initialChars[_initialsIndex] + 26) % 27;

        if (KeyJustPressed(Keys.Space) || KeyJustPressed(Keys.Down) || KeyJustPressed(Keys.Enter))
        {
            _initialsIndex++;
            if (_initialsIndex >= 3)
            {
                CommitInitials();
                if (_mode == GameMode.TwoPlayer && _pendingInitialsPlayer == 0 && _players[1].Score > 0)
                    StartInitialsFor(1);
                else
                    _state = GameState.Attract;
            }
        }
    }

    private void CommitInitials()
    {
        string initials = new(_initialChars.Select(c => c == 26 ? ' ' : (char)('A' + c)).ToArray());
        int score = _players[_pendingInitialsPlayer].Score;
        _highScores.Add(new HighScoreEntryRecord { Initials = initials, Score = score });
        _highScores.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (_highScores.Count > 10) _highScores.RemoveRange(10, _highScores.Count - 10);
    }

    // ----------------------------------------------------------------- Rendering

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Black);

        switch (_state)
        {
            case GameState.Attract:
                DrawAttract(g);
                break;
            case GameState.Setup:
                DrawSetup(g);
                break;
            case GameState.Playing:
            case GameState.Dying:
                DrawPlayfield(g);
                DrawHud(g);
                break;
            case GameState.GameOver:
                DrawPlayfield(g);
                DrawHud(g);
                DrawCenteredOverlay(g, Strings.Get("GAME_OVER", _dip.Language), "", "");
                break;
            case GameState.HighScoreEntry:
                DrawHighScoreEntry(g);
                break;
        }
    }

    private void DrawPlayfield(Graphics g)
    {
        foreach (var a in _asteroids) DrawAsteroid(g, a);
        DrawParticles(g);
        if (_saucer != null) DrawSaucer(g, _saucer);
        DrawBullets(g);
        DrawShip(g, _ship);
    }

    private void DrawAttract(Graphics g)
    {
        foreach (var a in _demoAsteroids) DrawAsteroid(g, a);

        using var titleFont = new Font("Consolas", 36f, FontStyle.Bold);
        using var subFont = new Font("Consolas", 13f);
        using var hintFont = new Font("Consolas", 11f);
        using var brush = new SolidBrush(Color.White);
        using var dimBrush = new SolidBrush(Color.FromArgb(255, 150, 150, 150));

        DrawWireTitle(g, "ASTEROIDS", GameConstants.Center.X, 90f, 40f);

        if (_attractPage == AttractPage.Demo)
        {
            var lang = _dip.Language;
            DrawCenteredString(g, Strings.Get("PUSH_START", lang), subFont, brush, 170f);

            float y = GameConstants.ScreenHeight - 150f;
            bool freePlay = _dip.CoinMode == CoinMode.FreePlay;
            if (freePlay)
            {
                DrawCenteredString(g, Strings.Get("FREE_PLAY", lang), subFont, brush, y);
            }
            else
            {
                DrawCenteredString(g, $"{Strings.Get("CREDITS", lang)}: {_credits}", subFont, brush, y);
                DrawCenteredString(g, "5/6/7 = INSERT COIN (LEFT/CENTER/RIGHT)", hintFont, dimBrush, y + 22);
            }
            DrawCenteredString(g, $"1 = {Strings.Get("1_PLAYER", lang)}    2 = {Strings.Get("2_PLAYERS", lang)}", subFont, brush, y + 48);
            DrawCenteredString(g, "O = GAME OPTIONS (DIP SWITCHES)   •   R = RESOLUTION", hintFont, dimBrush, y + 74);
        }
        else
        {
            using var hsFont = new Font("Consolas", 15f, FontStyle.Bold);
            DrawCenteredString(g, Strings.Get("HIGH_SCORE", _dip.Language) + " TABLE", subFont, brush, 160f);
            float y = 210f;
            for (int i = 0; i < _highScores.Count; i++)
            {
                var rec = _highScores[i];
                string line = $"{i + 1,2}.  {rec.Initials,-4} {rec.Score,6}";
                DrawCenteredString(g, line, hsFont, i == 0 ? brush : dimBrush, y);
                y += 26f;
            }
        }

        DrawCenteredString(g, "COPYRIGHT 1979 ATARI   •   C# VERSION BY JIMMY IPOCK", hintFont, dimBrush, GameConstants.ScreenHeight - 34f);

        string versionText = $"v{GameConstants.Version}";
        using var vFont = hintFont;
        var vSize = g.MeasureString(versionText, vFont);
        g.DrawString(versionText, vFont, dimBrush, GameConstants.ScreenWidth - vSize.Width - 10, GameConstants.ScreenHeight - vSize.Height - 8);
    }

    /// <summary>The start page's centerpiece: a vector rendering of the physical 8-toggle and
    /// 4-toggle DIP switch banks from Figure 7 of the service manual, laid out and numbered the
    /// same way (toggle 8 at the left down to toggle 1 at the right), with the "0 = ON / 1 = OFF"
    /// self-test readout row underneath and the decoded, plain-English option text beside each
    /// group - exactly the information a technician would read off the real cabinet.</summary>
    // Vertical space each switch bank reserves above its rockers (for the selection arrow and the
    // "ON" caption) and below them (for the "OFF" caption and the toggle number) - keeping these
    // fixed lets the whole screen be laid out as a simple top-down cursor with no overlap.
    private const float SwitchWidth = 34f;
    private const float SwitchHeight = 42f;
    private const float SwitchColumnPitch = 56f;
    private const float SwitchHeaderReserve = 34f;
    private const float SwitchFooterReserve = 34f;

    private void DrawSetup(Graphics g)
    {
        using var titleFont = new Font("Consolas", 22f, FontStyle.Bold);
        using var groupFont = new Font("Consolas", 13f, FontStyle.Bold);
        using var labelFont = new Font("Consolas", 10f, FontStyle.Bold);
        using var optionFont = new Font("Consolas", 12f);
        using var hintFont = new Font("Consolas", 11f);
        using var brush = new SolidBrush(Color.White);
        using var dimBrush = new SolidBrush(Color.FromArgb(255, 140, 140, 140));
        using var selBrush = new SolidBrush(Color.FromArgb(255, 255, 210, 90));

        float y = 26f;
        DrawCenteredString(g, "GAME OPTIONS", titleFont, brush, y); y += 34f;
        DrawCenteredString(g, "(SET LIKE THE ARCADE CABINET'S DIP SWITCHES - TM-143 FIG. 7)", hintFont, dimBrush, y); y += 30f;

        // --- 8-toggle switch bank: language, ships, coin mechanism multipliers, coinage. ---
        DrawCenteredString(g, "8-TOGGLE SWITCH (GAME PCB)", groupFont, brush, y); y += 24f;

        float bankWidth = 8 * SwitchColumnPitch;
        float bank8X = GameConstants.Center.X - bankWidth / 2f;
        float bank8Y = y + SwitchHeaderReserve;
        for (int col = 0; col < 8; col++)
        {
            int toggleNumber = DisplayOrder8[col] + 1;
            bool on = _dip.Switch8[DisplayOrder8[col]];
            bool selected = _state == GameState.Setup && _setupRow == SetupRow.MainSwitches && _setupCol == col;
            var rect = new RectangleF(bank8X + col * SwitchColumnPitch, bank8Y, SwitchWidth, SwitchHeight);
            _switch8Rects[col] = rect;
            DrawToggleSwitch(g, rect, on, toggleNumber.ToString(), selected, brush, dimBrush, selBrush, labelFont);
        }
        y = bank8Y + SwitchHeight + SwitchFooterReserve;

        var sb = new System.Text.StringBuilder();
        for (int col = 0; col < 8; col++) sb.Append(_dip.Switch8[DisplayOrder8[col]] ? "0   " : "1   ");
        DrawCenteredStringAt(g, sb.ToString().TrimEnd(), labelFont, dimBrush, bank8X, y, bankWidth); y += 26f;

        DrawOptionLine(g, optionFont, brush, dimBrush, ref y, "LANGUAGE (SW1,2):", _dip.Language.ToString().ToUpperInvariant());
        DrawOptionLine(g, optionFont, brush, dimBrush, ref y, "SHIPS PER GAME (SW4):", _dip.ShipsPerGame.ToString());
        DrawOptionLine(g, optionFont, brush, dimBrush, ref y, "CENTER COIN MECH x (SW3):", _dip.CenterCoinMechMultiplier.ToString());
        DrawOptionLine(g, optionFont, brush, dimBrush, ref y, "RIGHT COIN MECH x (SW5,6):", _dip.RightCoinMechMultiplier.ToString());
        DrawOptionLine(g, optionFont, brush, dimBrush, ref y, "COINAGE (SW7,8):", CoinModeText(_dip.CoinMode));
        y += 18f;

        // --- 4-toggle switch bank: coin door denomination configuration. ---
        DrawCenteredString(g, "4-TOGGLE SWITCH (COIN DOOR CONFIGURATION)", groupFont, brush, y); y += 24f;

        float bank4Width = 4 * SwitchColumnPitch;
        float bank4X = GameConstants.Center.X - bank4Width / 2f;
        float bank4Y = y + SwitchHeaderReserve;
        for (int col = 0; col < 4; col++)
        {
            int toggleNumber = DisplayOrder4[col] + 1;
            bool on = _dip.Switch4[DisplayOrder4[col]];
            bool selected = _state == GameState.Setup && _setupRow == SetupRow.CoinDoorSwitches && _setupCol == col;
            var rect = new RectangleF(bank4X + col * SwitchColumnPitch, bank4Y, SwitchWidth, SwitchHeight);
            _switch4Rects[col] = rect;
            DrawToggleSwitch(g, rect, on, toggleNumber.ToString(), selected, brush, dimBrush, selBrush, labelFont);
        }
        y = bank4Y + SwitchHeight + SwitchFooterReserve;

        var sb4 = new System.Text.StringBuilder();
        for (int col = 0; col < 4; col++) sb4.Append(_dip.Switch4[DisplayOrder4[col]] ? "0   " : "1   ");
        DrawCenteredStringAt(g, sb4.ToString().TrimEnd(), labelFont, dimBrush, bank4X, y, bank4Width); y += 26f;

        DrawOptionLine(g, optionFont, brush, dimBrush, ref y, "COIN DOOR (SW1,2):", CoinDoorText(_dip.CoinDoor));

        DrawCenteredString(g, "◄ ► SELECT SWITCH   •   ▲ ▼ SWITCH BANK   •   ENTER/SPACE OR CLICK TO FLIP   •   O / ESC TO EXIT", hintFont, dimBrush,
            GameConstants.ScreenHeight - 40f);

        string versionText = $"v{GameConstants.Version}";
        using var vFont = hintFont;
        var vSize = g.MeasureString(versionText, vFont);
        g.DrawString(versionText, vFont, dimBrush, GameConstants.ScreenWidth - vSize.Width - 10, GameConstants.ScreenHeight - vSize.Height - 8);
    }

    private static string CoinModeText(CoinMode mode) => mode switch
    {
        CoinMode.FreePlay => "FREE PLAY",
        CoinMode.OneCoinTwoPlays => "1 COIN FOR 2 PLAYS",
        CoinMode.OneCoinOnePlay => "1 COIN FOR 1 PLAY",
        _ => "2 COINS FOR 1 PLAY",
    };

    private static string CoinDoorText(CoinDoorConfig cfg) => cfg switch
    {
        CoinDoorConfig.OneDenomination => "ALL MECHS SAME DENOM. (1 COUNTER)",
        CoinDoorConfig.TwoDenominations => "LEFT+CENTER SAME, RIGHT DIFFERENT (2 COUNTERS)",
        CoinDoorConfig.Unused => "NOT DEFINED (NO COIN DOOR BUILT FOR THIS)",
        _ => "ALL 3 MECHS DIFFERENT DENOM. (3 COUNTERS)",
    };

    private void DrawOptionLine(Graphics g, Font font, Brush brush, Brush dimBrush, ref float y, string label, string value)
    {
        DrawCenteredStringPair(g, font, dimBrush, brush, label, value, y);
        y += 20f;
    }

    private static void DrawCenteredStringPair(Graphics g, Font font, Brush labelBrush, Brush valueBrush, string label, string value, float y)
    {
        string full = $"{label}  {value}";
        var fullSize = g.MeasureString(full, font);
        float x = (GameConstants.ScreenWidth - fullSize.Width) / 2f;
        g.DrawString(label, font, labelBrush, x, y);
        float valX = x + g.MeasureString(label + "  ", font).Width;
        g.DrawString(value, font, valueBrush, valX, y);
    }

    private static void DrawCenteredStringAt(Graphics g, string text, Font font, Brush brush, float regionX, float y, float regionWidth)
    {
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, regionX + (regionWidth - size.Width) / 2f, y);
    }

    /// <summary>One physical rocker switch: a rectangular body with the toggle drawn near the top
    /// of the body when ON and near the bottom when OFF, matching how a real DIP switch reads.</summary>
    private static void DrawToggleSwitch(Graphics g, RectangleF rect, bool on, string label, bool selected,
        Brush brush, Brush dimBrush, Brush selBrush, Font labelFont)
    {
        Color bodyColor = selected ? Color.FromArgb(255, 255, 210, 90) : Color.White;
        using var bodyPen = new Pen(bodyColor, selected ? 2f : 1.3f);
        g.DrawRectangle(bodyPen, rect.X, rect.Y, rect.Width, rect.Height);

        float rockerH = rect.Height * 0.45f;
        float rockerY = on ? rect.Y + 2f : rect.Y + rect.Height - rockerH - 2f;
        var rockerRect = new RectangleF(rect.X + 4f, rockerY, rect.Width - 8f, rockerH);
        using var rockerBrush = new SolidBrush(selected ? Color.FromArgb(255, 255, 210, 90) : Color.FromArgb(255, 200, 200, 200));
        g.FillRectangle(rockerBrush, rockerRect);
        g.DrawRectangle(bodyPen, rockerRect.X, rockerRect.Y, rockerRect.Width, rockerRect.Height);

        using var onFont = new Font(labelFont.FontFamily, 7f, FontStyle.Regular);
        var onDim = on ? brush : dimBrush;
        var offDim = !on ? brush : dimBrush;
        g.DrawString("ON", onFont, onDim, rect.X + 2f, rect.Y - 12f);
        g.DrawString("OFF", onFont, offDim, rect.X + 2f, rect.Y + rect.Height + 2f);

        var numSize = g.MeasureString(label, labelFont);
        g.DrawString(label, labelFont, selected ? selBrush : dimBrush, rect.X + (rect.Width - numSize.Width) / 2f, rect.Y + rect.Height + 16f);

        if (selected)
        {
            using var pen = new Pen(Color.FromArgb(255, 255, 210, 90), 1.6f);
            float ax = rect.X + rect.Width / 2f;
            g.DrawLine(pen, ax, rect.Y - 26f, ax, rect.Y - 16f);
            g.DrawLine(pen, ax - 5f, rect.Y - 21f, ax, rect.Y - 16f);
            g.DrawLine(pen, ax + 5f, rect.Y - 21f, ax, rect.Y - 16f);
        }
    }

    /// <summary>Draws large block letters as short vector strokes rather than a filled system
    /// font glyph, so even the title reads like it was plotted on an X-Y monitor.</summary>
    private void DrawWireTitle(Graphics g, string text, float centerX, float y, float charWidth)
    {
        using var font = new Font("Consolas", 34f, FontStyle.Bold);
        var size = g.MeasureString(text, font);
        float x = centerX - size.Width / 2f;

        using var path = new GraphicsPath();
        path.AddString(text, font.FontFamily, (int)FontStyle.Bold, 40f, new PointF(x, y), StringFormat.GenericTypographic);

        DrawGlowPath(g, path, Color.White, 3.5f, 1.4f);
    }

    private static void DrawGlowPath(Graphics g, GraphicsPath path, Color color, float glowWidth, float lineWidth)
    {
        using (var glowPen = new Pen(Color.FromArgb(55, color), glowWidth) { LineJoin = LineJoin.Round })
            g.DrawPath(glowPen, path);
        using (var pen = new Pen(color, lineWidth) { LineJoin = LineJoin.Round })
            g.DrawPath(pen, path);
    }

    private void DrawShip(Graphics g, Ship ship)
    {
        if (!ship.Alive) return;
        if (ship.Invulnerable && ((int)(_animClock * 10) % 2 == 0)) return;

        Vector2 fwd = ship.Forward;
        Vector2 right = new(fwd.Y, -fwd.X);
        Vector2 p = ship.Position;
        float s = GameConstants.ShipRadius;

        PointF nose = ToPt(p + fwd * s * 1.7f);
        PointF back1 = ToPt(p - fwd * s * 1.0f + right * s * 0.95f);
        PointF back2 = ToPt(p - fwd * s * 1.0f - right * s * 0.95f);
        PointF backMid = ToPt(p - fwd * s * 0.35f);

        if (ship.Thrusting)
        {
            float flick = 0.6f + (float)_rng.NextDouble() * 0.8f;
            PointF flame = ToPt(p - fwd * s * (1.0f + flick * 1.8f));
            using var flameBrush = new SolidBrush(Color.FromArgb(220, 255, 160, 60));
            g.FillPolygon(flameBrush, new[] { back1, flame, back2 });
        }

        var hull = new[] { nose, back1, backMid, back2 };
        DrawGlowPolygon(g, hull, Color.White, 3f, 1.6f);
        DrawHotCorners(g, hull, Color.White, 3f, 1.1f, 60);
    }

    private void DrawAsteroid(Graphics g, AsteroidObj a)
    {
        int n = a.Shape.Length;
        var pts = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            float ang = i / (float)n * MathF.Tau + a.RotationAngle;
            float r = a.Radius * a.Shape[i];
            pts[i] = new PointF(a.Position.X + MathF.Cos(ang) * r, a.Position.Y + MathF.Sin(ang) * r);
        }
        DrawGlowPolygon(g, pts, Color.White, 2.6f, 1.3f, dimFactor: 0.8f);
    }

    private void DrawSaucer(Graphics g, Saucer s)
    {
        float r = s.Radius;
        var p = s.Position;
        var pts = new[]
        {
            new PointF(p.X - r, p.Y),
            new PointF(p.X - r * 0.4f, p.Y - r * 0.55f),
            new PointF(p.X + r * 0.4f, p.Y - r * 0.55f),
            new PointF(p.X + r, p.Y),
            new PointF(p.X + r * 0.4f, p.Y + r * 0.5f),
            new PointF(p.X - r * 0.4f, p.Y + r * 0.5f),
        };
        DrawGlowPolygon(g, pts, Color.White, 3f, 1.5f);
        var top = new[]
        {
            new PointF(p.X - r * 0.35f, p.Y - r * 0.55f),
            new PointF(p.X - r * 0.2f, p.Y - r * 1.0f),
            new PointF(p.X + r * 0.2f, p.Y - r * 1.0f),
            new PointF(p.X + r * 0.35f, p.Y - r * 0.55f),
        };
        using var pen = new Pen(Color.White, 1.4f);
        g.DrawLines(pen, top);
    }

    private void DrawBullets(Graphics g)
    {
        using var glowBrush = new SolidBrush(Color.FromArgb(100, 220, 240, 255));
        using var brush = new SolidBrush(Color.White);
        foreach (var b in _bullets)
        {
            g.FillEllipse(glowBrush, b.Position.X - 4, b.Position.Y - 4, 8, 8);
            g.FillEllipse(brush, b.Position.X - 1.6f, b.Position.Y - 1.6f, 3.2f, 3.2f);
        }
    }

    private void DrawParticles(Graphics g)
    {
        foreach (var p in _particles)
        {
            Color c = p.CurrentColor();
            if (c.A <= 1) continue;
            using var pen = new Pen(c, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            Vector2 tail = p.Position - Vector2.Normalize(p.Velocity + new Vector2(0.001f, 0f)) * 4f;
            g.DrawLine(pen, ToPt(p.Position), ToPt(tail));
        }
    }

    private void DrawHud(Graphics g)
    {
        using var font = new Font("Consolas", 13f, FontStyle.Bold);
        using var smallFont = new Font("Consolas", 10f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.White);
        using var dimBrush = new SolidBrush(Color.FromArgb(255, 130, 130, 130));
        var lang = _dip.Language;

        g.DrawString(Strings.Get("PLAYER_1", lang), smallFont, _activePlayer == 0 ? brush : dimBrush, 14, 10);
        g.DrawString($"{_players[0].Score}", font, brush, 14, 26);
        DrawLifeIcons(g, 14, 50, _players[0].Lives);

        if (_mode == GameMode.TwoPlayer)
        {
            string p2 = Strings.Get("PLAYER_2", lang);
            g.DrawString(p2, smallFont, _activePlayer == 1 ? brush : dimBrush, GameConstants.ScreenWidth - 14 - g.MeasureString(p2, smallFont).Width, 10);
            string p2s = $"{_players[1].Score}";
            var p2Size = g.MeasureString(p2s, font);
            g.DrawString(p2s, font, brush, GameConstants.ScreenWidth - 14 - p2Size.Width, 26);
            DrawLifeIcons(g, GameConstants.ScreenWidth - 14 - Math.Max(60, _players[1].Lives * 22), 50, _players[1].Lives, rightAlign: true);
        }

        string wave = $"WAVE {_wave}";
        var wSize = g.MeasureString(wave, smallFont);
        g.DrawString(wave, smallFont, dimBrush, (GameConstants.ScreenWidth - wSize.Width) / 2f, 10);
    }

    private void DrawLifeIcons(Graphics g, float x, float y, int count, bool rightAlign = false)
    {
        using var pen = new Pen(Color.White, 1.4f);
        for (int i = 0; i < Math.Max(count, 0); i++)
        {
            float ix = rightAlign ? GameConstants.ScreenWidth - 14 - i * 20 : x + i * 20;
            var pts = new[]
            {
                new PointF(ix, y),
                new PointF(ix - 6, y + 12),
                new PointF(ix, y + 9),
                new PointF(ix + 6, y + 12),
            };
            g.DrawPolygon(pen, pts);
        }
    }

    private void DrawCenteredOverlay(Graphics g, string title, string sub, string sub2)
    {
        using var bg = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
        g.FillRectangle(bg, 0, 0, GameConstants.ScreenWidth, GameConstants.ScreenHeight);

        using var titleFont = new Font("Consolas", 30f, FontStyle.Bold);
        using var subFont = new Font("Consolas", 14f);
        using var brush = new SolidBrush(Color.White);

        DrawCenteredString(g, title, titleFont, brush, GameConstants.ScreenHeight / 2f - 40);
        if (!string.IsNullOrEmpty(sub)) DrawCenteredString(g, sub, subFont, brush, GameConstants.ScreenHeight / 2f + 15);
        if (!string.IsNullOrEmpty(sub2)) DrawCenteredString(g, sub2, subFont, brush, GameConstants.ScreenHeight / 2f + 42);
    }

    private void DrawHighScoreEntry(Graphics g)
    {
        foreach (var a in _demoAsteroids) DrawAsteroid(g, a);

        using var titleFont = new Font("Consolas", 16f, FontStyle.Bold);
        using var letterFont = new Font("Consolas", 40f, FontStyle.Bold);
        using var hintFont = new Font("Consolas", 12f);
        using var brush = new SolidBrush(Color.White);
        using var dimBrush = new SolidBrush(Color.FromArgb(255, 150, 150, 150));

        var lang = _dip.Language;
        string who = _mode == GameMode.TwoPlayer
            ? Strings.Get(_pendingInitialsPlayer == 0 ? "PLAYER_1" : "PLAYER_2", lang)
            : Strings.Get("PLAYER_1", lang);
        DrawCenteredString(g, $"{who}   {Strings.Get("HIGH_SCORE", lang)}: {_players[_pendingInitialsPlayer].Score}", titleFont, brush, 150f);

        float totalWidth = 3 * 60f;
        float startX = GameConstants.Center.X - totalWidth / 2f;
        for (int i = 0; i < 3; i++)
        {
            char c = _initialChars[i] == 26 ? '_' : (char)('A' + _initialChars[i]);
            var b = i == _initialsIndex ? brush : dimBrush;
            g.DrawString(c.ToString(), letterFont, b, startX + i * 60f, 220f);
            if (i == _initialsIndex)
            {
                using var pen = new Pen(Color.White, 2f);
                g.DrawLine(pen, startX + i * 60f, 280f, startX + i * 60f + 40f, 280f);
            }
        }

        DrawCenteredString(g, "◄/► TO CHANGE LETTER   •   FIRE/ENTER TO CONFIRM", hintFont, dimBrush, 340f);
    }

    private static void DrawCenteredString(Graphics g, string text, Font font, Brush brush, float y)
    {
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, (GameConstants.ScreenWidth - size.Width) / 2f, y);
    }

    private static void DrawGlowPolygon(Graphics g, PointF[] pts, Color color, float glowWidth, float lineWidth, float dimFactor = 1f)
    {
        Color dim = Color.FromArgb(color.A, (int)(color.R * dimFactor), (int)(color.G * dimFactor), (int)(color.B * dimFactor));
        using (var glowPen = new Pen(Color.FromArgb(40, dim), glowWidth) { LineJoin = LineJoin.Round })
            g.DrawPolygon(glowPen, pts);
        using (var pen = new Pen(dim, lineWidth) { LineJoin = LineJoin.Round })
            g.DrawPolygon(pen, pts);
    }

    private static void DrawHotCorners(Graphics g, IEnumerable<PointF> corners, Color color, float glowRadius, float dotRadius, int boost)
    {
        Color hot = Color.FromArgb(255, Math.Min(255, color.R + boost), Math.Min(255, color.G + boost), Math.Min(255, color.B + boost));
        foreach (var corner in corners)
        {
            using var glow = new SolidBrush(Color.FromArgb(120, hot));
            g.FillEllipse(glow, corner.X - glowRadius, corner.Y - glowRadius, glowRadius * 2, glowRadius * 2);
            using var dot = new SolidBrush(hot);
            g.FillEllipse(dot, corner.X - dotRadius, corner.Y - dotRadius, dotRadius * 2, dotRadius * 2);
        }
    }

    private static PointF ToPt(Vector2 v) => new(v.X, v.Y);
}
