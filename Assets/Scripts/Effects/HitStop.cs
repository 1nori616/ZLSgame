using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// 命中顿帧：把时间短暂冻结几十毫秒，制造"打到实感"。
    /// 不需要手动挂载——第一次调用时自动创建。
    /// 会记住冻结前的 timeScale，结束后恢复（所以和锁定慢动作兼容）。
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        private static HitStop instance;

        private float timer;
        private float restoreScale = 1f;

        /// <summary>冻结 duration 秒（真实时间）。重复调用会取较长的那次。</summary>
        public static void Freeze(float duration)
        {
            if (duration <= 0f) return;

            if (instance == null)
            {
                GameObject go = new GameObject("HitStop");
                instance = go.AddComponent<HitStop>();
            }

            instance.DoFreeze(duration);
        }

        private void DoFreeze(float duration)
        {
            if (timer <= 0f)
            {
                restoreScale = Mathf.Approximately(Time.timeScale, 0f) ? 1f : Time.timeScale;
            }

            timer = Mathf.Max(timer, duration);
            Time.timeScale = 0f;
        }

        private void Update()
        {
            if (timer <= 0f) return;

            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f)
            {
                Time.timeScale = restoreScale;
                restoreScale = 1f;
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
                Time.timeScale = 1f;
            }
        }
    }
}
