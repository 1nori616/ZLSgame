# CONTRACTS — 公开接口契约

> 这份文件回答一个问题：**"谁能调用谁，改了什么会连带改什么"**。
> 和 `AI_CONTEXT.md` 的区别：AI_CONTEXT 是"写给 AI 的规矩"，这份是"当前真实的接口地图"。

## 怎么用（四步）

1. **动手前查表**：要在某个类上加公开成员，先看它在这一页的哪一节。
2. **先改文档，再改代码**：新增公开成员 → 先在这里加一行 → 再写实现。
3. **改签名 = 改三方**：改方法签名 → 同步所有调用方 → 更新这里 → 单独提交 `refactor:`。
4. **合并前扫技术债**：看最后一节，有新欠的就记上。

---

## 一、玩家移动

### `PlayerInput`
```csharp
PlayerInputData Read()          // 调用方：PlayerController、GravityShift
struct PlayerInputData { float MoveX; bool JumpDown, JumpHeld, DashDown, AntiDown, HeavyDown, SelfDown; }
```

### `PlayerMotor2D`（物理层，唯一改 velocity 的地方）
```csharp
// 只读状态
bool  IsGrounded / IsDashing / IsKnockedBack / IsGravityOverridden / CanDash
float VerticalVelocity / HorizontalSpeed / Facing
PlayerConfig Config
// 指令
void  SetHorizontal(float input)     // PlayerController
void  Jump(float force)              // PlayerController
void  CutJump()                      // PlayerController
void  Dash(float direction)          // PlayerController
void  ApplyKnockback(Vector2 v, float duration)                 // PlayerHealth
void  ApplyGravityOverride(float acceleration, float maxSpeed, float duration)  // GravityShift
```
外部只读 `Facing`：`CameraFollow`、`GravityShift`

### `PlayerController`（决策层）
**无公开成员**。只接收 Unity 消息，依赖 Input / Motor / Config / Animator。
> 任何"玩家该不该动"的逻辑放这里；任何"改速度"的逻辑不允许写在这里。

### `PlayerConfig`（ScriptableObject，数值契约）
| 分组 | 字段 |
|---|---|
| 移动 | moveSpeed, acceleration, deceleration |
| 跳跃 | jumpForce, gravityScale, fallGravityMultiplier, coyoteTime, jumpBufferTime, jumpCutMultiplier |
| 冲刺 | dashSpeed, dashDuration, dashCooldown |
| 重力操控 | gravityDuration, gravityCharges, gravityRechargeTime, cameraFocusDuration |
| 自己施放 | selfAntiAcceleration, selfAntiMaxSpeed |
| 施放判定 | castRadius, frontPreference, castLayer |
| 受击 | hurtInvincibleTime, knockbackTime |
| 接地 | groundLayer, groundCheckWidthScale, groundCheckDistance |

## 二、战斗

### `DamageInfo`（值类型，不可变数据）
```csharp
struct DamageInfo { int Amount; Vector2 Knockback; GameObject Source; }
```

### `IDamageable`（所有"能被打的东西"）
```csharp
interface IDamageable { bool IsDead; bool IsInvincible; void TakeDamage(in DamageInfo); }
实现：Health、PlayerHealth
```

### `Health`（纯数据组件）
```csharp
int  Max / Current; bool IsDead; bool IsInvincible(恒 false)
event Action<int,int> OnChanged;        // UI 血条订阅
event Action<DamageInfo> OnDamaged;     // EnemyAI、HitFlash、PlayerHealth 订阅
event Action OnDied;                    // EnemyAI 订阅
void TakeDamage(in DamageInfo);  void ResetHealth();
```

### `DamageSource`（标记 + 数值，挂在尖刺/敌人身上）
```csharp
int Amount; Vector2 Knockback; bool DestroyAfterHit;
void Consume();
调用方：PlayerHurtbox
```

### `PlayerHealth`（组合 Health + 无敌帧 + 击退 + 震屏）
```csharp
bool IsDead; bool IsInvincible;  Health Health
void TakeDamage(in DamageInfo)   // 由 PlayerHurtbox 调用
调用 CameraFollow.Instance.Shake()
```

### `PlayerHurtbox`（Trigger 子物体，自己检测伤害源）
```csharp
读取：DamageSource（在碰到的物体或它的父级上）
```

## 三、重力

### `GravityMode`
```csharp
enum GravityMode { AntiGravity, Heavy }
```

### `GravityAffected`（挂在敌人 / 箱子上）
```csharp
bool  IsOverridden / IsGrounded / IsValidTarget
int   ChainCount;  GravityMode LastMode;  float RemainingTime / ChainWindow / CurrentScale / BaseScale
event Action<GravityAffected> OnGroundSlam;   // 订阅方：GroundSlamEffect
void  ApplyOverride(float duration, GravityMode mode)   // 调用方：GravityShift
void  ResetGravity();  void SetPermanent(float scale);  void ResetChain();
```

### `ImpactDamage`（摔伤，挂在被打的目标上）
```csharp
// 无公开成员，靠 Unity 的 OnCollisionEnter2D 触发
依赖：IDamageable（同物体或父级）、GravityAffected（读 ChainCount）、CameraFollow.Instance
```

### `GroundSlamEffect`（"站地被压"反馈）
```csharp
// 无公开成员
依赖：GravityAffected.OnGroundSlam、EnemyAI.ApplyStun、ShockwaveRing.Spawn、CameraFollow.Instance
```

### `GravityShift`（玩家技能，充能制）
```csharp
int  Charges / MaxCharges;  bool IsReady;  float RechargeProgress;  GravityAffected LockedTarget
bool TryCastOnTarget(GravityMode);  bool TryCastOnSelf();
依赖：PlayerInput、PlayerMotor2D、PlayerConfig、CameraFollow
```

## 四、表现层

### `CameraFollow`（相机位置的唯一拥有者）
```csharp
static CameraFollow Instance;     // 全局访问点（见技术债）
bool IsFocusing;  float Trauma;  Transform FocusTarget
void Shake(float amount);         // PlayerHealth / ImpactDamage / EnemyAI / GroundSlamEffect
void FocusOn(Transform, float);   // GravityShift
void ClearFocus();
```

### `ShockwaveRing`
```csharp
static ShockwaveRing Spawn(Vector3 position, float radius, float lifetime, Color color)
调用方：GroundSlamEffect
```

### `HitFlash`
```csharp
// 无公开成员，订阅 Health.OnDamaged
```

## 五、调用关系

```
PlayerInput ──> PlayerController ──> PlayerMotor2D ──> Rigidbody2D
     │                                     ↑
     └────> GravityShift ──────────────────┘（对自己施放）
                 │
                 ├──> GravityAffected.ApplyOverride ──> Rigidbody2D
                 │            │
                 │            └──> OnGroundSlam ──> GroundSlamEffect ──> ShockwaveRing
                 │                                      └──> EnemyAI.ApplyStun
                 └──> CameraFollow.FocusOn

碰触伤害：DamageSource ──> PlayerHurtbox ──> PlayerHealth ──> Health
                                                └──> CameraFollow.Shake

摔伤：ImpactDamage ──> IDamageable.TakeDamage
                  └──> CameraFollow.Shake

受击表现：Health.OnDamaged ──> HitFlash / EnemyAI(硬直) / PlayerHealth(闪烁)
```

## 六、接口变更记录

| # | 变更 | 影响面 | 状态 |
|---|---|---|---|
| 1 | `GravityAffected.ApplyOverride(scale, duration)` → 加 `GravityMode` → 最终 `(duration, mode)` | 仅 GravityShift | ✅ 已同步 |
| 2 | `PlayerMotor2D.ApplyGravityOverride(scale, duration)` → `(acceleration, maxSpeed, duration)` | 仅 GravityShift | ✅ 已同步 |
| 3 | 新增 `CameraFollow.Instance` + `Shake()` | 4 个调用方 | ⚠️ 技术债 |
| 4 | `Health` 增加 `OnGUI` 调试血条 | 无（自包含） | ⚠️ 违反单一职责 |
| 5 | `EnemyAI.ApplyStun()` 新增 | GroundSlamEffect | ✅ |
| 6 | `PlayerInputData` 增加 3 个字段 | 仅 PlayerInput 构造 | ✅ 向后兼容 |

## 七、技术债（合并前扫一眼）

| 债务 | 位置 | 影响 | 处理建议 |
|---|---|---|---|
| 静态单例 | `CameraFollow.Instance` | 4 处硬依赖，难测试、难替换 | 以后改成事件（`CameraEvents.ShakeRequested`）或注入 |
| 表现混进数据 | `Health.OnGUI` | 关掉调试要改组件 | 正式版删除，或拆到 `HealthDebugView` |
| 违反 Find 规则 | `CameraFollow.Awake`、`EnemyAI.Update` 用 `FindGameObjectWithTag` | 每帧查找有开销 | 改成注入或场景初始化时缓存（EnemyAI 已缓存） |
| 敌人无寻路 | `EnemyAI` | 只能水平追 | 后续加简单跳跃/绕障 |
| 接地配置重复 | `PlayerConfig.groundLayer` 与 `GravityAffected.groundLayer` 各一份 | 改层要改两处 | 统一到一个 GroundSettings 资产 |

## 八、三条硬规则

1. **加公开成员必须登记**：没登记 = 这份文档失效 = 框架失控。
2. **改签名必须同步所有调用方**，并单独提交：`refactor: xxx 签名变更`。
3. **一个提交只做一类事**：新功能、调参、重构，不要混在一起。
