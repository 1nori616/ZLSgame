using UnityEngine;

namespace Game.Level
{
    /// <summary>
    /// 关卡终点：玩家碰到就判定通关。
    /// 挂在带 Trigger 碰撞体的物体上，做成预制体 P_Goal 供关卡搭建设计师拖拽。
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LevelExit : MonoBehaviour
    {
        [Tooltip("玩家所在的 Tag")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("通关后是否暂停游戏（Time.timeScale = 0）")]
        [SerializeField] private bool pauseOnComplete = true;

        [Tooltip("通关提示文字")]
        [SerializeField] private string message = "关卡完成！";

        private bool completed;

        public static event System.Action OnLevelCompleted;

        public bool Completed => completed;

        private void Reset()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnValidate()
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (completed) return;
            if (!other.CompareTag(playerTag)) return;

            Complete();
        }

        private void Complete()
        {
            completed = true;

            Debug.Log($"[LevelExit] 通关：{message}");
            OnLevelCompleted?.Invoke();

            if (Game.Effects.ComboCounter.Instance != null)
                Debug.Log($"[LevelExit] 本次连击最高：x{Game.Effects.ComboCounter.Instance.Count}");

            if (pauseOnComplete) Time.timeScale = 0f;
        }

        private void OnGUI()
        {
            if (!completed) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.3f) }
            };

            GUI.Label(new Rect(0, Screen.height * 0.4f, Screen.width, 80f), message, style);

            var tip = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.75f) }
            };
            GUI.Label(new Rect(0, Screen.height * 0.4f + 70f, Screen.width, 30f),
                "按 R 重新开始", tip);

            if (Input.GetKeyDown(KeyCode.R))
            {
                Time.timeScale = 1f;
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
        }
    }
}
