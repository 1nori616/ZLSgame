using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// 背景音乐：自动从 Assets/Resources/Music/ 里找第一首曲子，循环播放。
    /// 跨场景不中断（死亡重开时不会被重新开始）。
    /// 用法：把音乐文件丢进 Assets/Resources/Music/ 即可，代码不用改。
    /// </summary>
    public class BgmPlayer : MonoBehaviour
    {
        public static BgmPlayer Instance { get; private set; }

        [Tooltip("背景音量：一定要比音效低，建议 0.2~0.3")]
        [Range(0f, 1f)]
        public static float Volume = 0.25f;

        [Tooltip("淡入时长（秒）")]
        [SerializeField] private float fadeInTime = 1.5f;

        private AudioSource source;
        private float targetVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            Ensure();
        }

        public static BgmPlayer Ensure()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("BgmPlayer");
            DontDestroyOnLoad(go);
            return go.AddComponent<BgmPlayer>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            source = gameObject.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;

            AudioClip clip = LoadMusic();
            if (clip == null)
            {
                Debug.Log("[BgmPlayer] Assets/Resources/Music/ 下没有音乐文件（不会播放 BGM）");
                return;
            }

            source.clip = clip;
            source.Play();

            targetVolume = Volume;
            Debug.Log($"[BgmPlayer] 播放背景音乐：{clip.name}");
        }

        private static AudioClip LoadMusic()
        {
            AudioClip[] clips = Resources.LoadAll<AudioClip>("Music");
            if (clips == null || clips.Length == 0) return null;

            // 优先找名字里带 bgm 的
            for (int i = 0; i < clips.Length; i++)
                if (clips[i].name.ToLower().Contains("bgm")) return clips[i];

            return clips[0];
        }

        private void Update()
        {
            if (source == null || source.clip == null) return;

            targetVolume = Volume;
            if (Mathf.Approximately(source.volume, targetVolume)) return;

            float speed = fadeInTime > 0.01f ? (1f / fadeInTime) : 100f;
            source.volume = Mathf.MoveTowards(source.volume, targetVolume, speed * Time.unscaledDeltaTime);
        }

        /// <summary>换一首曲子（按名字，位于 Resources/Music/ 下）。</summary>
        public static void PlayClip(string clipName, float fade = 0.8f)
        {
            BgmPlayer p = Ensure();
            if (p.source == null) return;

            AudioClip clip = Resources.Load<AudioClip>("Music/" + clipName);
            if (clip == null)
            {
                Debug.LogWarning($"[BgmPlayer] 找不到 Music/{clipName}");
                return;
            }

            if (p.source.clip == clip) return;

            p.source.clip = clip;
            p.source.Play();
            p.fadeInTime = Mathf.Max(0.01f, fade);
        }

        public static void Stop()
        {
            if (Instance != null && Instance.source != null) Instance.source.Stop();
        }
    }
}
