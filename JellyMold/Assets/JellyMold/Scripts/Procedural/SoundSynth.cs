using System;

namespace JellyMold.Procedural
{
    public enum SoundId
    {
        Tap,
        Press,
        Squish,
        Bump,
        Fill0,
        Fill1,
        Fill2,
        Fill3,
        Fill4,
        Combo,
        Win,
        Lose,
        Click,
        Undo,
    }

    /// <summary>
    /// Procedural sound effects: short, soft sine/FM blips with smooth envelopes.
    /// Generated at start-up, so the game ships no audio files and has no licensing questions.
    /// </summary>
    public static class SoundSynth
    {
        private const float Tau = (float)(Math.PI * 2);

        /// <summary>Pentatonic steps so successive fills in a chain climb pleasantly.</summary>
        private static readonly float[] FillNotes = { 523.25f, 587.33f, 659.25f, 783.99f, 880.00f };

        public static float[] Generate(SoundId id, int rate)
        {
            switch (id)
            {
                case SoundId.Tap: return Normalize(Blip(rate, 0.07f, 520f, 780f, 0.35f), 0.45f);
                case SoundId.Press: return Normalize(Whoosh(rate, 0.14f), 0.3f);
                case SoundId.Squish: return Normalize(Squish(rate), 0.5f);
                case SoundId.Bump: return Normalize(Bump(rate), 0.55f);
                case SoundId.Fill0:
                case SoundId.Fill1:
                case SoundId.Fill2:
                case SoundId.Fill3:
                case SoundId.Fill4:
                    return Normalize(Bell(rate, FillNotes[id - SoundId.Fill0], 0.5f), 0.6f);
                case SoundId.Combo: return Normalize(Arpeggio(rate, new[] { 1046.5f, 1318.5f, 1568f, 2093f }, 0.06f, 0.3f), 0.5f);
                case SoundId.Win: return Normalize(Arpeggio(rate, new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f }, 0.1f, 0.7f), 0.65f);
                case SoundId.Lose: return Normalize(Arpeggio(rate, new[] { 392f, 329.63f }, 0.18f, 0.45f), 0.4f);
                case SoundId.Click: return Normalize(Blip(rate, 0.035f, 1250f, 1100f, 0.1f), 0.3f);
                case SoundId.Undo: return Normalize(Blip(rate, 0.11f, 720f, 430f, 0.4f), 0.4f);
                default: return new float[1];
            }
        }

        private static float[] Blip(int rate, float duration, float f0, float f1, float harmonic)
        {
            int n = (int)(rate * duration);
            var s = new float[n];
            float phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = f0 + (f1 - f0) * (1f - (1f - t) * (1f - t));
                phase += Tau * f / rate;
                float env = Attack(i, rate, 0.004f) * (float)Math.Exp(-5f * t);
                s[i] = env * ((float)Math.Sin(phase) + harmonic * (float)Math.Sin(phase * 2f));
            }
            return s;
        }

        /// <summary>Wet "blop": falling pitch with a little FM wobble.</summary>
        private static float[] Squish(int rate)
        {
            int n = (int)(rate * 0.13f);
            var s = new float[n];
            float phase = 0, mod = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = 340f * (float)Math.Pow(0.55f, t) + 18f * (float)Math.Sin(t * 40f);
                phase += Tau * f / rate;
                mod += Tau * f * 2f / rate;
                float index = 1.4f * (1f - t);
                float env = Attack(i, rate, 0.003f) * (1f - t) * (1f - t);
                s[i] = env * (float)Math.Sin(phase + index * Math.Sin(mod));
            }
            return s;
        }

        private static float[] Bump(int rate)
        {
            int n = (int)(rate * 0.1f);
            var s = new float[n];
            var rng = new Random(7);
            float phase = 0, noise = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = 140f - 70f * t;
                phase += Tau * f / rate;
                noise += ((float)rng.NextDouble() * 2f - 1f - noise) * 0.2f;
                float env = Attack(i, rate, 0.002f) * (float)Math.Exp(-7f * t);
                s[i] = env * ((float)Math.Sin(phase) + noise * 0.4f * (1f - t));
            }
            return s;
        }

        /// <summary>Low-passed noise swell for the press gesture.</summary>
        private static float[] Whoosh(int rate, float duration)
        {
            int n = (int)(rate * duration);
            var s = new float[n];
            var rng = new Random(3);
            float lp = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float cutoff = 0.05f + 0.25f * (float)Math.Sin(t * Math.PI);
                lp += ((float)rng.NextDouble() * 2f - 1f - lp) * cutoff;
                s[i] = lp * (float)Math.Sin(t * Math.PI);
            }
            return s;
        }

        /// <summary>Soft bell with a short upward pitch scoop at the start.</summary>
        private static float[] Bell(int rate, float f, float duration)
        {
            int n = (int)(rate * duration);
            var s = new float[n];
            float phase = 0;
            for (int i = 0; i < n; i++)
            {
                float time = (float)i / rate;
                float scoop = time < 0.03f ? 0.85f + 0.15f * (time / 0.03f) : 1f;
                phase += Tau * f * scoop / rate;
                float env = Attack(i, rate, 0.004f) * (float)Math.Exp(-6.5f * time);
                s[i] = env * ((float)Math.Sin(phase) + 0.32f * (float)Math.Sin(phase * 2f) * (float)Math.Exp(-10f * time) + 0.12f * (float)Math.Sin(phase * 3.01f));
            }
            return s;
        }

        private static float[] Arpeggio(int rate, float[] notes, float step, float tail)
        {
            float duration = step * (notes.Length - 1) + tail;
            int n = (int)(rate * duration);
            var s = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int start = (int)(k * step * rate);
                float phase = 0;
                for (int i = start; i < n; i++)
                {
                    float time = (float)(i - start) / rate;
                    phase += Tau * notes[k] / rate;
                    float env = Attack(i - start, rate, 0.005f) * (float)Math.Exp(-5f * time);
                    s[i] += env * ((float)Math.Sin(phase) + 0.25f * (float)Math.Sin(phase * 2f)) * 0.6f;
                }
            }
            return s;
        }

        private static float Attack(int i, int rate, float seconds)
        {
            float a = i / (rate * seconds);
            return a >= 1f ? 1f : a;
        }

        private static float[] Normalize(float[] s, float peak)
        {
            float max = 1e-6f;
            foreach (float v in s) max = Math.Max(max, Math.Abs(v));
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
            // Short fade-out avoids clicks at the end of the buffer.
            int fade = Math.Min(s.Length, 64);
            for (int i = 0; i < fade; i++) s[s.Length - 1 - i] *= (float)i / fade;
            return s;
        }
    }
}
