using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// 横板跟随摄像机：平滑跟随 + 朝向预判 + 技能聚焦 + 受击震屏。
    /// 相机位置的唯一拥有者——其它系统只能调用 Shake()，不要直接改 transform。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance { get; private set; }

        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.4f, -10f);
        [SerializeField] private float smoothTime = 0.12f;

        [Header("边界")]
        [Tooltip("镜头最低高度：低于这个值就锁住，避免露出地板下方的空白")]
        [SerializeField] private float minCameraY = 4.2f;

        [Header("朝向预判")]
        [Tooltip("人物朝向哪边，镜头就往那边偏一点")]
        [SerializeField] private float lookAheadX = 1.5f;
        [SerializeField] private float lookAheadSmooth = 2f;

        [Header("技能聚焦")]
        [Tooltip("聚焦时正交尺寸增加多少（越大拉得越远）")]
        [SerializeField] private float focusZoomOut = 1.5f;
        [SerializeField] private float focusBlend = 6f;

        [Header("受击震屏")]
        [Tooltip("最大抖动位移（世界单位）")]
        [SerializeField] private float maxShakeOffset = 0.35f;

        [Tooltip("创伤值每秒衰减多少，越大平息越快")]
        [SerializeField] private float shakeDecay = 2.6f;

        private Vector3 followPosition;
        private Vector3 velocity;
        private float currentLookAhead;

        private Camera cam;
        private float baseSize;
        private Transform focusTarget;
        private float focusTimer;
        private float focusZoom;

        private float trauma;

        public Transform FocusTarget => focusTarget;
        public bool IsFocusing => focusTarget != null && focusTimer > 0f;
        public float Trauma => trauma;

        private void Awake()
        {
            Instance = this;
            followPosition = transform.position;

            cam = GetComponent<Camera>();
            if (cam != null) baseSize = cam.orthographicSize;

            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>把内部跟随位置直接挪到某个点，避免启用瞬间瞬移。</summary>
        public void WarpTo(Vector3 position)
        {
            followPosition = position;
            velocity = Vector3.zero;
        }

        /// <summary>临时改变跟随平滑时间（做镜头过渡用）。</summary>
        public void SetSmoothTime(float time)
        {
            smoothTime = Mathf.Max(0.02f, time);
        }

        /// <summary>触发震屏。amount 0~1，多次调用会叠加（有上限）。</summary>
        public void Shake(float amount)
        {
            trauma = Mathf.Clamp(trauma + amount, 0f, 1f);
        }

        public void FocusOn(Transform focus, float duration, float zoomDelta = 0f)
        {
            if (focus == null) return;
            focusTarget = focus;
            focusTimer = Mathf.Max(0.01f, duration);
            focusZoom = zoomDelta;
        }

        public void ClearFocus()
        {
            focusTarget = null;
            focusTimer = 0f;
        }

        private void LateUpdate()
        {
            if (focusTimer > 0f)
            {
                focusTimer -= Time.deltaTime;
                if (focusTimer <= 0f) focusTarget = null;
            }

            if (target == null) return;

            Vector3 desired;
            if (focusTarget != null)
            {
                desired = (target.position + focusTarget.position) * 0.5f + offset;
                currentLookAhead = 0f;
            }
            else
            {
                var motor = target.GetComponent<Game.Player.PlayerMotor2D>();
                float look = motor != null ? motor.Facing * lookAheadX : 0f;
                currentLookAhead = Mathf.Lerp(currentLookAhead, look, lookAheadSmooth * Time.deltaTime);
                desired = target.position + offset + new Vector3(currentLookAhead, 0f, 0f);
            }

            // 跟随只动"逻辑位置"，抖动单独叠加，避免抖动被平滑进跟随里
            followPosition = Vector3.SmoothDamp(followPosition, desired, ref velocity, smoothTime);

            // 锁住最低高度，地板下方不会露白
            if (followPosition.y < minCameraY)
                followPosition.y = minCameraY;

            if (cam != null && cam.orthographic)
            {
                float targetSize = baseSize + (IsFocusing ? focusZoom : 0f);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, focusBlend * Time.deltaTime);
            }

            trauma = Mathf.Max(0f, trauma - shakeDecay * Time.deltaTime);
            float shake = trauma * trauma;
            Vector2 jitter = Random.insideUnitCircle * (maxShakeOffset * shake);

            transform.position = followPosition + new Vector3(jitter.x, jitter.y, 0f);
        }
    }
}
