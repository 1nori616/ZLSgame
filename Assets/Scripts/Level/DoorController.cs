using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// 舱门控制：
    ///   起点门：玩家走远后自动关闭（"你被关在里面了"）
    ///   终点门：玩家靠近时自动打开（通关演出）
    /// 由关卡生成器自动创建并配置贴图。
    /// </summary>
    public class DoorController : MonoBehaviour
    {
        public enum DoorMode
        {
            StartDoor,   // 开 → 关
            EndDoor      // 关 → 开
        }

        [SerializeField] private DoorMode mode = DoorMode.StartDoor;

        [Tooltip("初始贴图（起点=开，终点=关）")]
        [SerializeField] private Sprite initialState;

        [Tooltip("触发后的贴图")]
        [SerializeField] private Sprite triggeredState;

        [Tooltip("玩家走到这个距离内/外就切换")]
        [SerializeField] private float triggerDistance = 3.5f;

        private SpriteRenderer spriteRenderer;
        private Transform player;
        private bool triggered;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null && initialState != null)
                spriteRenderer.sprite = initialState;
        }

        private void Update()
        {
            if (triggered) return;

            if (player == null)
            {
                GameObject go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
                return;
            }

            float dist = Vector2.Distance(player.position, transform.position);
            bool shouldTrigger = mode == DoorMode.StartDoor
                ? dist > triggerDistance      // 走远了就关门
                : dist < triggerDistance;     // 靠近了就开门

            if (!shouldTrigger) return;

            triggered = true;
            if (spriteRenderer != null && triggeredState != null)
                spriteRenderer.sprite = triggeredState;

            Game.Audio.SoundPlayer.Play("whoosh", 0.6f);
        }

        /// <summary>给生成器用。</summary>
        public void Setup(DoorMode newMode, Sprite initial, Sprite after, float distance)
        {
            mode = newMode;
            initialState = initial;
            triggeredState = after;
            triggerDistance = distance;
        }
    }
}
