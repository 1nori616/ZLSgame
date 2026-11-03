using Game.Gravity;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 玩家的重力技能（充能制，自动回复）：
    ///   Q = 对目标施放反重力（飘起）
    ///   E = 对目标施放加重（砸下，会打断漂浮）
    ///   R = 对自己施放反重力（反重力跳）
    ///   F = 翻转整个房间的重力（10 秒后自动恢复）
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
        private float flipCooldownTimer;

        public int Charges => charges;
        public int MaxCharges => config != null ? config.gravityCharges : 3;
        public bool IsReady => charges > 0;
        public bool CanFlip => flipCooldownTimer <= 0f;
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
            if (flipCooldownTimer > 0f) flipCooldownTimer -= Time.deltaTime;

            PlayerInputData data = input.Read();

            if (data.AntiDown) TryCastOnTarget(GravityMode.AntiGravity);
            if (data.HeavyDown) TryCastOnTarget(GravityMode.Heavy);
            if (data.SelfDown) TryCastOnSelf();
            if (data.FlipDown) TryFlip();
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

        /// <summary>翻转整个房间的重力，duration 秒后自动恢复。</summary>
        public bool TryFlip()
        {
            if (!CanFlip || GravityWorld.Instance == null) return false;

            GravityWorld.Instance.Toggle(config.flipDuration);
            flipCooldownTimer = config.flipCooldown;
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
            if (config == null) return null;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, config.castRadius, config.castLayer);
            float facing = motor != null ? motor.Facing : 1f;

            GravityAffected best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;

                GravityAffected candidate = hits[i].GetComponentInParent<GravityAffected>();
                if (candidate == null || candidate.transform.IsChildOf(transform)) continue;
                if (!candidate.IsValidTarget) continue;

                Combat.Health hp = candidate.GetComponent<Combat.Health>();
                if (hp != null && hp.IsDead) continue;

                Vector2 delta = (Vector2)candidate.transform.position - (Vector2)transform.position;
                float distance = delta.magnitude;

                bool inFront = Mathf.Abs(delta.x) < 0.2f || Mathf.Sign(delta.x) == Mathf.Sign(facing);
                float score = distance * (inFront ? config.frontPreference : 1f);

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private void OnGUI()
        {
            if (config == null) return;

            string pip = "";
            for (int i = 0; i < config.gravityCharges; i++)
                pip += i < charges ? "●" : "○";

            string text = $"重力 {pip}";
            if (lockedTarget != null) text += $"  [锁定: {lockedTarget.name} x{lockedTarget.ChainCount}]";

            if (GravityWorld.Instance != null && GravityWorld.Instance.IsFlipped)
                text += $"  [翻转中 {GravityWorld.Instance.RemainingTime:0.0}s]";
            else if (!CanFlip)
                text += "  [翻转冷却]";

            GUILayout.BeginArea(new Rect(12f, 46f, 520f, 50f));
            GUILayout.Label(text);
            GUILayout.EndArea();
        }
    }
}
