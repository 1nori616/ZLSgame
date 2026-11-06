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

            string levelName = Path.GetFileNameWithoutExtension(path);
            Build(lines, levelName);
        }

        private static void Build(string[] lines, string levelName)
        {
            int height = lines.Length;
            int width = 0;
            for (int i = 0; i < height; i++) width = Mathf.Max(width, lines[i].Length);

            // ---- 新场景 ----
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- 相机 ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(4.5f, height * 0.62f);
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

            TileBase floorTile = GetOrCreateTile("Tile_Ground_00");
            TileBase hazardTile = GetOrCreateTile("Tile_Ground_01");

            if (floorTile == null)
            {
                EditorUtility.DisplayDialog("生成失败",
                    $"找不到地砖贴图 Tile_Ground_00.png。\n已搜索：Assets/Art/Tiles 和 Assets/Art/Sprites", "好");
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
                            floorMap.SetTile(cell, floorTile);
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
            }
            else
            {
                Debug.LogWarning("[LevelBuilder] 找不到 Assets/Prefabs/P_Player.prefab，场景里没有玩家。");
            }

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

        private static void Place(GameObject prefab, Vector3 pos, string label)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[LevelBuilder] 缺少预制体：{label}");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = pos;
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
