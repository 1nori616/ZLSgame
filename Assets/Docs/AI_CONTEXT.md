# AI_CONTEXT — 项目约定（每次让 AI 写代码前先贴这段）

## 技术栈（不要改）
- Unity **2022.3.62f3c1**，**2D** 项目，内置渲染管线（Built-in RP）
- 输入：**旧版 Input Manager**（`Input.GetAxisRaw("Horizontal")`、`Input.GetButtonDown("Jump")`）
  - 不要引入新 Input System，不要改 Project Settings 的 Input Manager
- 物理：**Rigidbody2D + Collider2D**，不用 CharacterController
- 只用 `UnityEngine` 自带 API，不引入第三方包

## 代码规范
- 命名空间：`Game.Player` / `Game.CameraSystem` / `Game.Combat`（新增系统按此扩展）
- 一个文件一个类，**类名 = 文件名**
- 脚本放 `Assets/Scripts/<系统>/`
- 引用一律走 `[SerializeField] private`，在 Inspector 里拖
  - **禁止** `GameObject.Find`、`FindObjectOfType`、`Camera.main` 反复调用
- 数值不要写死在逻辑里，放 `PlayerConfig`（ScriptableObject）
- 单一职责：Input 只读输入，Motor 只改物理，Controller 只做决策

## 现有类与接口
```csharp
// PlayerInput
PlayerInputData Read()
// PlayerInputData { float MoveX; bool JumpDown; bool JumpHeld; bool DashDown; }

// PlayerMotor2D
bool  IsGrounded { get; }
bool  IsDashing  { get; }
bool  CanDash    { get; }
float VerticalVelocity { get; }
float HorizontalSpeed  { get; }
float Facing { get; }
void  SetHorizontal(float input)   // -1..1
void  Jump(float force)
void  CutJump()
void  Dash(float direction)

// PlayerConfig（ScriptableObject）
moveSpeed, acceleration, deceleration,
jumpForce, gravityScale, fallGravityMultiplier,
coyoteTime, jumpBufferTime, jumpCutMultiplier,
dashSpeed, dashDuration, dashCooldown,
groundLayer, groundCheckWidthScale, groundCheckDistance
```

## 目标：横板动作游戏（死亡细胞方向）
已具备：水平移动、可变高度跳跃、土狼时间、跳跃缓冲、冲刺（无无敌帧）

待做（按优先级）：
1. 冲刺无敌帧 + 闪避
2. 近战攻击（三段连击 + 前摇/后摇 + 命中判定）
3. 受击硬直、击退、无敌时间
4. 敌人 AI（巡逻 / 追击 / 攻击状态机）
5. 生命值、死亡与重生点
6. 房间切换、掉落物

## 对 AI 的要求
- 一次只改**一个文件**
- 输出**完整文件**，不要用 `// ...` 省略
- 不修改场景文件、不改其他脚本、不新增包
- 改动涉及手感时，说明每个参数的效果和推荐范围
- 不写 `Debug.Log` 刷屏（调试用完删掉）
