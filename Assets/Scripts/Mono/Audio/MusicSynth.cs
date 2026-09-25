using UnityEngine;

namespace GasStation.Mono.Audio
{
    public enum RadioStation
    {
        Off = 0,
        Country = 1,
        Synthwave = 2,
        LoFi = 3
    }

    /// <summary>
    /// Generates looping radio tracks procedurally (no audio assets): chords, bass, an arpeggio melody and
    /// drums over an eight-bar progression. Each station has its own tempo, key and sound.
    /// </summary>
    public static class MusicSynth
    {
        private const int SampleRate = 22050;
        private const int Bars = 8;
        private const int BeatsPerBar = 4;

        private struct Style
        {
            public float Bpm;
            public int[] Roots;       // MIDI root per bar (two bars per chord)
            public bool[] Minor;
            public bool Sevenths;
            public int Wave;          // 0 sine, 1 triangle, 2 square, 3 saw
            public float Swing;
            public bool Drums;
            public bool Crackle;
        }

        public static AudioClip Create(RadioStation station)
        {
            var style = station switch
            {
                RadioStation.Country => new Style
                {
                    Bpm = 112f, Roots = new[] { 55, 60, 62, 55 }, Minor = new[] { false, false, false, false },
                    Wave = 1, Drums = true
                },
                RadioStation.Synthwave => new Style
                {
                    Bpm = 96f, Roots = new[] { 57, 53, 48, 55 }, Minor = new[] { true, false, false, false },
                    Wave = 3, Drums = true
                },
                _ => new Style
                {
                    Bpm = 78f, Roots = new[] { 50, 55, 48, 57 }, Minor = new[] { true, false, false, true },
                    Sevenths = true, Wave = 0, Swing = 0.12f, Drums = true, Crackle = true
                }
            };

            float beat = 60f / style.Bpm;
            int length = Mathf.CeilToInt(Bars * BeatsPerBar * beat * SampleRate);
            var data = new float[length];
            var random = new System.Random((int)station * 7919);

            for (int bar = 0; bar < Bars; bar++)
            {
                int chord = bar / 2 % style.Roots.Length;
                int root = style.Roots[chord];
                int third = style.Minor[chord] ? 3 : 4;
                int[] tones = style.Sevenths
                    ? new[] { root, root + third, root + 7, root + (style.Minor[chord] ? 10 : 11) }
                    : new[] { root, root + third, root + 7 };
                float barStart = bar * BeatsPerBar * beat;

                // Pad: the chord held for the whole bar, soft attack.
                foreach (int tone in tones)
                    AddNote(data, barStart, BeatsPerBar * beat, Frequency(tone), 0.07f, PadWave(style), 0.25f, 0.4f);

                for (int b = 0; b < BeatsPerBar; b++)
                {
                    float t = barStart + b * beat;

                    // Bass: root on 1 and 3, fifth on 2 and 4 (country) or eighth-note root (synthwave).
                    int bassNote = (b % 2 == 0 ? root : root + 7) - 24;
                    if (style.Wave == 3)
                    {
                        AddNote(data, t, beat * 0.45f, Frequency(root - 24), 0.16f, 3, 0.005f, 0.1f);
                        AddNote(data, t + beat * 0.5f, beat * 0.45f, Frequency(root - 24), 0.14f, 3, 0.005f, 0.1f);
                    }
                    else
                    {
                        AddNote(data, t, beat * 0.9f, Frequency(bassNote), 0.2f, 0, 0.005f, 0.3f);
                    }

                    // Arpeggio: eighth notes walking up and down the chord, an octave higher.
                    for (int e = 0; e < 2; e++)
                    {
                        int step = (b * 2 + e) % (tones.Length * 2 - 2);
                        int index = step < tones.Length ? step : tones.Length * 2 - 2 - step;
                        float swing = e == 1 ? style.Swing * beat : 0f;
                        // Leave some gaps so the melody breathes.
                        if (random.NextDouble() < 0.18)
                            continue;
                        AddNote(data, t + e * beat * 0.5f + swing, beat * 0.45f, Frequency(tones[index] + 12), 0.1f, style.Wave, 0.004f, 0.18f);
                    }

                    if (!style.Drums)
                        continue;

                    if (b % 2 == 0)
                        AddKick(data, t, style.Wave == 0 ? 0.25f : 0.4f);
                    else
                        AddNoise(data, t, 0.12f, style.Wave == 0 ? 0.08f : 0.16f, random);

                    AddNoise(data, t + beat * 0.5f + style.Swing * beat, 0.03f, 0.05f, random);
                }
            }

            if (style.Crackle)
            {
                for (int i = 0; i < length; i += random.Next(400, 3000))
                    data[i] += (float)(random.NextDouble() * 2 - 1) * 0.15f;
            }

            Normalize(data, 0.8f);
            var clip = AudioClip.Create($"Radio_{station}", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Pads: saw for synthwave, triangle for country, sine for lo-fi.</summary>
        private static int PadWave(Style style) => style.Wave == 3 ? 3 : style.Wave == 1 ? 1 : 0;

        private static float Frequency(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        private static void AddNote(float[] data, float start, float duration, float frequency, float volume, int wave,
            float attack, float release)
        {
            int from = Mathf.Clamp((int)(start * SampleRate), 0, data.Length);
            int to = Mathf.Clamp((int)((start + duration + release) * SampleRate), 0, data.Length);
            for (int i = from; i < to; i++)
            {
                float t = (float)(i - from) / SampleRate;
                float envelope = t < attack ? t / attack
                    : t < duration ? 1f - 0.3f * (t - attack) / Mathf.Max(0.001f, duration)
                    : 0.7f * Mathf.Max(0f, 1f - (t - duration) / release);
                float phase = t * frequency;
                data[i] += Wave(wave, phase) * envelope * volume;
            }
        }

        private static float Wave(int wave, float phase)
        {
            float p = phase - Mathf.Floor(phase);
            return wave switch
            {
                1 => 1f - 4f * Mathf.Abs(p - 0.5f),
                2 => p < 0.5f ? 0.6f : -0.6f,
                3 => (2f * p - 1f) * 0.7f,
                _ => Mathf.Sin(2f * Mathf.PI * p)
            };
        }

        private static void AddKick(float[] data, float start, float volume)
        {
            int from = Mathf.Clamp((int)(start * SampleRate), 0, data.Length);
            int to = Mathf.Clamp(from + SampleRate / 6, 0, data.Length);
            float phase = 0f;
            for (int i = from; i < to; i++)
            {
                float t = (float)(i - from) / SampleRate;
                phase += Mathf.Lerp(120f, 45f, t * 6f) / SampleRate;
                data[i] += Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t * 18f) * volume;
            }
        }

        private static void AddNoise(float[] data, float start, float duration, float volume, System.Random random)
        {
            int from = Mathf.Clamp((int)(start * SampleRate), 0, data.Length);
            int to = Mathf.Clamp((int)((start + duration) * SampleRate), 0, data.Length);
            for (int i = from; i < to; i++)
            {
                float t = (float)(i - from) / SampleRate;
                data[i] += (float)(random.NextDouble() * 2 - 1) * Mathf.Exp(-t * 40f) * volume;
            }
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0.0001f;
            foreach (float sample in data)
                max = Mathf.Max(max, Mathf.Abs(sample));
            float gain = peak / max;
            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;
        }
    }
}
