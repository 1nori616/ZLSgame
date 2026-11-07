#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.EditorTools
{
    /// <summary>
    /// 从文本网格生成关卡场景。
    /// 用法：Tools → 关卡 → 从文本生成关卡，选择 Assets/Levels/Level_01.txt。
    /// 一个字符 = 一格 = 1 个世界单位。
    /// </summary>
    public static class LevelBuilder
    {
        private const string TileAssetFolder = "Assets/Art/Tiles";   // Tile 资源输出位置
        private static readonly string[] SpriteFolders =
        {
            "Assets/Art/Tiles",
            "Assets/Art/Sprites",   // 目前地砖实际在这里
        };
        private const string PrefabFolder = "Assets/Prefabs";
        private const string SceneFolder = "Assets/Scenes/Levels";
        private const string LevelFolder = "Assets/Levels";

        // 图例
        private const char Floor = '#';
        private const char Empty = '.';
        private const char Enemy = 'E';
        private const char Box = 'B';
        private const char Spawn = 'P';
        private const char Goal = 'G';
        private const char Hazard = '^';

        private static int currentHeight = 20;

        [MenuItem("Tools/关卡/从文本生成关卡")]
        public static void BuildFromText()
        {
            if (!Directory.Exists(LevelFolder)) Directory.CreateDirectory(LevelFolder);

            string path = EditorUtility.OpenFilePanel("选择关卡文本", LevelFolder, "txt");
            if (string.IsNullOrEmpty(path)) return;

            string[] lines = File.ReadAllLines(path);
            if (lines.Length == 0)
            {
                EditorUtility.DisplayDialog("生成失败", "文件是空的", "好");
                return;
            }

                        if (path.EndsWith(".hints.txt"))
            {
                EditorUtility.DisplayDialog("选错文件了",
                    "这是提示文件，不是地图文件。\n请选择同目录下的 Level_XX.txt。", "好");
                return;
            }
string levelName = Path.GetFileNameWithoutExtension(path);
            Build(lines, levelName, path);
        }

        private static void Build(string[] lines, string levelName, string levelPath)
        {
            int height = lines.Length;
            currentHeight = height;
            int width = 0;
            for (int i = 0; i < height; i++) width = Mathf.Max(width, lines[i].Length);

            // ---- 新场景 ----
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- 相机 ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            // 固定视野：不随关卡高度缩放，否则地图一高就"缩得很小"
            cam.orthographicSize = 6.5f;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            camGo.transform.position = new Vector3(width * 0.5f, height * 0.5f, -10f);
            camGo.AddComponent<Game.CameraSystem.CameraFollow>();
            camGo.AddComponent<Game.Effects.Vignette>();   // 锁定/失重的暗角效果
            camGo.AddComponent<AudioListener>();           // 没有它整个场景都听不到声音

            // ---- 重力世界 ----
            var gwGo = new GameObject("GravityWorld");
            gwGo.AddComponent<Game.Gravity.GravityWorld>();

            // ---- 网格与图层 ----
            var gridGo = new GameObject("Grid");
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;

            Tilemap floorMap = CreateTilemap(gridGo.transform, "Tilemap_Floor", true);
            Tilemap hazardMap = CreateTilemap(gridGo.transform, "Tilemap_Hazard", true);
            Tilemap decoMap = CreateTilemap(gridGo.transform, "Tilemap_Deco", false);

            // 载入 8 块地板贴图，按列号轮换，和美术给的横排图一致
            TileBase[] floorTiles = new TileBase[8];
            for (int i = 0; i < floorTiles.Length; i++)
                floorTiles[i] = GetOrCreateTile($"Tile_Floor_{i:00}");

            // 填充块：去掉顶边和发光边，可以竖直无缝堆叠
            TileBase[] fillTiles = new TileBase[8];
            for (int i = 0; i < fillTiles.Length; i++)
                fillTiles[i] = GetOrCreateTile($"Tile_FloorFill_{i:00}");

            // 浮空平台用的贴图（一层薄平台）
            TileBase[] platTiles = new TileBase[8];
            for (int i = 0; i < platTiles.Length; i++)
                platTiles[i] = GetOrCreateTile($"Tile_Plat_{i:00}");

            TileBase floorTile = floorTiles[0];
            TileBase hazardTile = GetOrCreateTile("Tile_Floor_03");

            if (floorTile == null)
            {
                EditorUtility.DisplayDialog("生成失败",
                    $"找不到地砖贴图 Tile_Floor_00.png。\n已搜索：Assets/Art/Tiles 和 Assets/Art/Sprites", "好");
                return;
            }

            // ---- 预制体 ----
            GameObject playerPrefab = LoadPrefab("P_Player");
            GameObject enemyPrefab = LoadPrefab("P_Enemy");
            GameObject boxPrefab = LoadPrefab("P_Box");
            GameObject goalPrefab = LoadPrefab("P_Goal");

            Vector2 spawnPos = new Vector2(width * 0.5f, height * 0.5f);
            bool hasGoal = false;

            for (int row = 0; row < height; row++)
            {
                string line = lines[row];
                for (int col = 0; col < line.Length; col++)
                {
                    char c = line[col];
                    // 文本第一行是关顶，所以 y 要反过来
                    int x = col;
                    int y = height - 1 - row;
                    Vector3 cellCenter = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    Vector3Int cell = new Vector3Int(x, y, 0);

                    switch (c)
                    {
                        case Floor:
                            // 这一列从当前格一直连到地图底部 → 实心地面/高台
                            bool grounded = true;
                            for (int k = row + 1; k < lines.Length; k++)
                            {
                                if (col >= lines[k].Length || lines[k][col] != Floor)
                                {
                                    grounded = false;
                                    break;
                                }
                            }

                            bool floorAbove = row > 0
                                              && col < lines[row - 1].Length
                                              && lines[row - 1][col] == Floor;

                            int tileIdx = x % 8;

                            if (!grounded)
                            {
                                if (!floorAbove)
                                    floorMap.SetTile(cell, platTiles[tileIdx] ?? floorTile);
                            }
                            else if (floorAbove)
                            {
                                floorMap.SetTile(cell, fillTiles[tileIdx] ?? floorTile);
                            }
                            else
                            {
                                floorMap.SetTile(cell, floorTiles[tileIdx] ?? floorTile);
                            }
                            break;

                        case Hazard:
                            hazardMap.SetTile(cell, hazardTile != null ? hazardTile : floorTile);
                            break;

                        case Empty:
                            break;

                        case Enemy:
                            Place(enemyPrefab, cellCenter, "enemy");
                            break;

                        case Box:
                            Place(boxPrefab, cellCenter, "box");
                            break;

                        case Spawn:
                            spawnPos = new Vector2(cellCenter.x, cellCenter.y - 0.3f);
                            break;

                        case Goal:
                            Place(goalPrefab, cellCenter, "goal");
                            hasGoal = true;
                            break;
                    }
                }
            }

            // ---- 玩家 ----
            if (playerPrefab != null)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
                p.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);

                // 自动补 HUD（预制体上可能没有）
                if (p.GetComponent<Game.UI.PlayerHud>() == null)
                    p.AddComponent<Game.UI.PlayerHud>();

                // 按关卡高度设置出界边界：上方多留 8 格，下方多留 6 格
                var bounds = p.GetComponent<Game.Combat.OutOfBounds>();
                if (bounds == null) bounds = p.AddComponent<Game.Combat.OutOfBounds>();
                bounds.Configure(-4f, height + 20f);
            }
            else
            {
                Debug.LogWarning("[LevelBuilder] 找不到 Assets/Prefabs/P_Player.prefab，场景里没有玩家。");
            }

            // ---- 浮动提示文字 ----
            CreateHints(levelPath, height);

            // ---- 背景 + 舱门 ----
            CreateBackground(cam, width, height, spawnPos);
            CreateDoors(spawnPos, width, height, lines);

            // ---- 连击计数 ----
            var comboGo = new GameObject("ComboCounter");
            comboGo.AddComponent<Game.Effects.ComboCounter>();

            // ---- 开场演出 ----
            var introGo = new GameObject("Intro");
            introGo.AddComponent<Game.Level.IntroSequence>().Setup(spawnPos.x + 7f, 2.8f);

            // ---- 相机对准玩家 ----
            camGo.transform.position = new Vector3(spawnPos.x, spawnPos.y + 1f, -10f);

            // ---- 保存场景 ----
            if (!Directory.Exists(SceneFolder)) Directory.CreateDirectory(SceneFolder);
            string scenePath = $"{SceneFolder}/{levelName}.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            AddSceneToBuild(scenePath);

            Debug.Log($"[LevelBuilder] 已生成 {scenePath}（{width}×{height} 格，{gridGo.name}）。终点：{(hasGoal ? "有" : "没有")}");
            EditorUtility.DisplayDialog("生成完成",
                $"场景已生成：\n{scenePath}\n\n尺寸：{width} × {height}\n终点：{(hasGoal ? "有" : "没有")}", "好");
        }

        /// <summary>星空背景 + 视差。</summary>
        private static void CreateBackground(Camera cam, int width, int height, Vector2 spawnPos)
        {
            Sprite bg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/BG_Space.png");
            if (bg == null)
            {
                Debug.LogWarning("[LevelBuilder] 找不到背景图 Assets/Art/Sprites/BG_Space.png");
                return;
            }

            var root = new GameObject("Background");
            // 放到出生点附近（相机初始位置），否则背景会停在关卡中心，玩家在起点看不到
            root.transform.position = new Vector3(spawnPos.x, spawnPos.y + 2f, 20f);

            float spriteW = bg.bounds.size.x;   // 世界单位
            float spriteH = bg.bounds.size.y;

            // 只按"相机高度"缩放，保证星星大小正常（之前按关卡宽度算，放大了 12 倍）
            float camHeight = 6.5f * 2f;
            float scale = (camHeight * 1.25f) / spriteH;
            float scaledW = spriteW * scale;

            // 横向平铺，铺满整关（视差移动时不会露出边缘）
            int count = Mathf.CeilToInt((width + 40f) / scaledW) + 1;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"BG_{i:00}");
                go.transform.SetParent(root.transform, false);
                go.transform.localScale = Vector3.one * scale;
                go.transform.localPosition = new Vector3((i - count / 2) * scaledW, 0f, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = bg;
                sr.sortingOrder = -100;
            }

            root.AddComponent<Game.Effects.Parallax>().Setup(0.35f, 1f);
        }

        /// <summary>起点舱门（开→关）和终点舱门（关→开）。</summary>
        private static void CreateDoors(Vector2 spawnPos, int width, int height, string[] lines)
        {
            Sprite startOpen = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Door_Start_Open.png");
            Sprite startClose = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Door_Start_Close.png");
            Sprite endClose = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Door_End_Close.png");
            Sprite endOpen = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Door_End_Open.png");

            // 起点门：放在玩家出生点左边一点
            if (startOpen != null)
            {
                var go = new GameObject("Door_Start");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = -2;   // 放在玩家后面，主角从舱里走出来才看得见
                const float doorScale = 2.1f;
                go.transform.localScale = Vector3.one * doorScale;
                float h = startOpen.bounds.size.y * doorScale;
                go.transform.position = new Vector3(spawnPos.x, spawnPos.y - 0.2f + h * 0.5f, 0f);
                go.AddComponent<Game.Level.DoorController>().Setup(
                    Game.Level.DoorController.DoorMode.StartDoor, startOpen, startClose, 3.5f);
            }

            // 终点门：跟终点位置对齐
            Vector2 goalPos = spawnPos;
            for (int r = 0; r < lines.Length; r++)
            {
                int y = lines.Length - 1 - r;
                for (int x = 0; x < lines[r].Length; x++)
                    if (lines[r][x] == Goal) goalPos = new Vector2(x + 0.5f, y + 0.5f);
            }

            if (endClose != null)
            {
                var go = new GameObject("Door_End");
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = -2;   // 放在玩家后面，主角从舱里走出来才看得见
                const float endScale = 2.1f;
                go.transform.localScale = Vector3.one * endScale;
                float h2 = endClose.bounds.size.y * endScale;
                go.transform.position = new Vector3(goalPos.x, goalPos.y - 0.5f + h2 * 0.5f, 0f);
                go.AddComponent<Game.Level.DoorController>().Setup(
                    Game.Level.DoorController.DoorMode.EndDoor, endClose, endOpen, 2.5f);
            }
        }

        /// <summary>读取同名 .hints.txt（每行：x,y,文字），生成浮动提示。</summary>
        private static void CreateHints(string levelPath, int height)
        {
            string hintPath = System.IO.Path.ChangeExtension(levelPath, null) + ".hints.txt";
            if (!File.Exists(hintPath))
            {
                Debug.Log($"[LevelBuilder] 没有提示文件：{hintPath}（跳过）");
                return;
            }

            var root = new GameObject("Hints");
            string[] lines = File.ReadAllLines(hintPath);
            int count = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                // x,y,文字
                int first = line.IndexOf(",");
                int second = first >= 0 ? line.IndexOf(",", first + 1) : -1;
                if (second < 0) continue;

                if (!float.TryParse(line.Substring(0, first), out float x)) continue;
                if (!float.TryParse(line.Substring(first + 1, second - first - 1), out float y)) continue;

                string text = line.Substring(second + 1).Trim().Replace("\\n", "\n");
                if (text.Length == 0) continue;

                var go = new GameObject($"Hint_{count:00}");
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0f);
                go.AddComponent<Game.UI.LevelHint>().Setup(text, 9f);
                count++;
            }

            Debug.Log($"[LevelBuilder] 生成 {count} 条浮动提示");
        }

        private static void Place(GameObject prefab, Vector3 pos, string label)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[LevelBuilder] 缺少预制体：{label}");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = pos;

            // 敌人/箱子被重力甩飞时不要立刻判定出界传送回去
            var bounds = go.GetComponent<Game.Combat.OutOfBounds>();
            if (bounds != null) bounds.Configure(-4f, currentHeight + 20f);
        }

        private static Tilemap CreateTilemap(Transform parent, string name, bool solid)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();

            if (solid)
            {
                // 合并碰撞体：避免角色卡在两块地砖的接缝上
                var col = go.AddComponent<TilemapCollider2D>();
                col.usedByComposite = true;

                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Static;

                go.AddComponent<CompositeCollider2D>();
                go.AddComponent<Game.Level.TilemapColliderBaker>();   // 运行时补烘焙
            }

            return tilemap;
        }

        private static TileBase GetOrCreateTile(string spriteName)
        {
            if (!Directory.Exists(TileAssetFolder))
            {
                Directory.CreateDirectory(TileAssetFolder);
                AssetDatabase.Refresh();
            }

            string tilePath = $"{TileAssetFolder}/{spriteName}_Tile.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TileBase>(tilePath);
            if (existing != null) return existing;

            Sprite sprite = null;
            string usedPath = null;
            for (int i = 0; i < SpriteFolders.Length && sprite == null; i++)
            {
                string candidate = $"{SpriteFolders[i]}/{spriteName}.png";
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(candidate);
                if (sprite != null) usedPath = candidate;
            }

            if (sprite == null)
            {
                Debug.LogWarning($"[LevelBuilder] 找不到地砖 {spriteName}，已搜索：" +
                                 string.Join("、", SpriteFolders));
                return null;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = Tile.ColliderType.Grid;

            AssetDatabase.CreateAsset(tile, tilePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LevelBuilder] 用 {usedPath} 生成了 {tilePath}");
            return tile;
        }

        private static GameObject LoadPrefab(string name)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
        }

        private static void AddSceneToBuild(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
                if (scenes[i].path == scenePath) return;

            // 新关卡插到第一位（方便直接 Play）
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
