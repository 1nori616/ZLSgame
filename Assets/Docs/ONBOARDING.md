# 团队上手手册

> 目标：让美术和策划在 30 分钟内能独立完成「拉取 → 修改 → 提交」。
> 不需要理解 Git 原理，只需要按流程做。

## 0. 你需要装什么

| 角色 | Unity 2022.3.62f3c1 | GitHub Desktop | 说明 |
|---|---|---|---|
| 程序 | ✅ | ✅ | 全流程 |
| 美术 | ✅（要和项目完全同版本） | ✅ | 导入模型/贴图、调材质 |
| 策划 | 建议装 | ✅ | 只改文档的话可以不装 Unity |

序列化模式已设为 Force Text，`.gitignore` 已配好，**不需要你自己配置任何东西**。

## 1. 第一次：把项目拿到本地

1. 打开 **GitHub Desktop** → `File` → `Clone repository`
2. 选 `URL` 标签 → 粘贴：`https://github.com/1nori616/ZLSgame.git`
3. `Local path` 改成英文路径，比如 `D:\UnityProjects\ZLSgame`
4. 点 `Clone`
5. 克隆完，点 `Fetch origin`，再在分支下拉里切到 **`develop`**
6. 打开 **Unity Hub** → `Add` → 选 `D:\UnityProjects\ZLSgame` → 打开

**验收标准**：Unity 打开后，Hierarchy 里有 `player`、`enemy1`、`Ground`、`Main Camera`。如果没有，说明打开的不是 `Assets/Scenes/main`。

## 2. 每天怎么干活

### 开工（2 分钟）

1. GitHub Desktop → `Fetch origin`
2. 确认当前分支是 **`develop`** → 点 `Pull origin`（有更新时才会出现）
3. `Branch` → `New branch`，从 `develop` 建自己的分支：

| 角色 | 分支名前缀 | 例子 |
|---|---|---|
| 程序 | `feature/` `fix/` | `feature/enemy-ai` |
| 美术 | `art/` | `art/player-sprite` |
| 策划 | `docs/` | `docs/level-design` |

4. 点 `Publish branch`

### 干活

- 改完东西 → **Unity 里 `Ctrl + S` 保存**
- 切回 GitHub Desktop → 看 `Changes` 列表有没有奇怪的文件（比如 `Library/`）
- 左下角 `Summary` 填一行说明（见第 5 节）
- 点 `Commit to xxx` → 再点 `Push origin`

### 收工

哪怕没做完，也要 **Commit + Push** 一次，Summary 写 `wip: 还没做完的部分`。

## 3. 美术专用

### 文件放哪

```
Assets/
├── Art/
│   ├── Sprites/     贴图、UI 图
│   ├── Animations/  动画片段
│   └── Materials/   材质
├── Audio/           音效、音乐
└── Prefabs/         预制体（要复用的物体）
```

**不要**直接丢在 `Assets` 根目录，也不要丢在 `Scripts` 里。

### 大文件

`psd` `fbx` `wav` `mp3` `tga` `png` 已经走 Git LFS，正常提交即可。

如果你要用 **其他大格式**（`tif` `psb` `blend` `mb` 等），**先找程序加规则**，否则仓库会变得很大且无法回退。

### 不要做

- ❌ 改 `Assets/Scripts/` 里的任何代码
- ❌ 同时改 `main.unity` 场景（场景冲突极难处理）
- ❌ 提交 `.psd` 的备份文件、临时导出文件

## 4. 策划专用

### 改数值

打开 Unity → `Project` 面板 → `Assets/PlayerConfig` → 在 Inspector 里改数字。

改完 `Ctrl + S`，然后提交。**这个文件是团队共用的**，一次只改你负责的那几项。

### 改文档

`Assets/Docs/` 下的 `.md` 文件可以直接改，也可以在 GitHub 网页上改（不用装任何东西）。

### 不要做

- ❌ 改代码
- ❌ 改场景结构（加物体、删物体）
- ❌ 用 Excel 编辑表格后直接提交 `.xlsx`——二进制文件无法合并，两个人同时改必然互相覆盖

## 5. 提交信息怎么写

一行，**英文前缀 + 中文说明**：

```
art: 导入主角走路序列帧
docs: 补充第二关关卡设计
feat: 玩家重力操控
fix: 敌人卡在台阶下
tune: 跳跃高度 13 → 15
```

前缀：`feat` 新功能 / `fix` 修 bug / `art` 美术 / `docs` 文档 / `tune` 调数值 / `chore` 杂项。

## 6. 绝对不能做的六件事

1. ❌ **直接改 `main` 分支**——那是发布版本
2. ❌ **开着 Unity 切分支**——会让 `.meta` 和资源引用错乱
3. ❌ **两个人同时改同一个场景**——冲突无法自动合并
4. ❌ **提交 `Library/`、`Temp/`**——几万个缓存文件
5. ❌ **把打包出来的游戏文件夹放进仓库**——几 GB
6. ❌ **遇到冲突乱点**——停下来，截图，找人

## 7. 遇到问题

**先别自己试。** 发三样东西给程序：

1. GitHub Desktop 的截图（能看到 Changes 列表）
2. 报错原文（Console 或弹窗）
3. 你刚才做了什么操作

大部分问题（冲突、分支搞错、文件丢失）都能救回来，但**错误的"尝试修复"会把可救的变成不可救的**。

## 8. 第一次练手任务

**目标：完整走一遍流程，不追求产出。**

### 美术

1. 找一张自己的图，拖进 `Assets/Art/Sprites/`
2. 保存 → Commit（`art: 测试导入一张图`）→ Push
3. 目标：确认图片能进仓库、别人能拉到

### 策划

1. 打开 `Assets/PlayerConfig`，把 `Move Speed` 从 7 改成 8
2. `Ctrl + S` → Commit（`tune: 测试修改移动速度`）→ Push
3. 目标：确认改数值能提交、能合并

做完这两个任务，流程就通了，之后按第 2 节每天走一遍。
