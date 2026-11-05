# 关卡搭建手册（给策划）

> 目标：你可以自己在 Unity 里搭关卡，程序只负责合并和检查。
> 你**不需要**写代码，也**不需要**会 Git 推送。
> 工作方式：拉一份项目到本地 → 在 Unity 里画 → **打包把文件发给程序** → 程序合并进主线。

---

## 0. 先看这个：现在还不能开工

目前项目里**还没有**这些东西，程序做完你才能开始画图：

- [ ] 预制体 `P_Enemy`（敌人）
- [ ] 预制体 `P_Box`（箱子）
- [ ] 预制体 `P_Spawn`（出生点）
- [ ] 预制体 `P_Goal`（终点）
- [ ] 模板场景 `Level_Template.unity`
- [ ] 地砖的 `Tile` 资源

**程序做完会通知你。** 在这之前你可以先做第 1、2 步（装环境、拉项目）。

---

## 1. 环境要求

| 项 | 要求 | 说明 |
|---|---|---|
| Unity | **2022.3.62f3c1** | 版本必须和程序完全一致，否则打不开 |
| Git | 任意版本 | 装 GitHub Desktop 会自带 |
| Git LFS | 必须装 | 否则图片全是坏的 |
| 磁盘 | 5 GB 空闲 | |

### 装 Git LFS

装完 Git 后打开命令行（Win + R → `cmd`），输入：

```
git lfs install
```

看到 `Git LFS initialized` 就行。

---

## 2. 拿到项目

1. 打开 GitHub Desktop → `File` → `Clone repository`
2. 选 `ZLSgame` → 本地路径选 `D:\UnityProjects\`
3. 点 `Clone`
4. 打开 Unity Hub → `Add` → 选 `D:\UnityProjects\ZLSgame`
5. 用 **2022.3.62f3c1** 打开

第一次打开会导入几分钟，正常。

---

## 3. 你的工作区

### 可以改

```
Assets/Scenes/Levels/Level_01.unity   ← 你的关卡（自己新建）
Assets/Art/Tiles/                     ← 地砖图
```

### 不要碰

```
Assets/Scripts/           代码
Assets/Prefabs/           预制体（要改先找程序）
Assets/Scenes/main.unity  程序的主测试场景
Assets/PlayerConfig.asset 玩家数值
ProjectSettings/          项目设置
```

**动了这些会导致别人打不开项目。**

---

## 4. 搭一个关卡（5 步）

### 第 1 步：从模板复制场景

1. `Project` 面板 → `Assets/Scenes/` → 找到 `Level_Template`
2. 右键 → `Duplicate`（或 `Ctrl + D`）
3. 改名 `Level_01`
4. 拖到 `Assets/Scenes/Levels/` 文件夹里（没有就右键 `Create` → `Folder`）

**不要直接在模板上改**，模板要留着给下一关用。

### 第 2 步：建地砖调色板

1. 菜单 `Window` → `2D` → `Tile Palette`
2. 面板左上角 `Create New Palette` → 命名 `Palette_Main`
3. 保存位置选 `Assets/Art/Tiles/`
4. 打开 `Assets/Art/Tiles/`，把里面的 PNG **拖进 Tile Palette 面板**
5. 弹窗会自动生成 Tile 资源，点确定

### 第 3 步：画地板

1. `Hierarchy` 里选中 **`Tilemap_Floor`**
2. 在 Tile Palette 里选一个地砖
3. 用画笔（快捷键 `B`）在场景里画
4. 按住 `Shift` 拖拽 = 画直线；用矩形工具（快捷键 `U`）可以刷一片

> 地砖会自动吸附到网格，不用担心对齐。

### 第 4 步：放敌人和道具

从 `Assets/Prefabs/` 拖到场景里：

| 预制体 | 放什么 |
|---|---|
| `P_Enemy` | 敌人，可以放多个 |
| `P_Box` | 可被重力甩的箱子 |
| `P_Spawn` | 玩家出生的位置，**只能放一个** |
| `P_Goal` | 终点，碰到就过关，**只能放一个** |

**放完之后不要 Add Component**，有需要改参数找程序。

### 第 5 步：保存并测试

1. `Ctrl + S` 保存
2. 点顶部 ▶ 进 Play
3. 从出生点走到终点，看能不能通关
4. 有报错看 `Console` 面板（红色文字）

---

## 5. 关卡设计原则

### 每关 60~90 秒

从出生点到终点，正常走一遍 1 分钟左右。太长玩家会烦。

### 至少 2 个"必须用重力"的地方

- 高台：必须把箱子甩上去垫脚，或者把自己飘上去
- 挡路的敌人：必须甩飞才能过

**如果一关不用技能也能过，这关就没意义。**

### 先给安全区

出生点附近 3~5 格不要放敌人，让玩家先熟悉操作。

### 高度差不要超过 3 格

玩家跳跃 + 冲刺能过的极限大约 3 格。超过就必须用重力技能。

---

## 6. 程序要先做的（给程序看）

按顺序做，做完通知策划：

1. **做预制体**
   - `enemy1` 拖到 `Assets/Prefabs/` → 改名 `P_Enemy`
   - 箱子（带 `GravityAffected`）→ `P_Box`
   - 空物体（只有 Transform）→ `P_Spawn`
   - 空物体 + 碰撞体（Is Trigger）+ `LevelExit` → `P_Goal`

2. **做模板场景** `Level_Template.unity`
   ```
   Main Camera
   GravityWorld
   player
   Grid
   ├── Tilemap_Floor   （Tilemap + Tilemap Collider 2D + Composite Collider 2D）
   ├── Tilemap_Hazard  （Trigger）
   └── Tilemap_Deco    （无碰撞）
   LevelExit
   UI
   ```

3. **Tilemap 碰撞设置**
   - `Tilemap_Floor` → `Tilemap Collider 2D` → 勾 `Used By Composite`
   - 同级加 `Composite Collider 2D` → `Rigidbody 2D` 会自动出现，设成 `Static`

4. **写 `LevelExit` 组件**
   - 玩家碰到 → 触发胜利（现在还没胜负判定，可以先只打日志）

---

## 7. 交付与验收

### 你要交的文件

每次做完一关，交这三样：

```
1. Assets/Scenes/Levels/Level_01.unity
   （同目录下的 Level_01.unity.meta 也要一起交）

2. 如果有新地砖：Assets/Art/Tiles/ 里的 PNG + 对应的 .meta 和 Tile 资源

3. 一句话说明：这关怎么玩、大概多久
```

### 怎么打包（3 步）

1. 在文件资源管理器里打开 `D:\UnityProjects\ZLSgame\Assets\Scenes\Levels\`
2. 选中 `Level_01.unity` **和** `Level_01.unity.meta`（两个都要）
3. 右键 → `发送到` → `压缩(zipped)文件夹` → 发到群里 / 私聊程序

> **`.meta` 文件千万别漏**。少了它，程序导入后你的场景会变成空白。

### 交付节奏

| 时间 | 交什么 |
|---|---|
| 每做完一个房间 | 先发一版给程序试（早发现问题） |
| 整关完成 | 完整交一次 |
| 修改后 | 重新打包，文件名带日期，如 `Level_01_1106.zip` |

**不要攒到最后一刻一起交。**

### 验收标准（程序检查）

- [ ] 进 Play 能走到终点
- [ ] Console 没有红色报错
- [ ] 场景里没有手动添加的组件（只有预制体 + Tilemap）
- [ ] 出生点、终点各只有一个

---

## 8. 关于 Git（现在不用管）

现在你**不需要**推送代码。流程是：

```
你本地画 → 打包发文件 → 程序合并进主线
```

这样做的原因：场景文件（.unity）一旦两边同时改，冲突几乎无法手工解决，3 天内不值得冒这个险。

**以后**如果你熟练了、并且程序给你开了 `level/*` 分支权限，再切换到"自己推分支"的模式。
## 9. 常见问题

| 问题 | 原因 | 解决 |
|---|---|---|
| 打开项目报错"版本不匹配" | Unity 版本不对 | 装 2022.3.62f3c1 |
| 图片显示成彩色方块/粉色 | 没装 Git LFS | `git lfs install` 后重新 Clone |
| 地砖画上去没有碰撞 | Tilemap 没加 Collider | 找程序，或检查 `Tilemap Collider 2D` |
| 场景打不开 / 一片空白 | 拉到了冲突的版本 | 重新 `Pull`，不行就找程序 |
| 点了 Play 玩家掉下去 | 出生点下面是空的 | 在出生点下方画地板 |
| 改完场景后程序说"打不开" | 动了不该动的文件 | 只改 `Levels/` 里的场景 |
| 交的场景导入后是空白 | 漏了 `.meta` 文件 | 场景文件必须和 `.meta` 一起打包 |

---

## 10. 一句话总结

**你负责"画关卡 + 摆东西"，程序负责"给工具 + 合并"。**
只改 `Levels/` 里的场景，其他都别碰，就不会出问题。