using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gravity
{
    public enum WorldGravityMode
    {
        Normal,
        Flipped,
        ZeroG
    }

    /// <summary>
    /// 房间级重力状态：
    ///   Normal  = 正常（往下）
    ///   Flipped = 翻转（往上）
    ///   ZeroG   = 失重（重力 0 + 阻力 + 轻微随机漂浮，推一下飘一段就慢慢停）
    /// 直接改 Physics2D.gravity，所以场景里所有刚体一起受影响。
    /// </summary>
    public class GravityWorld : MonoBehaviour
    {
        public static GravityWorld Instance { get; private set; }

        [Tooltip("正常重力大小")]
        [SerializeField] private float normalGravity = 9.81f;

        [Tooltip("翻转默认持续时间（秒）")]
        [SerializeField] private float flipDuration = 10f;

        [Header("失重手感")]
        [Tooltip("空气阻力：越大停得越快，1.5 左右比较像飘")]
        [SerializeField] private float zeroGDrag = 1.4f;

        [Tooltip("随机漂浮的推力，0 = 完全静止悬浮")]
        [SerializeField] private float zeroGWander = 1.2f;

        [Tooltip("进入失重瞬间给所有物体的向上速度，制造「集体离地」的效果")]
        [SerializeField] private float zeroGLiftSpeed = 2.6f;

        [Tooltip("进入失重时保留多少原有速度（0.3 = 抹掉大部分冲量）")]
        [Range(0f, 1f)]
        [SerializeField] private float zeroGVelocityKeep = 0.25f;

        private float timer;

        private readonly List<Rigidbody2D> bodies = new List<Rigidbody2D>();
        private readonly List<float> originalDrag = new List<float>();
        private readonly List<Vector2> driftDir = new List<Vector2>();
        private readonly List<float> driftTimer = new List<float>();
        private readonly List<bool> isPlayerBody = new List<bool>();

        public WorldGravityMode Mode { get; private set; } = WorldGravityMode.Normal;
        public bool IsFlipped => Mode == WorldGravityMode.Flipped;
        public bool IsZeroG => Mode == WorldGravityMode.ZeroG;
        public int Sign => Mode == WorldGravityMode.Flipped ? -1 : 1;
        public float RemainingTime => Mathf.Max(0f, timer);

        public event Action<int> OnSignChanged;
        public event Action<WorldGravityMode> OnModeChanged;

        private void Awake()
        {
            Instance = this;
            Apply(WorldGravityMode.Normal, 0f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RestoreDrag();
        }

        private void Update()
        {
            if (timer <= 0f) return;

            timer -= Time.deltaTime;
            if (timer <= 0f) Apply(WorldGravityMode.Normal, 0f);
        }

        private void FixedUpdate()
        {
            if (Mode != WorldGravityMode.ZeroG || zeroGWander <= 0f) return;

            for (int i = 0; i < bodies.Count; i++)
            {
                Rigidbody2D body = bodies[i];
                if (body == null || body.bodyType != RigidbodyType2D.Dynamic) continue;

                // 玩家只吃阻力、不自动漂移，保证操控精准
                if (isPlayerBody[i]) continue;

                driftTimer[i] -= Time.fixedDeltaTime;
                if (driftTimer[i] <= 0f)
                {
                    // 每隔 2~4 秒换一个方向，看起来像"慢慢飘到别处"
                    driftDir[i] = UnityEngine.Random.insideUnitCircle.normalized;
                    driftTimer[i] = UnityEngine.Random.Range(2f, 4f);
                }

                body.AddForce(driftDir[i] * zeroGWander, ForceMode2D.Force);
            }
        }

        public void Toggle(float duration = -1f)
        {
            if (Mode == WorldGravityMode.ZeroG)
            {
                Apply(WorldGravityMode.Normal, 0f);
                return;
            }

            Apply(Mode == WorldGravityMode.Flipped ? WorldGravityMode.Normal : WorldGravityMode.Flipped,
                  duration > 0f ? duration : flipDuration);
        }

        /// <summary>进入失重。</summary>
        public void EnterZeroG(float duration)
        {
            Apply(WorldGravityMode.ZeroG, duration);
            CaptureBodies();
        }

        public void ForceSign(int sign, float duration = 0f)
        {
            Apply(sign >= 0 ? WorldGravityMode.Normal : WorldGravityMode.Flipped, duration);
        }

        private void Apply(WorldGravityMode mode, float duration)
        {
            WorldGravityMode previous = Mode;

            Mode = mode;
            timer = duration;

            float g = 0f;
            if (mode == WorldGravityMode.Normal) g = -normalGravity;
            else if (mode == WorldGravityMode.Flipped) g = normalGravity;

            Physics2D.gravity = new Vector2(0f, g);

            if (previous == WorldGravityMode.ZeroG && mode != WorldGravityMode.ZeroG)
                RestoreDrag();

            OnSignChanged?.Invoke(Sign);
            OnModeChanged?.Invoke(mode);
        }

        /// <summary>进入失重时记录所有动态刚体，并加上阻力、削掉原有冲量。</summary>
        private void CaptureBodies()
        {
            bodies.Clear();
            originalDrag.Clear();

            Rigidbody2D[] found = FindBodies();

            for (int i = 0; i < found.Length; i++)
            {
                Rigidbody2D body = found[i];
                if (body == null || body.bodyType != RigidbodyType2D.Dynamic) continue;

                bodies.Add(body);
                originalDrag.Add(body.drag);
                driftDir.Add(UnityEngine.Random.insideUnitCircle.normalized);
                driftTimer.Add(UnityEngine.Random.Range(0.5f, 2f));
                isPlayerBody.Add(body.GetComponent<Game.Player.PlayerMotor2D>() != null);

                body.drag = zeroGDrag;

                // 统一给一个向上的初速度：所有人一起离地，再各自漂散
                body.velocity = new Vector2(body.velocity.x * zeroGVelocityKeep, zeroGLiftSpeed);
                body.angularVelocity *= zeroGVelocityKeep;
                body.WakeUp();
            }
        }

        private void RestoreDrag()
        {
            for (int i = 0; i < bodies.Count; i++)
            {
                if (bodies[i] != null) bodies[i].drag = originalDrag[i];
            }

            bodies.Clear();
            originalDrag.Clear();
            driftDir.Clear();
            driftTimer.Clear();
            isPlayerBody.Clear();
        }

        private Rigidbody2D[] FindBodies()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<Rigidbody2D>();
#endif
        }
    }
}
