using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Level
{
    /// <summary>
    /// 开始界面：整屏显示封面图，点击"开始游戏"（或按空格/回车）才进入游戏。
    /// 由关卡生成器加到"第一个关卡"上，不需要单独的场景。
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        [Tooltip("封面图（放在 Assets/Resources/Cover.png 就会自动读取）")]
        [SerializeField] private Texture2D cover;

        [Tooltip("按钮文字")]
        [SerializeField] private string startLabel = "开始游戏";

        /// <summary>封面是否正在显示（开场演出要等它关闭）。</summary>
        public static bool IsShowing { get; private set; }

        private bool started;

        private void Awake()
        {
            IsShowing = true;

            if (cover == null) cover = Resources.Load<Texture2D>("Cover");

            // 开始界面期间冻结游戏，玩家不能乱动
            Time.timeScale = 0f;
        }

        private void Update()
        {
            if (started) return;

            if (Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                StartGame();
            }
        }

        private void StartGame()
        {
            if (started) return;
            started = true;
            IsShowing = false;

            Time.timeScale = 1f;
            Game.Audio.SoundPlayer.Play("whoosh", 0.6f);
            Destroy(this);
        }

        private void OnGUI()
        {
            if (started) return;

            float sw = Screen.width;
            float sh = Screen.height;

            // 封面：等比铺满（超出部分裁掉）
            if (cover != null)
            {
                float texAspect = cover.width / (float)cover.height;
                float screenAspect = sw / sh;

                Rect r;
                if (texAspect > screenAspect)
                {
                    float w = sh * texAspect;
                    r = new Rect((sw - w) * 0.5f, 0f, w, sh);
                }
                else
                {
                    float h = sw / texAspect;
                    r = new Rect(0f, (sh - h) * 0.5f, sw, h);
                }

                GUI.DrawTexture(r, cover, ScaleMode.StretchToFill, false);
            }
            else
            {
                GUI.DrawTexture(new Rect(0, 0, sw, sh), Texture2D.blackTexture);
            }

            // 底部压暗，保证按钮清楚
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(0, sh * 0.72f, sw, sh * 0.28f), Texture2D.whiteTexture);
            GUI.color = old;

            // 开始按钮
            float bw = Mathf.Min(340f, sw * 0.32f);
            float bh = 76f;
            Rect button = new Rect((sw - bw) * 0.5f, sh * 0.80f, bw, bh);

            bool hover = button.Contains(Event.current.mousePosition);

            // 按钮底
            GUI.color = hover ? new Color(0.85f, 0.28f, 0.24f, 0.95f) : new Color(0.16f, 0.2f, 0.28f, 0.92f);
            GUI.DrawTexture(button, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(button.x, button.y, button.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(button.x, button.yMax - 2f, button.width, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(button.x, button.y, 2f, button.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(button.xMax - 2f, button.y, 2f, button.height), Texture2D.whiteTexture);
            GUI.color = old;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.95f) }
            };
            GUI.Label(button, startLabel, style);

            // 提示
            var tip = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.6f) }
            };
            GUI.Label(new Rect(0, button.yMax + 8f, sw, 24f), "空格 / 回车 也可以开始", tip);

            if (Event.current.type == EventType.MouseDown && button.Contains(Event.current.mousePosition))
            {
                StartGame();
                Event.current.Use();
            }
        }
    }
}
