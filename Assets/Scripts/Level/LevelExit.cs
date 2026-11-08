using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Level
{
    /// <summary>
    /// 关卡终点：
    ///   - 配置了 nextScene → 短暂停留后加载下一关
    ///   - 没配置（最后一关）→ 显示"游戏结束 / 感谢游玩"
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LevelExit : MonoBehaviour
    {
        [Tooltip("玩家所在的 Tag")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("下一关的场景名；留空表示这是最后一关")]
        [SerializeField] private string nextScene = "";

        [Tooltip("通关提示文字")]
        [SerializeField] private string message = "关卡完成！";

        [Tooltip("停留多久再切关（真实秒数）")]
        [SerializeField] private float holdSeconds = 1.1f;

        private bool completed;

        public static event System.Action OnLevelCompleted;

        public bool Completed => completed;

        /// <summary>给关卡生成器用：设置下一关。</summary>
        public void SetNextScene(string sceneName)
        {
            nextScene = sceneName ?? "";
        }

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

            bool isPlayer = other.CompareTag(playerTag)
                            || other.GetComponentInParent<Game.Player.PlayerHealth>() != null;
            if (!isPlayer) return;

            Complete();
        }

        private void Complete()
        {
            completed = true;

            Debug.Log($"[LevelExit] 通关：{message}（下一关：{(string.IsNullOrEmpty(nextScene) ? "无（最终关）" : nextScene)}）");
            OnLevelCompleted?.Invoke();

            Game.Audio.SoundPlayer.Play("whoosh", 0.7f);
            StartCoroutine(Advance());
        }

        private IEnumerator Advance()
        {
            yield return new WaitForSecondsRealtime(holdSeconds);

            if (!string.IsNullOrEmpty(nextScene))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(nextScene);
                yield break;
            }

            // 最后一关：定格在结算画面
            Time.timeScale = 0f;
        }

        private void OnGUI()
        {
            if (!completed) return;
            if (!string.IsNullOrEmpty(nextScene) && !IsLastFrame()) return;

            bool isFinal = string.IsNullOrEmpty(nextScene);

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = isFinal ? 64 : 52,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.3f) }
            };

            string text = isFinal ? "游戏结束" : message;
            GUI.Label(new Rect(0, Screen.height * 0.36f, Screen.width, 90f), text, title);

            if (!isFinal) return;

            var thanks = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.92f) }
            };
            GUI.Label(new Rect(0, Screen.height * 0.36f + 84f, Screen.width, 40f),
                "感谢你游玩此 Demo", thanks);

            var tip = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.65f) }
            };
            GUI.Label(new Rect(0, Screen.height * 0.36f + 132f, Screen.width, 30f),
                "按 R 从头再来", tip);

            if (Input.GetKeyDown(KeyCode.R))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(0);
            }
        }

        // 让"关卡完成"提示在切关前的短暂停留里也画出来
        private static bool IsLastFrame()
        {
            return true;
        }
    }
}
