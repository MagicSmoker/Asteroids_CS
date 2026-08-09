namespace Asteroids;

/// <summary>Procedurally generates short float PCM clips for each sound effect - classic arcade
/// square-wave blips and filtered-noise thumps - so the game ships with no audio asset files.</summary>
internal static class Synth
{
    private const int SampleRate = 44100;
    private static readonly Random Rng = new(20250809);

    private static readonly Lazy<float[]> _thrustLoopClip = new(() => BuildThrustLoop());
    public static float[] ThrustLoopClip => _thrustLoopClip.Value;

    public static float[] Build(Sfx sfx) => sfx switch
    {
        Sfx.Fire => Tone(950f, 620f, 0.09f, square: true, volume: 0.45f, attack: 0.002f),
        Sfx.SaucerFire => Tone(1400f, 220f, 0.16f, square: true, volume: 0.4f, attack: 0.002f, noiseMix: 0.2f),
        Sfx.BangLarge => Combine(
            Noise(0.28f, volume: 0.6f, lowpass: 5, decayPower: 1.4f),
            Tone(110f, 60f, 0.26f, square: false, volume: 0.4f, attack: 0.004f)),
        Sfx.BangMedium => Combine(
            Noise(0.2f, volume: 0.55f, lowpass: 3, decayPower: 1.6f),
            Tone(190f, 90f, 0.18f, square: false, volume: 0.35f, attack: 0.003f)),
        Sfx.BangSmall => Combine(
            Noise(0.13f, volume: 0.5f, lowpass: 1, decayPower: 2f),
            Tone(340f, 150f, 0.11f, square: false, volume: 0.3f, attack: 0.002f)),
        Sfx.SaucerBig => Tone(180f, 220f, 0.5f, square: true, volume: 0.28f, attack: 0.05f),
        Sfx.SaucerSmall => Tone(420f, 520f, 0.35f, square: true, volume: 0.28f, attack: 0.03f),
        Sfx.ExtraShip => Arpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.09f, volume: 0.5f),
        Sfx.Hyperspace => Tone(90f, 1600f, 0.22f, square: false, volume: 0.4f, attack: 0.01f, noiseMix: 0.25f),
        Sfx.ShipExplode => Combine(
            Noise(0.5f, volume: 0.65f, lowpass: 4, decayPower: 1.3f),
            Tone(170f, 35f, 0.45f, square: false, volume: 0.4f, attack: 0.005f)),
        Sfx.Beat1 => Tone(90f, 60f, 0.09f, square: true, volume: 0.42f, attack: 0.001f),
        Sfx.Beat2 => Tone(75f, 48f, 0.09f, square: true, volume: 0.42f, attack: 0.001f),
        _ => Array.Empty<float>(),
    };

    private static float[] Tone(float freqStart, float freqEnd, float duration, bool square, float volume, float attack, float noiseMix = 0f)
    {
        int n = (int)(duration * SampleRate);
        var outp = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float freq = freqStart + (freqEnd - freqStart) * (t / duration);
            phase += freq / SampleRate;
            double raw = square ? (Math.Sin(phase * Math.Tau) >= 0 ? 1.0 : -1.0) : Math.Sin(phase * Math.Tau);
            if (noiseMix > 0f) raw = raw * (1 - noiseMix) + (Rng.NextDouble() * 2 - 1) * noiseMix;

            float attackEnv = attack <= 0f ? 1f : Math.Clamp(t / attack, 0f, 1f);
            float releaseEnv = MathF.Pow(Math.Clamp(1f - t / duration, 0f, 1f), 1.4f);
            outp[i] = (float)raw * attackEnv * releaseEnv * volume;
        }
        return outp;
    }

    private static float[] Noise(float duration, float volume, int lowpass, float decayPower)
    {
        int n = (int)(duration * SampleRate);
        var raw = new float[n];
        for (int i = 0; i < n; i++) raw[i] = (float)(Rng.NextDouble() * 2 - 1);

        var filtered = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0; int cnt = 0;
            for (int k = -lowpass; k <= lowpass; k++)
            {
                int idx = i + k;
                if (idx >= 0 && idx < n) { sum += raw[idx]; cnt++; }
            }
            filtered[i] = sum / cnt;
        }

        var outp = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float env = MathF.Pow(Math.Clamp(1f - t / duration, 0f, 1f), decayPower);
            outp[i] = filtered[i] * env * volume;
        }
        return outp;
    }

    private static float[] Combine(float[] a, float[] b)
    {
        int n = Math.Max(a.Length, b.Length);
        var outp = new float[n];
        for (int i = 0; i < n; i++)
        {
            float v = 0f;
            if (i < a.Length) v += a[i];
            if (i < b.Length) v += b[i];
            outp[i] = Math.Clamp(v, -1f, 1f);
        }
        return outp;
    }

    private static float[] Arpeggio(float[] freqs, float noteDuration, float volume)
    {
        var parts = new float[freqs.Length][];
        int total = 0;
        for (int i = 0; i < freqs.Length; i++)
        {
            parts[i] = Tone(freqs[i], freqs[i], noteDuration, square: false, volume: volume, attack: 0.005f);
            total += parts[i].Length;
        }

        var outp = new float[total];
        int pos = 0;
        foreach (var p in parts)
        {
            Array.Copy(p, 0, outp, pos, p.Length);
            pos += p.Length;
        }
        return outp;
    }

    private static float[] BuildThrustLoop()
    {
        const float duration = 0.3f;
        int n = (int)(duration * SampleRate);
        var raw = new float[n];
        for (int i = 0; i < n; i++) raw[i] = (float)(Rng.NextDouble() * 2 - 1);

        const int lp = 6;
        var filtered = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0;
            for (int k = -lp; k <= lp; k++)
                sum += raw[((i + k) % n + n) % n];
            filtered[i] = sum / (lp * 2 + 1);
        }

        var outp = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SampleRate;
            float sine = MathF.Sin(2f * MathF.PI * 42f * t) * 0.3f;
            outp[i] = Math.Clamp(filtered[i] * 0.55f + sine, -1f, 1f) * 0.5f;
        }
        return outp;
    }
}
