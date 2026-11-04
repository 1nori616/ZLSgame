using System;
using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// 程序合成的音效：不需要任何音频资源。
    /// 第一次调用时自动创建 AudioSource，之后直接 PlayXxx()。
    /// 以后美术给了真音效，把 BuildClips 里的合成换成 Resources.Load 即可。
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private static Sfx instance;

        [Range(0f, 1f)] public static float Volume = 0.65f;

        private AudioSource source;
        private AudioClip hit;
        private AudioClip slam;
        private AudioClip whoosh;
        private AudioClip death;
        private AudioClip pickup;
        private AudioClip zeroG;

        private static Sfx Get()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("Sfx");
                instance = go.AddComponent<Sfx>();
            }
            return instance;
        }

        private void Awake()
        {
            instance = this;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;

            BuildClips();
        }

        private void BuildClips()
        {
            hit = Build("sfx_hit", 0.12f, t =>
                Noise(t) * Mathf.Exp(-40f * t) * 0.7f +
                Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-28f * t) * 0.5f);

            slam = Build("sfx_slam", 0.30f, t =>
            {
                float freq = 55f + 45f * Mathf.Exp(-9f * t);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-7f * t) * 0.8f +
                       Noise(t) * Mathf.Exp(-35f * t) * 0.25f;
            });

            whoosh = Build("sfx_whoosh", 0.25f, t =>
            {
                float env = Mathf.Sin(Mathf.PI * t / 0.25f);
                return Noise(t) * env * 0.35f;
            });

            death = Build("sfx_death", 0.35f, t =>
            {
                float f = Mathf.Lerp(420f, 110f, t / 0.35f);
                float square = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
                return square * Mathf.Exp(-5f * t) * 0.35f;
            });

            pickup = Build("sfx_pickup", 0.16f, t =>
            {
                float f = t < 0.07f ? 880f : 1320f;
                return Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-14f * t) * 0.4f;
            });

            zeroG = Build("sfx_zerog", 0.7f, t =>
            {
                float f = Mathf.Lerp(180f, 560f, t / 0.7f);
                float tremolo = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 9f * t);
                return Mathf.Sin(2f * Mathf.PI * f * t) * tremolo * Mathf.Exp(-2.2f * t) * 0.35f;
            });
        }

        private static float Noise(float t)
        {
            // 简单的确定性"噪声"：多个正弦叠加，听起来像沙沙声
            return Mathf.Sin(2f * Mathf.PI * 1731f * t) * 0.5f +
                   Mathf.Sin(2f * Mathf.PI * 2737f * t) * 0.3f +
                   Mathf.Sin(2f * Mathf.PI * 4129f * t) * 0.2f;
        }

        private static AudioClip Build(string name, float duration, Func<float, float> sample)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                data[i] = Mathf.Clamp(sample(t), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void Play(AudioClip clip, float volumeScale = 1f)
        {
            if (source == null || clip == null) return;
            source.PlayOneShot(clip, Mathf.Clamp01(Volume * volumeScale));
        }

        public static void PlayHit(float scale = 1f) => Get().Play(Get().hit, scale);
        public static void PlaySlam() => Get().Play(Get().slam);
        public static void PlayWhoosh() => Get().Play(Get().whoosh);
        public static void PlayDeath() => Get().Play(Get().death);
        public static void PlayPickup() => Get().Play(Get().pickup);
        public static void PlayZeroG() => Get().Play(Get().zeroG);
    }
}
