using UnityEngine;

namespace Game
{
    /// <summary>
    /// 启动设置：限帧 + 开垂直同步。
    /// 不用挂到场景里，Unity 启动时自动执行。
    /// 不限帧的话 Unity 会跑满 1000+ FPS，CPU/GPU 满载，风扇狂转。
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            // 60 帧足够；想更顺滑可以改 120
            Application.targetFrameRate = 60;

            // 垂直同步：和显示器刷新对齐，最省电
            QualitySettings.vSyncCount = 1;

            // 后台时不渲染（切出去就降负载）
            Application.runInBackground = false;
        }
    }
}
