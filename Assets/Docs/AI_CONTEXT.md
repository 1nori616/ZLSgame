# AI_CONTEXT — 项目约定（每次让 AI 写代码前先贴这段）

## 技术栈（不要改）
- Unity **2022.3.62f3c1**，**2D** 项目，内置渲染管线（Built-in RP）
- 输入：**旧版 Input Manager**（`Input.GetAxisRaw("Horizontal")`、`Input.GetButtonDown("Jump")`）
  - 不要引入新 Input System，不要改 Project Settings 的 Input Manager
- 物理：**Rigidbody2D + Collider2D**，不用 CharacterController
- 只用 `UnityEngine` 自带 API，不引入第三方包

## 代码规范
- 命名空间：`Game.Player` / `Game.CameraSystem` / `Game.Combat` / `Game.Gravity`
- 一个文件一个类，**类名 = 文件名**，脚本放 `Assets/Scripts/<系统>/`
- 引用走 `[SerializeField] private`，在 Inspector 拖
  - **禁止** `GameObject.Find`、`FindObjectOfType`、反复 `Camera.main`
- 数值放 `PlayerConfig`（ScriptableObject），不写死在逻辑里
- 单一职责：Input 只读输入，Motor 只改物理，Controller 只做决策

## 现有类与接口

### 移动
```csharp
PlayerInputData Read()
// { float MoveX; bool JumpDown, JumpHeld, DashDown, AntiDown, HeavyDown, SelfDown; }

// PlayerMotor2D
bool  IsGrounded / IsDashing / IsKnockedBack / IsGravityOverridden / CanDash
float VerticalVelocity / HorizontalSpeed / Facing
void  SetHorizontal(float)  Jump(float)  CutJump()  Dash(float)
void  ApplyKnockback(Vector2 velocity, float duration)
void  ApplyGravityOverride(float scale, float duration)
```

### 战斗
```csharp
// DamageInfo { int Amount; Vector2 Knockback; GameObject Source; }
interface IDamageable { bool IsDead; bool IsInvincible; void TakeDamage(in DamageInfo); }
Health : MonoBehaviour, IDamageable        // 纯血量 + OnChanged/OnDamaged/OnDied
DamageSource : MonoBehaviour               // 尖刺/敌人攻击判定（Trigger）
PlayerHealth : IDamageable                 // 无敌帧（冲刺/受击后）、击退、闪烁、调试血条
PlayerHurtbox                              // 玩家 Trigger 子物体，自行检测 DamageSource
```

### 重力操控（核心玩法）
```csharp
enum GravityMode { AntiGravity, Heavy }

GravityAffected : MonoBehaviour            // 挂在敌人/箱子上
  bool  IsOverridden; float CurrentScale / BaseScale / RemainingTime
  void  ApplyOverride(float scale, float duration)
  void  ResetGravity(); void SetPermanent(float scale)

ImpactDamage : MonoBehaviour               // 摔伤：撞击速度 > 阈值才扣血
  minImpactSpeed=8, baseDamage=1, damagePerSpeed=6, cooldown=0.2

GravityShift : MonoBehaviour               // 玩家技能：Q 反重力 / E 加重 / R 对自己
  bool  IsReady; bool TryCastOnTarget(GravityMode); bool TryCastOnSelf()
```

### 关键设计约束（已拍板，不要擅自改）
1. **伤害由受击方检测**：玩家靠 `PlayerHurtbox` 自己发现被碰
2. **冲刺自带无敌**：`PlayerHealth.IsInvincible` 包含 `motor.IsDashing`
3. **受击后 0.8s 无敌**：`config.hurtInvincibleTime`
4. **敌人/尖刺不做实体碰撞**：只用 Trigger，玩家可以穿过去
5. **血量存在被打的一方**：`Health` 挂在木桩/敌人/玩家身上
6. **玩家重力必须走 Motor**：`PlayerMotor2D` 每帧覆盖 `rb.gravityScale`，任何改玩家重力的代码都要调用 `motor.ApplyGravityOverride`
7. **重力方向只有上下**：`gravityScale` 正负决定方向，不做"往左飘"（需要的话另写 AddForce 方案）

## 目标：横板动作游戏（重力操控 + 横板动作）
已具备：移动、跳跃、冲刺、受击、无敌帧、
       重力操控（反重力/加重/对自己）、摔伤、调试 HUD

待做：
1. 测试假人（Dummy）验证重力摔伤
2. 敌人 AI（巡逻/追击/攻击）
3. 重力操控的视觉反馈（飘起来的特效、施放指示）
4. 死亡与重生点
5. 血条 UI（订阅 `Health.OnChanged`）

## 对 AI 的要求
- 一次只改**一个文件**
- 输出**完整文件**，不要用 `// ...` 省略
- 不改场景文件、不改其他脚本、不新增包
- 新增公开方法 = 修改接口，必须说明"谁会调用、为什么需要"
- 手感相关参数：说明效果和推荐范围
