using System;
using UnityEngine;

namespace Game.Runtime
{
    /// The five M1 sounds, synthesised at boot so nothing is downloaded or imported (ARCHITECTURE §14.6).
    /// Deleted in M3 when the CC0 set lands under Resources/Audio/sfx/.
    public static class PlaceholderSfx
    {
        const int SampleRate = 44100;
        const float NoisePeak = 0.6f;
        const float TonePeak = 0.5f;
        const float EdgeSeconds = 0.005f;
        const float C5 = 523.25f;
        const float E5 = 659.25f;
        const float G5 = 783.99f;
        const float G4 = 392.00f;
        const float D4 = 293.66f;

        /// Indexed by (int)SfxId.
        public static AudioClip[] Build()
        {
            var clips = new AudioClip[5];
            clips[(int)SfxId.Hit] = Clip("sfx_hit", Noise(0.060f, 0.012f));
            clips[(int)SfxId.Skill] = Clip("sfx_skill", Sweep(0.120f, 300f, 900f));
            clips[(int)SfxId.Victory] = Clip("sfx_victory", Tones(0.090f, C5, E5, G5));
            clips[(int)SfxId.Defeat] = Clip("sfx_defeat", Tones(0.150f, G4, D4));
            clips[(int)SfxId.MenuTap] = Clip("sfx_menu_tap", Tones(0.020f, 1000f));
            return clips;
        }

        static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // White noise under an exponential decay; the fixed seed keeps every boot's hit identical.
        static float[] Noise(float seconds, float decaySeconds)
        {
            var data = new float[Samples(seconds)];
            var random = new System.Random(1234);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = (float)(random.NextDouble() * 2.0 - 1.0) * NoisePeak * Mathf.Exp(-t / decaySeconds);
            }
            return data;
        }

        // Sine whose frequency rises linearly; the phase is accumulated so the sweep has no discontinuities.
        static float[] Sweep(float seconds, float fromHz, float toHz)
        {
            var data = new float[Samples(seconds)];
            double phase = 0.0;
            for (int i = 0; i < data.Length; i++)
            {
                phase += 2.0 * Math.PI * Mathf.Lerp(fromHz, toHz, i / (float)data.Length) / SampleRate;
                data[i] = (float)Math.Sin(phase) * TonePeak * Edge(i, data.Length);
            }
            return data;
        }

        // One sine per frequency, back to back, each with its own attack and decay.
        static float[] Tones(float secondsEach, params float[] frequencies)
        {
            int per = Samples(secondsEach);
            var data = new float[per * frequencies.Length];
            for (int n = 0; n < frequencies.Length; n++)
                for (int i = 0; i < per; i++)
                    data[n * per + i] = (float)Math.Sin(2.0 * Math.PI * frequencies[n] * i / SampleRate) * TonePeak * Edge(i, per);
            return data;
        }

        // Linear 5 ms attack and decay so a tone starts and stops without a click.
        static float Edge(int i, int length)
        {
            int edge = Samples(EdgeSeconds);
            float attack = Mathf.Clamp01((i + 1) / (float)edge);
            float decay = Mathf.Clamp01((length - i) / (float)edge);
            return Mathf.Min(attack, decay);
        }

        static int Samples(float seconds) => Mathf.RoundToInt(seconds * SampleRate);
    }
}
