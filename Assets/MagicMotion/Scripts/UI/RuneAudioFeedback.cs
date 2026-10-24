using UnityEngine;
using MagicMotion.Gesture;

namespace MagicMotion.UI
{
    [DisallowMultipleComponent]
    public sealed class RuneAudioFeedback : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip summon, confirm, fire, thunder, water, wind;

        private void Awake()
        {
            source = gameObject.GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            summon = Tone("rune_summon", 220f, 0.32f, 0.22f, 880f);
            confirm = Tone("rune_confirm", 440f, 0.18f, 0.2f, 660f);
            fire = Tone("rune_fire", 95f, 0.42f, 0.24f, 180f);
            thunder = Tone("rune_thunder", 720f, 0.18f, 0.3f, 120f);
            water = Tone("rune_water", 330f, 0.35f, 0.2f, 520f);
            wind = Tone("rune_wind", 520f, 0.28f, 0.18f, 920f);
        }

        public void PlaySummon() => Play(summon);
        public void PlayConfirm() => Play(confirm);
        public void PlayElement(RuneElement element)
        {
            switch (element)
            {
                case RuneElement.Fire: Play(fire); break;
                case RuneElement.Thunder: Play(thunder); break;
                case RuneElement.Water: Play(water); break;
                case RuneElement.Wind: Play(wind); break;
            }
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && source != null) source.PlayOneShot(clip);
        }

        private static AudioClip Tone(string name, float start, float seconds,
            float volume, float end)
        {
            const int rate = 44100;
            var samples = Mathf.CeilToInt(rate * seconds);
            var data = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)rate;
                var p = t / seconds;
                var frequency = Mathf.Lerp(start, end, p);
                var envelope = Mathf.Sin(Mathf.PI * p) * volume;
                var noise = (Mathf.PerlinNoise(t * 70f, 0.37f) - 0.5f) * 0.12f;
                data[i] = (Mathf.Sin(2f * Mathf.PI * frequency * t) + noise) * envelope;
            }
            var clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
