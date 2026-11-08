using System.Collections.Generic;
using Game.Gravity;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// 玩家的重力技能（充能制，自动回复）：
    ///   Q = 对目标施放反重力（飘起）
    ///   E = 对目标施放加重（砸下，会打断漂浮）
    ///   R = 对自己施放反重力
    ///   F = 翻转整个房间的重力
    ///   Tab = 切换手动锁定目标（锁定后 Q/E 只作用于它）
    /// </summary>
    public class GravityShift : MonoBehaviour
    {
        [SerializeField] private PlayerInput input;
        [SerializeField] private PlayerMotor2D motor;
        [SerializeField] private PlayerConfig config;

        [Tooltip("留空会自动找 Camera.main 上的 CameraFollow")]
        [SerializeField] private Game.CameraSystem.CameraFollow cameraFollow;

        [Tooltip("留空会自动找 Main Camera 上的 Vignette")]
        [SerializeField] private Game.Effects.Vignette vignette;

        [Tooltip("左上角那行调试文字（重力 ●●○ / 锁定 / 冷却），正式版建议关掉")]
        [SerializeField] private bool showDebugHud = false;

        [Tooltip("锁定目标在重力持续时间之外还能保留多久（秒）")]
        [SerializeField] private float targetLockExtra = 0.5f;

        private int charges;
        private float rechargeTimer;
        private float flipCooldownTimer;
        [Tooltip("对自己施放要消耗几格充能")]
        [SerializeField] private int selfChargeCost = 4;   // 最少 4 格

        [Tooltip("自锁之后的冷却时间（秒），防止反复逃课")]
        [SerializeField] private float selfCastCooldown = 6f;

        private float selfCastCooldownTimer;

        [Tooltip("鼠标瞄准灵敏度：越大越容易甩出大角度（1 = 原始）")]
        [Range(0.3f, 3f)]
        [SerializeField] private float aimSensitivity = 1.6f;

        [Tooltip("鼠标离角色小于这个距离时，沿用上一次的方向，避免乱抖")]
        [SerializeField] private float aimDeadZone = 1.2f;

        private Vector2 lastAim = Vector2.right;
        private Vector2 lastPointer;      // 最近一次鼠标屏幕坐标（E 的甩出方向用它）

        private float zeroGCharge;
        private bool zeroGVisualOn;
        private Game.Gravity.GravityAffected selfGravity;

        [Tooltip("对自己按 E 时的向下加速度")]
        [SerializeField] private float selfHeavyAcceleration = 55f;

        [Tooltip("对自己按 E 时的最大下落速度")]
        [SerializeField] private float selfHeavyMaxSpeed = 26f;
        private float zeroGCooldownTimer;

        private GravityAffected manualLock;   // Tab 选中的目标
        private GravityAffected lastCast;     // 上一次施放的对象（自动续接）
        private float lastCastTimer;

        public int Charges => charges;
        public int MaxCharges => config != null ? config.gravityCharges : 3;
        public bool IsReady => charges > 0;
        public bool CanFlip => flipCooldownTimer <= 0f;
        public bool CanZeroG => zeroGCooldownTimer <= 0f;
        public float ZeroGCharge01 => config != null ? Mathf.Clamp01(zeroGCharge / Mathf.Max(0.01f, config.zeroGChargeTime)) : 0f;

        /// <summary>当前生效的目标：手动锁定优先。</summary>
        public GravityAffected LockedTarget => IsValid(manualLock) ? manualLock : (IsValid(lastCast) ? lastCast : null);
        public bool HasManualLock => IsValid(manualLock);

        /// <summary>当前锁定的是不是自己。</summary>
        public bool IsSelfLocked => IsValid(manualLock) && manualLock == selfGravity;

        /// <summary>自锁需要多少格充能。</summary>
        public int SelfChargeCost => Mathf.Max(4, selfChargeCost);

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInput>();
            if (motor == null) motor = GetComponent<PlayerMotor2D>();
            if (config == null && motor != null) config = motor.Config;
            if (config != null) charges = config.gravityCharges;

            if (cameraFollow == null && Camera.main != null)
                cameraFollow = Camera.main.GetComponent<Game.CameraSystem.CameraFollow>();

            selfGravity = GetComponent<Game.Gravity.GravityAffected>();

            if (vignette == null && Camera.main != null)
                vignette = Camera.main.GetComponent<Game.Effects.Vignette>();
        }

        private void Update()
        {
            if (config == null || input == null) return;

            if (selfCastCooldownTimer > 0f) selfCastCooldownTimer -= Time.deltaTime;

            TickRecharge();
            TickTimers();

            PlayerInputData data = input.Read();

            lastPointer = data.Pointer;
            if (data.ClickDown) LockByPointer(data.Pointer);
            if (data.AntiDown) TryCastOnTarget(GravityMode.AntiGravity);
            if (data.HeavyDown) TryCastOnTarget(GravityMode.Heavy);
            UpdateZeroG(data);
            TickZeroGVisual();
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

        private void TickTimers()
        {
            if (flipCooldownTimer > 0f) flipCooldownTimer -= Time.deltaTime;

            if (lastCastTimer > 0f)
            {
                lastCastTimer -= Time.deltaTime;
                if (lastCastTimer <= 0f) lastCast = null;
            }

            // 手动锁定失效就自动解除
            if (manualLock != null && !IsValid(manualLock))
            {
                manualLock = null;
                SetAim(false);
            }
            if (manualLock != null && DistanceTo(manualLock) > config.lockRange)
            {
                manualLock = null;
                SetAim(false);
            }
        }

        private bool IsValid(GravityAffected target)
        {
            return target != null && target.IsValidTarget;
        }

        private float DistanceTo(GravityAffected target)
        {
            if (target == null) return float.MaxValue;
            return Vector2.Distance(transform.position, target.transform.position);
        }

        [Tooltip("鼠标点击容差：点到这个半径内的目标才算选中")]
        [SerializeField] private float clickRadius = 0.8f;

        [Header("锁定慢动作")]
        [Tooltip("锁定时的时间流速，0.35 = 三成速度")]
        [Range(0.05f, 1f)]
        [SerializeField] private float aimTimeScale = 0.35f;

        [Tooltip("锁定时镜头聚焦的时长（够长即可）")]
        [SerializeField] private float aimFocusDuration = 999f;

        [Tooltip("锁定时镜头推近多少（正交尺寸减少量，越大越近）")]
        [SerializeField] private float aimZoomIn = 1.4f;

        private bool aiming;

        /// <summary>进入/退出"瞄准慢动作"：慢时间 + 镜头推近目标。</summary>
        private void SetAim(bool on)
        {
            if (aiming == on) return;
            aiming = on;

            Time.timeScale = on ? aimTimeScale : 1f;

            // 世界变慢，但玩家保持正常速度 → "我快、世界慢"的子弹时间感
            if (motor != null) motor.SetSpeedMultiplier(on ? 1f / Mathf.Max(0.05f, aimTimeScale) : 1f);

            // 瞄准用黑色暗角；失重用淡蓝，这里必须显式指定颜色，否则会沿用上一次的
            if (vignette != null)
            {
                if (on) vignette.SetActive(true, 0.72f, Color.black);
                else if (GravityWorld.Instance != null && GravityWorld.Instance.IsZeroG)
                    vignette.SetActive(true, 0.55f, new Color(0.35f, 0.62f, 0.95f));
                else vignette.SetActive(false);
            }

            if (cameraFollow == null) return;

            if (on && IsValid(manualLock))
                cameraFollow.FocusOn(manualLock.transform, aimFocusDuration, -aimZoomIn);
            else
                cameraFollow.ClearFocus();
        }

        private void OnDisable()
        {
            if (aiming)
            {
                aiming = false;
                Time.timeScale = 1f;
                if (motor != null) motor.SetSpeedMultiplier(1f);
                if (vignette != null) vignette.SetActive(false);
                if (cameraFollow != null) cameraFollow.ClearFocus();
            }
        }

        /// <summary>鼠标左键：点中谁就锁谁，点空地取消锁定。</summary>
        public void LockByPointer(Vector2 screenPosition)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float depth = -cam.transform.position.z;
            Vector3 screen = new Vector3(screenPosition.x, screenPosition.y, depth);
            Vector2 world = cam.ScreenToWorldPoint(screen);

            Collider2D[] hits = Physics2D.OverlapCircleAll(world, clickRadius, config.castLayer);
            GravityAffected best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                GravityAffected candidate;

                // 点到自己：锁自己（需要 player 上有 GravityAffected）
                if (hits[i].transform.IsChildOf(transform))
                {
                    candidate = selfGravity;
                }
                else
                {
                    candidate = hits[i].GetComponentInParent<GravityAffected>();
                    if (candidate != null && candidate.transform.IsChildOf(transform)) candidate = null;
                }

                if (candidate == null || !candidate.IsValidTarget) continue;

                Combat.Health hp = candidate.GetComponent<Combat.Health>();
                if (hp != null && hp.IsDead) continue;

                float d = Vector2.Distance(world, candidate.transform.position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = candidate;
                }
            }

            manualLock = best;   // null = 点空地，取消锁定
            SetAim(manualLock != null);
        }

        /// <summary>Tab：在范围内按距离循环切换锁定目标。</summary>
        public void SelectNextLock()
        {
            List<GravityAffected> candidates = Collect(transform.position, config.lockRange);
            if (candidates.Count == 0)
            {
                manualLock = null;
                return;
            }

            if (manualLock == null || !candidates.Contains(manualLock))
            {
                manualLock = candidates[0];
                return;
            }

            int index = candidates.IndexOf(manualLock);
            manualLock = candidates[(index + 1) % candidates.Count];
        }

        /// <summary>收集范围内所有可施放目标，按距离从近到远排序。</summary>
        private List<GravityAffected> Collect(Vector2 center, float radius)
        {
            List<GravityAffected> result = new List<GravityAffected>();
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, config.castLayer);

            for (int i = 0; i < hits.Length; i++)
            {
                GravityAffected candidate;

                // 点到自己：锁自己（需要 player 上有 GravityAffected）
                if (hits[i].transform.IsChildOf(transform))
                {
                    candidate = selfGravity;
                }
                else
                {
                    candidate = hits[i].GetComponentInParent<GravityAffected>();
                    if (candidate != null && candidate.transform.IsChildOf(transform)) candidate = null;
                }

                if (candidate == null || !candidate.IsValidTarget) continue;
                if (result.Contains(candidate)) continue;

                Combat.Health hp = candidate.GetComponent<Combat.Health>();
                if (hp != null && hp.IsDead) continue;

                result.Add(candidate);
            }

            result.Sort((a, b) =>
                Vector2.Distance(center, a.transform.position)
                .CompareTo(Vector2.Distance(center, b.transform.position)));

            return result;
        }

        public bool TryCastOnTarget(GravityMode mode)
        {
            if (!IsReady) return false;

            // 严格模式：没有锁定目标就不能施放
            if (!IsValid(manualLock)) return false;
            if (DistanceTo(manualLock) > config.lockRange) return false;

            GravityAffected target = manualLock;

            // 自锁需要足够充能，不够就不放
            int selfCost = Mathf.Max(4, selfChargeCost);
            if (target == selfGravity && charges < selfCost) return false;
            if (target == selfGravity && selfCastCooldownTimer > 0f) return false;

            if (target == selfGravity)
            {
                // 对自己施放：走 PlayerMotor 的通道，不碰别人的 GravityAffected
                if (mode == GravityMode.Heavy)
                {
                    // 往下砸：给一个很强的向下加速度
                    motor.ApplyGravityOverride(-selfHeavyAcceleration, selfHeavyMaxSpeed, config.gravityDuration);
                    selfGravity.Stamp();      // 开启"落地摔伤"判定
                }
                else
                {
                    // 往上飘：限制最大升高；不 Stamp，落地不算"被甩摔伤"
                    motor.StartSelfLift(config.selfAntiMaxHeight);
                    motor.ApplyGravityOverride(config.selfAntiAcceleration, config.selfAntiMaxSpeed, config.gravityDuration);
                }
            }
            else
            {
                target.ApplyOverride(config.gravityDuration, mode, BuildImpulse(mode));
            }
            Game.Audio.SoundPlayer.Play("whoosh", 0.8f);

            // 对自己施放要花更多充能（削弱"自锁飞行"）
            int cost = target == selfGravity ? selfCost : 1;
            Debug.Log($"[SelfCast] v4 字段值={selfChargeCost} 是否自锁={target == selfGravity} 消耗={cost} 消耗前充能={charges}");
            for (int i = 0; i < cost; i++) ConsumeCharge();

            // 自锁后进入冷却，防止反复拿来逃课
            if (target == selfGravity) selfCastCooldownTimer = selfCastCooldown;

            Debug.Log($"[SelfCast] 消耗后充能={charges} 冷却={selfCastCooldownTimer:0.0}s");

            lastCast = target;
            lastCastTimer = config.gravityDuration + targetLockExtra;

            // 一次操作后自动解除锁定：想再连一次，需要重新点击目标
            SetAim(false);
            manualLock = null;
            lastCast = null;
            lastCastTimer = 0f;

            FocusCamera(target.transform);
            return true;
        }

        /// <summary>鼠标当前指向的世界坐标。</summary>
        private Vector2 PointerWorld()
        {
            Camera cam = Camera.main;
            if (cam == null) return transform.position;

            Vector3 world = cam.ScreenToWorldPoint(new Vector3(lastPointer.x, lastPointer.y, 0f));
            return new Vector2(world.x, world.y);
        }

        /// <summary>Q：朝身前拉；E：朝鼠标方向甩出去。</summary>
        private Vector2 BuildImpulse(GravityMode mode)
        {
            float facing = motor != null ? motor.Facing : 1f;

            if (mode == GravityMode.AntiGravity)
                return new Vector2(facing * config.antiThrowX, config.antiThrowUp);

            // E：朝鼠标方向甩出去。
            // 关键：先把偏移量放大（aimSensitivity），再归一化 —— 这样鼠标不用移很远也能出大角度，
            // 小窗口下也不会"怎么划都不够"。
            Vector2 aim = PointerWorld() - (Vector2)transform.position;

            if (aim.magnitude >= aimDeadZone)
                lastAim = aim.normalized;
            else
                aim = lastAim;                       // 鼠标贴脸时保持上次方向，不乱抖

            aim = (aim * aimSensitivity).normalized;

            // 在鼠标方向上再叠一个固定的下砸分量，避免"平推"软绵绵
            return aim * config.heavyThrowX + new Vector2(0f, config.heavyThrowDown);
        }

        private GravityAffected ResolveTarget()
        {
            // 1) 手动锁定优先
            if (IsValid(manualLock) && DistanceTo(manualLock) <= config.lockRange) return manualLock;

            // 2) 上次施放的目标（续接连段）
            if (IsValid(lastCast) && DistanceTo(lastCast) <= config.lockRange) return lastCast;

            // 3) 自动选最近的
            List<GravityAffected> near = Collect(transform.position, config.castRadius);
            return near.Count > 0 ? near[0] : null;
        }


        public bool TryFlip()
        {
            if (!CanFlip || GravityWorld.Instance == null) return false;

            GravityWorld.Instance.Toggle(config.flipDuration);
            flipCooldownTimer = config.flipCooldown;
            return true;
        }

        private void ConsumeCharge()
        {
            if (charges == config.gravityCharges) rechargeTimer = config.gravityRechargeTime;
            charges = Mathf.Max(0, charges - 1);
        }

        /// <summary>失重时给画面加一层淡蓝暗角，让玩家一眼看出状态。</summary>
        private void TickZeroGVisual()
        {
            bool on = GravityWorld.Instance != null && GravityWorld.Instance.IsZeroG;
            if (on == zeroGVisualOn) return;

            zeroGVisualOn = on;
            if (vignette == null) return;

            if (on)
                vignette.SetActive(true, 0.55f, new Color(0.35f, 0.62f, 0.95f));
            else if (!aiming)
                vignette.SetActive(false);
        }

        /// <summary>按住 F 蓄力，满 1.5 秒进入失重；松开取消。</summary>
        private void UpdateZeroG(PlayerInputData data)
        {
            if (zeroGCooldownTimer > 0f) zeroGCooldownTimer -= Time.unscaledDeltaTime;

            if (GravityWorld.Instance != null && GravityWorld.Instance.IsZeroG)
            {
                zeroGCharge = 0f;
                return;
            }

            if (!data.ZeroGHeld || !CanZeroG)
            {
                zeroGCharge = 0f;
                return;
            }

            // 用真实时间蓄力：锁定慢动作(0.35x)下不会变成"要按 1.4 秒"
            zeroGCharge += Time.unscaledDeltaTime;
            if (zeroGCharge < config.zeroGChargeTime) return;

            zeroGCharge = 0f;
            zeroGCooldownTimer = config.zeroGCooldown;
            GravityWorld.Instance.EnterZeroG(config.zeroGDuration);
            Game.Audio.SoundPlayer.Play("zerog", 1f, 0f);
        }

        private void FocusCamera(Transform targetTransform)
        {
            if (cameraFollow == null) return;
            cameraFollow.FocusOn(targetTransform, config.cameraFocusDuration);
        }

        private void OnGUI()
        {
            if (config == null) return;

            // ---- 锁定指示：目标头顶画 ▼ ----
            if (HasManualLock && Camera.main != null)
            {
                Vector3 p = Camera.main.WorldToScreenPoint(manualLock.transform.position + Vector3.up * 0.8f);
                if (p.z > 0f)
                {
                    var style = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 22,
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(1f, 0.85f, 0.2f) }
                    };
                    GUI.Label(new Rect(p.x - 20f, Screen.height - p.y - 20f, 40f, 30f), "▼", style);
                }
            }

            // ---- 左上角调试 HUD（正式版关掉）----
            if (!showDebugHud) return;

            string pip = "";
            for (int i = 0; i < config.gravityCharges; i++)
                pip += i < charges ? "●" : "○";

            string text = $"重力 {pip}";
            if (HasManualLock) text += $"  [锁定 {manualLock.name}]";
            else text += "  [左键锁定]";

            if (GravityWorld.Instance != null && GravityWorld.Instance.IsZeroG)
                text += $"  [失重 {GravityWorld.Instance.RemainingTime:0.0}s] WS={motor.TargetVertical:0} vy={motor.VerticalVelocity:0.0}";
            else if (GravityWorld.Instance != null && GravityWorld.Instance.IsFlipped)
                text += $"  [翻转 {GravityWorld.Instance.RemainingTime:0.0}s]";

            if (manualLock != null && manualLock.ChainCount > 1)
                text += $"  x{manualLock.ChainCount}";

            GUILayout.BeginArea(new Rect(12f, 46f, 560f, 50f));
            GUILayout.Label(text);
            GUILayout.EndArea();
        }
    }
}
