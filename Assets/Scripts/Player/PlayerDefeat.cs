using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Player
{
    /// <summary>
    /// 玩家死亡：暂停游戏 + 显示"你死了"，按 R 重开本关。
    /// 挂在 player 上（同物体需要有 PlayerHealth）。
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerDefeat : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;

        [Tooltip("死亡后是否暂停游戏")]
        [SerializeField] private bool pauseOnDeath = true;

        [SerializeField] private string message = "你死了";
        [SerializeField] private string hint = "按 R 重新开始";

        private bool dead;

        private void Awake()
        {
            if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        }

        private void Start()
        {
            // 放在 Start：等 PlayerHealth 的 Awake 跑完，Health 才有值
            if (playerHealth != null && playerHealth.Health != null)
                playerHealth.Health.OnDied += HandleDied;
        }

        private void OnDestroy()
        {
            if (playerHealth != null && playerHealth.Health != null)
                playerHealth.Health.OnDied -= HandleDied;
        }

        private void HandleDied()
        {
            if (dead) return;
            dead = true;

            Debug.Log("[PlayerDefeat] 玩家死亡");
            if (pauseOnDeath) Time.timeScale = 0f;
        }

        private void Update()
        {
            // 兜底：直接看血量，避免事件没订上就没反应
            if (!dead && playerHealth != null && playerHealth.IsDead) HandleDied();

            if (!dead) return;

            if (Input.GetKeyDown(KeyCode.R))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }

        private void OnGUI()
        {
            if (!dead) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.95f, 0.3f, 0.3f) }
            };
            GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 70f), message, style);

            var tip = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.8f) }
            };
            GUI.Label(new Rect(0f, Screen.height * 0.42f + 62f, Screen.width, 30f), hint, tip);
        }
    }
}
