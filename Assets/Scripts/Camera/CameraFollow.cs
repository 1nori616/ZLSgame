using UnityEngine;

namespace Game.CameraSystem
{
    /// <summary>
    /// 横板跟随摄像机：平滑跟随 + 朝向前方留出视野。
    /// 挂在 Main Camera 上，Target 拖玩家即可。
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);
        [SerializeField] private float smoothTime = 0.12f;
        [Tooltip("人物朝向哪边，镜头就往那边偏一点")]
        [SerializeField] private float lookAheadX = 1.5f;
        [SerializeField] private float lookAheadSmooth = 2f;

        private Vector3 velocity;
        private float currentLookAhead;

        private void Awake()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            float targetAhead = 0f;
            var motor = target.GetComponent<Game.Player.PlayerMotor2D>();
            if (motor != null) targetAhead = motor.Facing * lookAheadX;

            currentLookAhead = Mathf.Lerp(currentLookAhead, targetAhead, lookAheadSmooth * Time.deltaTime);

            Vector3 desired = target.position + offset + new Vector3(currentLookAhead, 0f, 0f);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        }
    }
}
