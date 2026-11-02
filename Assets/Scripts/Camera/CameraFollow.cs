using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// 横板跟随摄像机：平滑跟随 + 朝向预判 + 技能聚焦。
    /// 平时跟玩家；调用 FocusOn() 后，镜头移到"玩家与目标的中点"并拉远，时间到自动回位。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);
        [SerializeField] private float smoothTime = 0.12f;

        [Header("朝向预判")]
        [Tooltip("人物朝向哪边，镜头就往那边偏一点")]
        [SerializeField] private float lookAheadX = 1.5f;
        [SerializeField] private float lookAheadSmooth = 2f;

        [Header("技能聚焦")]
        [Tooltip("聚焦时正交尺寸增加多少（越大拉得越远）")]
        [SerializeField] private float focusZoomOut = 1.5f;

        [Tooltip("聚焦过渡速度")]
        [SerializeField] private float focusBlend = 6f;

        private Vector3 velocity;
        private float currentLookAhead;
        private Camera cam;
        private float baseSize;
        private Transform focusTarget;
        private float focusTimer;

        public Transform FocusTarget => focusTarget;
        public bool IsFocusing => focusTarget != null && focusTimer > 0f;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null) baseSize = cam.orthographicSize;

            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        /// <summary>聚焦到某个目标：镜头移到两者中点并拉远，duration 秒后回位。</summary>
        public void FocusOn(Transform focus, float duration)
        {
            if (focus == null) return;
            focusTarget = focus;
            focusTimer = Mathf.Max(0.01f, duration);
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

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

            if (cam != null && cam.orthographic)
            {
                float targetSize = baseSize + (IsFocusing ? focusZoomOut : 0f);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, focusBlend * Time.deltaTime);
            }
        }
    }
}
