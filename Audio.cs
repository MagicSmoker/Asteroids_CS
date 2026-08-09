using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Asteroids;

internal enum Sfx { Fire, SaucerFire, BangLarge, BangMedium, BangSmall, SaucerBig, SaucerSmall, ExtraShip, Hyperspace, ShipExplode, Beat1, Beat2 }

/// <summary>Tiny procedural sound engine: synthesizes short arcade-style blips/noise bursts in
/// memory (no asset files) and layers them through an NAudio mixer so overlapping effects -
/// rapid fire, an explosion, and the background heartbeat - all play at once.</summary>
internal static class Audio
{
    private const int SampleRate = 44100;
    private static readonly WaveFormat Format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);

    private static WaveOutEvent? _output;
    private static MixingSampleProvider? _mixer;
    private static readonly Dictionary<Sfx, float[]> _clips = new();
    private static ISampleProvider? _thrustMixerInput;
    private static bool _thrustOn;
    private static bool _available;

    public static void Init()
    {
        try
        {
            foreach (Sfx sfx in Enum.GetValues<Sfx>())
                _clips[sfx] = Synth.Build(sfx);

            _mixer = new MixingSampleProvider(Format) { ReadFully = true };
            _output = new WaveOutEvent { DesiredLatency = 80 };
            _output.Init(_mixer);
            _output.Play();
            _available = true;
        }
        catch
        {
            // No audio device, or the driver refused us - the game still runs fine without sound.
            _available = false;
        }
    }

    public static void Play(Sfx sfx, float volume = 1f)
    {
        if (!_available || _mixer == null) return;
        var clip = new ClipProvider(_clips[sfx], Format);
        _mixer.AddMixerInput(new VolumeSampleProvider(clip) { Volume = volume });
    }

    public static void SetThrust(bool on)
    {
        if (!_available || _mixer == null || on == _thrustOn) return;
        _thrustOn = on;
        if (on)
        {
            var loop = new LoopingClipProvider(Synth.ThrustLoopClip, Format);
            _thrustMixerInput = new VolumeSampleProvider(loop) { Volume = 0.3f };
            _mixer.AddMixerInput(_thrustMixerInput);
        }
        else if (_thrustMixerInput != null)
        {
            _mixer.RemoveMixerInput(_thrustMixerInput);
            _thrustMixerInput = null;
        }
    }

    private sealed class ClipProvider : ISampleProvider
    {
        private readonly float[] _data;
        private int _pos;
        public ClipProvider(float[] data, WaveFormat format) { _data = data; WaveFormat = format; }
        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int remaining = _data.Length - _pos;
            int n = Math.Min(remaining, count);
            if (n <= 0) return 0;
            Array.Copy(_data, _pos, buffer, offset, n);
            _pos += n;
            return n;
        }
    }

    private sealed class LoopingClipProvider : ISampleProvider
    {
        private readonly float[] _data;
        private int _pos;
        public LoopingClipProvider(float[] data, WaveFormat format) { _data = data; WaveFormat = format; }
        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = _data[_pos];
                _pos = (_pos + 1) % _data.Length;
            }
            return count;
        }
    }
}
