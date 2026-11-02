using Game.Gravity;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 玩家的重力操控技能（充能制，可以连按）：
    ///   Q = 对目标施放反重力（往上飘）
    ///   E = 对目标施放加重（往下砸，会打断漂浮）
    ///   R = 对自己施放反重力（反重力跳 / 滞空）
    ///
    /// 目标选择：优先"锁定目标"，失效后才在身前搜索。
    /// 施放成功时会让相机聚焦到目标身上一小段时间。
    /// </summary>
    public class GravityShift : MonoBehaviour
    {
        [SerializeField] private PlayerInput input;
        [SerializeField] private PlayerMotor2D motor;
        [SerializeField] private PlayerConfig config;

        [Tooltip("留空会自动找 Camera.main 上的 CameraFollow")]
        [SerializeField] private Game.CameraSystem.CameraFollow cameraFollow;

        [Tooltip("锁定目标在重力持续时间之外还能保留多久（秒）")]
        [SerializeField] private float targetLockExtra = 0.5f;

        private int charges;
        private float rechargeTimer;
        private GravityAffected lockedTarget;
        private float lockTimer;

        public int Charges => charges;
        public int MaxCharges => config != null ? config.gravityCharges : 3;
        public bool IsReady => charges > 0;
        public GravityAffected LockedTarget => lockedTarget;
        public float RechargeProgress =>
            config == null ? 0f : 1f - Mathf.Clamp01(rechargeTimer / Mathf.Max(0.01f, config.gravityRechargeTime));

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (motor == null) motor = GetComponent<PlayerMotor2D>();
            if (config == null && motor != null) config = motor.Config;
            if (config != null) charges = config.gravityCharges;

            if (cameraFollow == null && Camera.main != null)
                cameraFollow = Camera.main.GetComponent<Game.CameraSystem.CameraFollow>();
        }

        private void Update()
        {
            if (config == null || input == null) return;

            TickRecharge();
            TickLock();

            PlayerInputData data = input.Read();

            if (data.AntiDown) TryCastOnTarget(GravityMode.AntiGravity);
            if (data.HeavyDown) TryCastOnTarget(GravityMode.Heavy);
            if (data.SelfDown) TryCastOnSelf();
        }

        private void TickRecharge()
        {
            if (charges >= config.gravityCharges)
            {
                rechargeTimer = 0f;
                return;
            }

            rechargeTimer -= Time.deltaTime;
            if (rechargeTimer <= 0f)
            {
                charges++;
                rechargeTimer = charges >= config.gravityCharges ? 0f : config.gravityRechargeTime;
            }
        }

        private void TickLock()
        {
            if (lockTimer > 0f)
            {
                lockTimer -= Time.deltaTime;
                if (lockTimer <= 0f) lockedTarget = null;
            }

            if (lockedTarget != null && !lockedTarget.IsValidTarget) lockedTarget = null;
        }

        private void ConsumeCharge()
        {
            if (charges == config.gravityCharges) rechargeTimer = config.gravityRechargeTime;
            charges = Mathf.Max(0, charges - 1);
        }

        public bool TryCastOnTarget(GravityMode mode)
        {
            if (!IsReady) return false;

            GravityAffected target = ResolveTarget();
            if (target == null) return false;

            target.ApplyOverride(config.gravityDuration, mode);
            ConsumeCharge();

            lockedTarget = target;
            lockTimer = config.gravityDuration + targetLockExtra;

            FocusCamera(target.transform);
            return true;
        }

        public bool TryCastOnSelf()
        {
            if (!IsReady || motor == null) return false;

            motor.ApplyGravityOverride(config.selfAntiAcceleration, config.selfAntiMaxSpeed, config.gravityDuration);
            ConsumeCharge();
            return true;
        }

        private void FocusCamera(Transform targetTransform)
        {
            if (cameraFollow == null) return;
            cameraFollow.FocusOn(targetTransform, config.cameraFocusDuration);
        }

        private GravityAffected ResolveTarget()
        {
            if (lockedTarget != null && lockedTarget.IsValidTarget) return lockedTarget;

            lockedTarget = null;
            return FindTarget();
        }

        private GravityAffected FindTarget()
        {
            float facing = motor != null ? motor.Facing : 1f;
            Vector2 center = (Vector2)transform.position
                             + new Vector2(facing * config.castRange * 0.5f, config.castOffsetY);
            Vector2 size = new Vector2(config.castRange, config.castHeight);

            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, config.castLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;

                GravityAffected target = hits[i].GetComponentInParent<GravityAffected>();
                if (target != null && !target.transform.IsChildOf(transform)) return target;
            }

            return null;
        }

        private void OnGUI()
        {
            if (config == null) return;

            string pip = "";
            for (int i = 0; i < config.gravityCharges; i++)
                pip += i < charges ? "●" : "○";

            string text = charges > 0 ? $"重力 {pip}" : $"重力 {pip}  {RechargeProgress * 100f:0}%";
            if (lockedTarget != null)
                text += $"  [锁定: {lockedTarget.name} x{lockedTarget.ChainCount}]";

            GUILayout.BeginArea(new Rect(12f, 46f, 420f, 50f));
            GUILayout.Label(text);
            GUILayout.EndArea();
        }
    }
}
