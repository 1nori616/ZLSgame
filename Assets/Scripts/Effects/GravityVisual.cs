using Game.Gravity;
using UnityEngine;

namespace Game.Effects
{
    /// <summary>
    /// 重力翻转时把贴图上下翻过来。
    /// 前提：贴图枢轴在**中心**（0.5, 0.5），碰撞体 Offset 为 (0,0)。
    /// 这样翻转只是镜像，不会移动任何物理物体，因此不会卡进天花板。
    /// </summary>
    public class GravityVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer[] renderers;

        private int appliedSign = int.MinValue;

        private void Awake()
        {
            RefreshRenderers();
        }

        private void Update()
        {
            GravityWorld world = GravityWorld.Instance;
            if (world == null) return;

            if (world.Sign != appliedSign) Apply(world.Sign);
        }

        private void RefreshRenderers()
        {
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void Apply(int sign)
        {
            RefreshRenderers();
            if (renderers == null) return;

            appliedSign = sign;
            bool flip = sign < 0;

            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].flipY = flip;
        }
    }
}
