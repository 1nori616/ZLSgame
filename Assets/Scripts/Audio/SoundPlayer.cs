using System.Collections.Generic;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// 音效播放器：从 Assets/Resources/SFX/ 加载，按名字播放。
    /// 用对象池 + 随机音高，避免同一个音反复播放听起来呆板。
    ///
    /// 用法：SoundPlayer.Play("hit", 0.8f);
    /// </summary>
    public class SoundPlayer : MonoBehaviour
    {
        /// <summary>总音量，0~1。</summary>
        public static float MasterVolume = 0.55f;

        private const int PoolSize = 6;
        private const string ResourceFolder = "SFX/";

        private static SoundPlayer instance;

        private readonly List<AudioSource> sources = new List<AudioSource>();
        private readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> missing = new HashSet<string>();
        private int next;

        private static SoundPlayer Get()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("SoundPlayer");
                instance = go.AddComponent<SoundPlayer>();
            }
            return instance;
        }

        private void Awake()
        {
            instance = this;

            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;      // 2D 音效
                s.volume = 1f;
                sources.Add(s);
            }
        }

        private AudioClip Load(string name)
        {
            if (cache.TryGetValue(name, out AudioClip clip)) return clip;
            if (missing.Contains(name)) return null;

            clip = Resources.Load<AudioClip>(ResourceFolder + name);
            if (clip == null)
            {
                missing.Add(name);
                Debug.LogWarning($"[SoundPlayer] 找不到音效：{ResourceFolder}{name}（会静默跳过）");
                return null;
            }

            cache[name] = clip;
            return clip;
        }

        /// <summary>播放一个音效。volume 是相对总音量的倍率。</summary>
        public static void Play(string name, float volume = 1f, float pitchJitter = 0.05f)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (MasterVolume <= 0.001f) return;

            SoundPlayer p = Get();
            AudioClip clip = p.Load(name);
            if (clip == null) return;

            if (p.sources.Count == 0) return;

            // 轮流用池子里的 AudioSource，避免互相打断
            AudioSource src = p.sources[p.next];
            p.next = (p.next + 1) % p.sources.Count;

            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.PlayOneShot(clip, Mathf.Clamp01(MasterVolume * volume));
        }

        /// <summary>停止所有音效（比如重开关卡时）。</summary>
        public static void StopAll()
        {
            if (instance == null) return;
            foreach (AudioSource s in instance.sources) s.Stop();
        }
    }
}
