using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Animations.Rigging;

namespace Ares.EditorTools
{
    /// <summary>
    /// One click project setup: sprite import, arena generation from an ASCII map, prefab
    /// creation (including the player IK rig and the boss), and scene assembly.
    /// </summary>
    public static class AresSetup
    {
        public const string PrefabDir = "Assets/Prefabs";
        public const string ScenePath = "Assets/Scenes/Arena.unity";
        public const string ConfigDir = "Assets/Config";
        const string GeneratedDir = "Assets/Generated";

        const string P_Ground = "Assets/Arts/changjing/pinzhuang/5_dibiao/dibiao_common/changjing_dibiao_malu 1.png";
        const string P_Wall = "Assets/Arts/changjing/pinzhuang/1_high/common/jianzhu_zhuang_shi.png";
        const string P_WallStrip = "Assets/Arts/changjing/pinzhuang/1_high/common/jianzhu_qiang_xizhuang.png";
        const string P_Crate = "Assets/Arts/changjing/pinzhuang/2_zawu/zawu_common/baifang_lajixiang.png";
        const string P_Rock = "Assets/Arts/changjing/pinzhuang/1_high/common/shitou_zhong.png";
        const string P_Grass = "Assets/Arts/changjing/pinzhuang/1_high/common/zhiwu_zacao_an.png";
        const string P_Debris = "Assets/Arts/changjing/pinzhuang/5_dibiao/dibiao_common/zhawu_muban.png";
        const string P_Neo = "Assets/Arts/zhujue/neo/neo.prefab";
        const string P_Chushouguai = "Assets/Arts/guaiwu/chushouguai/chushouguai.prefab";
        const string P_Outuzhe = "Assets/Arts/guaiwu/outuzhe/outuzhe.prefab";
        const string P_ChushouBody = "Assets/Arts/guaiwu/chushouguai/image/chushouguai_body.png";
        const string P_OutuzheBody = "Assets/Arts/guaiwu/outuzhe/image/outuzhe_body.png";
        const string P_PlayerBullet = "Assets/Arts/zidan/huoqiangzidan/weapon_huoqiangzidan.png";
        const string P_EnemyBullet = "Assets/Arts/guaiwu/outuzhe/zidan_outuzhe/image/zidan_outuzhe.png";
        const string P_Spark = "Assets/Arts/effect/tex_mat/effect_suipian.png";

        static readonly string[] Map = new string[]
        {
            "############################################",
            "#..........................................#",
            "#..ggg..............ggg....................#",
            "#...g....x.......................########..#",
            "#......#########........................#..#",
            "#......#......................x.........#..#",
            "#......#................................#..#",
            "#......#..........#.....................#..#",
            "#......#..........#.......#.............#..#",
            "#....rr#............#.....#......x.........#",
            "#...................#......................#",
            "#...................#......................#",
            "#..#...#.................x.................#",
            "#..#...#......#......P.........##..........#",
            "#..#...#......#.....#......................#",
            "#..#...#......#.....#......................#",
            "#..#...#............#.##...................#",
            "#..#...#...x........#......................#",
            "#..#...#......#............############....#",
            "#..#...#......#............#..........#....#",
            "#..#...#......#............#.......g..#....#",
            "#...................c...............g.#....#",
            "#...............cc.........#x.........#..c.#",
            "#...............c..........#..........#..c.#",
            "#...........rr................r............#",
            "############################################"
        };

        const float OriginX = -22f;
        const float OriginY = 13f;

        // ================================================================== 1. import

        [MenuItem("Tools/Ares/1. Configure Sprite Import", false, 1)]
        public static void ConfigureImport()
        {
            string[] tiled = new string[] { P_Ground, P_Wall, P_WallStrip, P_Crate, P_Rock, P_Grass, P_Debris };
            foreach (string p in tiled) ConfigureSprite(p, true);

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new string[] { "Assets/Resources/UI" }))
                ConfigureSprite(AssetDatabase.GUIDToAssetPath(guid), false);

            EnsureRingSprite();
            EnsureArcSprite();
            AssetDatabase.Refresh();
            Debug.Log("[Ares] Sprite import configured.");
        }

        private static void ConfigureSprite(string path, bool repeat)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            var ts = new TextureImporterSettings();
            ti.ReadTextureSettings(ts);
            ts.spriteMeshType = SpriteMeshType.FullRect;
            ts.spriteGenerateFallbackPhysicsShape = false;
            ti.SetTextureSettings(ts);
            ti.SaveAndReimport();
        }

        /// <summary>Generates the boss ground ring so the boss reads differently at a glance.</summary>
        public static void EnsureRingSprite()
        {
            EnsureFolder(GeneratedDir);
            string path = GeneratedDir + "/boss_ring.png";
            if (!File.Exists(path))
            {
                const int n = 256;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var px = new Color32[n * n];
                var c = new Vector2(n * 0.5f, n * 0.5f);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                        float outer = n * 0.47f;
                        float mid = outer - 11f;
                        float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - mid);
                        a = Mathf.Clamp01(a * 1.4f);
                        px[y * n + x] = new Color32(255, 60, 45, (byte)(a * 255));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                AssetDatabase.Refresh();
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti != null)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.spritePixelsPerUnit = 100f;
                    ti.alphaIsTransparency = true;
                    ti.SaveAndReimport();
                }
            }
        }

        /// <summary>Melee reach wedge (110 degrees, soft edges) baked to a PNG asset.</summary>
        public static void EnsureArcSprite()
        {
            EnsureFolder(GeneratedDir);
            string path = GeneratedDir + "/melee_arc.png";
            if (File.Exists(path)) return;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            var c = new Vector2(n * 0.5f, n * 0.5f);
            float maxR = n * 0.5f - 1f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - c;
                    float d = p.magnitude;
                    float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                    float a = 0f;
                    if (Mathf.Abs(ang) < 55f && d < maxR)
                    {
                        a = Mathf.Clamp01((maxR - d) / (maxR * 0.16f));
                        a *= Mathf.Clamp01((55f - Mathf.Abs(ang)) / 7f) * 0.9f;
                    }
                    px[y * n + x] = new Color32(255, 240, 210, (byte)(Mathf.Clamp01(a) * 255));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.Refresh();
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spritePixelsPerUnit = 256f;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
        }

        private static GameObject BuildHeldVisual(Transform hand, Transform gun, string name, string spritePath, float scale)
        {
            var sprite = S(spritePath);
            if (sprite == null) return null;

            var go = new GameObject(name);
            go.transform.SetParent(hand, false);
            if (gun != null)
            {
                go.transform.localPosition = gun.localPosition;
                go.transform.localRotation = gun.localRotation;
            }
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -230;
            go.SetActive(false);
            return go;
        }

        // ================================================================== 2. scene

        [MenuItem("Tools/Ares/2. Build Game Scene", false, 2)]
        public static void BuildScene()
        {
            EnsureLayer("Wall");
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Config");
            EnsureFolder(PrefabDir);
            EnsureFolder(PrefabDir + "/Enemies");
            EnsureFolder("Assets/Scenes");
            EnsureFolder(ConfigDir);
            EnsureRingSprite();
            EnsureArcSprite();

            var cfg = EnsureGameConfig();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- camera ----
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = cfg.cameraSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.055f, 0.07f, 1f);
            cam.nearClipPlane = -100f;
            cam.farClipPlane = 100f;
            var rig = camGo.AddComponent<CameraRig>();
            rig.Cam = cam;

            // ---- arena ----
            var arenaGo = new GameObject("Arena");
            var layout = arenaGo.AddComponent<ArenaLayout>();
            int width = Map[0].Length;
            int height = Map.Length;
            layout.BoundsMin = new Vector2(OriginX, OriginY - height);
            layout.BoundsMax = new Vector2(OriginX + width, OriginY);
            layout.CellSize = 0.5f;
            layout.Walls.Clear();

            BuildGround(arenaGo.transform, width, height);
            BuildWalls(arenaGo.transform, layout);
            BuildProps(arenaGo.transform);

            // ---- player ----
            var playerGo = BuildPlayer(layout, out PlayerRig playerRig);

            // ---- prefabs ----
            var projectilePrefab = BuildProjectilePrefab();
            var sparkPrefab = BuildSparkPrefab();
            var pickupPrefab = BuildPickupPrefab();
            var meleeFull = BuildMeleePrefab(false);
            var meleeSimple = BuildMeleePrefab(true);
            var rangedFull = BuildRangedPrefab(false);
            var rangedSimple = BuildRangedPrefab(true);
            var bossPrefab = BuildBossPrefab();

            // ---- systems ----
            var rootGo = new GameObject("GameRoot");
            var manager = rootGo.AddComponent<GameManager>();
            var save = rootGo.AddComponent<SaveService>();
            var items = rootGo.AddComponent<ItemDatabase>();
            var combat = rootGo.AddComponent<CombatSystem>();
            var spawner = rootGo.AddComponent<EnemySpawner>();
            var crafting = rootGo.AddComponent<CraftingSystem>();
            var hud = rootGo.AddComponent<HudUI>();
            var inventoryUi = rootGo.AddComponent<InventoryUI>();
            var menu = rootGo.AddComponent<MenuUI>();
            var minimap = rootGo.AddComponent<MinimapUI>();
            var story = rootGo.AddComponent<StoryUI>();
            var codex = rootGo.AddComponent<CodexUI>();
            var shelter = rootGo.AddComponent<ShelterUI>();
            var lore = rootGo.AddComponent<LoreUI>();

            items.PickupPrefab = pickupPrefab;

            combat.ProjectilePrefab = projectilePrefab;
            combat.SparkPrefab = sparkPrefab;
            combat.PlayerBulletSprite = S(P_PlayerBullet);
            combat.EnemyBulletSprite = S(P_EnemyBullet);
            combat.SparkSprite = S(P_Spark);

            spawner.MeleeFull = meleeFull.GetComponent<EnemyBase>();
            spawner.RangedFull = rangedFull.GetComponent<EnemyBase>();
            spawner.MeleeSimple = meleeSimple.GetComponent<EnemyBase>();
            spawner.RangedSimple = rangedSimple.GetComponent<EnemyBase>();
            spawner.BossPrefab = bossPrefab.GetComponent<EnemyBase>();

            manager.Arena = layout;
            manager.Player = playerGo.GetComponent<PlayerController>();
            manager.Camera = rig;
            manager.Combat = combat;
            manager.Spawner = spawner;
            manager.Items = items;
            manager.Save = save;
            manager.Crafting = crafting;
            manager.Hud = hud;
            manager.Inventory = inventoryUi;
            manager.Menu = menu;
            manager.Minimap = minimap;
            manager.Story = story;
            manager.Codex = codex;
            manager.Shelter = shelter;
            manager.Lore = lore;

            rig.Target = playerGo.transform;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new EditorBuildSettingsScene[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Ares Virus Arena 2";
            PlayerSettings.companyName = "HotZone";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            AssetDatabase.SaveAssets();
            Debug.Log("[Ares] Scene built at " + ScenePath);
        }

        [MenuItem("Tools/Ares/3. Build Windows Player", false, 3)]
        public static void BuildWindows()
        {
            string outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Build");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            var options = new BuildPlayerOptions
            {
                scenes = new string[] { ScenePath },
                locationPathName = Path.Combine(outDir, "AresVirusArena2.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[Ares] Build result: " + report.summary.result + " size=" + report.summary.totalSize + " errors=" + report.summary.totalErrors);
        }

        // ================================================================== ground / walls

        static void BuildGround(Transform parent, int width, int height)
        {
            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = S(P_Ground);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(width, height);
            sr.color = new Color(0.5f, 0.53f, 0.58f, 1f);
            sr.sortingOrder = -1000;
            go.transform.position = new Vector3(OriginX + width * 0.5f, OriginY - height * 0.5f, 0f);

            var frame = new GameObject("Vignette");
            frame.transform.SetParent(parent, false);
            var fsr = frame.AddComponent<SpriteRenderer>();
            fsr.sprite = BuiltinSprite();
            fsr.color = new Color(0.02f, 0.02f, 0.03f, 0.55f);
            fsr.drawMode = SpriteDrawMode.Sliced;
            fsr.size = new Vector2(width + 24f, height + 24f);
            fsr.sortingOrder = -1100;
            frame.transform.position = new Vector3(OriginX + width * 0.5f, OriginY - height * 0.5f, 0f);
        }

        static void BuildWalls(Transform parent, ArenaLayout layout)
        {
            var wallsGo = new GameObject("Walls");
            wallsGo.transform.SetParent(parent, false);
            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer < 0) wallLayer = 0;
            wallsGo.layer = wallLayer;

            var strip = S(P_WallStrip);
            var block = S(P_Wall);
            List<Rect> rects = MergeWallRects();

            foreach (Rect r in rects)
            {
                float cx = OriginX + r.x + r.width * 0.5f;
                float cy = OriginY - r.y - r.height * 0.5f;
                bool vertical = r.height > r.width;
                float length = vertical ? r.height : r.width;

                var colGo = new GameObject("WallCol");
                colGo.layer = wallLayer;
                colGo.transform.SetParent(wallsGo.transform, false);
                colGo.transform.position = new Vector3(cx, cy, 0f);
                var box = colGo.AddComponent<BoxCollider2D>();
                box.size = new Vector2(r.width, r.height);
                layout.Walls.Add(new Rect(OriginX + r.x, OriginY - r.y - r.height, r.width, r.height));

                var sh = new GameObject("WallShadow");
                sh.transform.SetParent(wallsGo.transform, false);
                var shr = sh.AddComponent<SpriteRenderer>();
                shr.sprite = BuiltinSprite();
                shr.drawMode = SpriteDrawMode.Sliced;
                shr.color = new Color(0f, 0f, 0f, 0.32f);
                shr.sortingOrder = -680;
                shr.size = new Vector2(vertical ? r.width + 0.45f : r.width + 0.18f,
                                       vertical ? r.height + 0.1f : r.height + 0.45f);
                sh.transform.position = new Vector3(cx + (vertical ? 0.18f : 0f), cy - (vertical ? 0f : 0.18f), 0f);

                var body = new GameObject("WallBody");
                body.transform.SetParent(wallsGo.transform, false);
                var bsr = body.AddComponent<SpriteRenderer>();
                bsr.sprite = BuiltinSprite();
                bsr.drawMode = SpriteDrawMode.Sliced;
                bsr.size = new Vector2(r.width + 0.02f, r.height + 0.02f);
                bsr.color = new Color(0.27f, 0.28f, 0.32f, 1f);
                bsr.sortingOrder = -675;
                body.transform.position = new Vector3(cx, cy, 0f);

                var vis = new GameObject("WallFace");
                vis.transform.SetParent(wallsGo.transform, false);
                var sr = vis.AddComponent<SpriteRenderer>();
                sr.sortingOrder = -670;
                sr.color = new Color(0.72f, 0.74f, 0.8f, 1f);

                if (strip != null)
                {
                    sr.sprite = strip;
                    sr.drawMode = SpriteDrawMode.Tiled;
                    sr.size = new Vector2(length, 2f);
                    const float faceScale = 1.75f;
                    vis.transform.localScale = new Vector3(1f, faceScale, 1f);
                    float faceOffset = 0.53f * faceScale;
                    if (vertical)
                    {
                        vis.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                        vis.transform.position = new Vector3(cx - r.width * 0.5f + 0.05f + faceOffset, cy, 0f);
                    }
                    else
                    {
                        vis.transform.position = new Vector3(cx, cy + r.height * 0.5f - 0.05f - faceOffset, 0f);
                    }
                }
                else if (block != null)
                {
                    sr.sprite = block;
                    vis.transform.position = new Vector3(cx, cy, 0f);
                    float w = block.bounds.size.x;
                    float s = 1.03f / (w * 0.705f);
                    vis.transform.localScale = new Vector3(s * r.width, s * r.height, 1f);
                }
            }
        }

        static List<Rect> MergeWallRects()
        {
            int h = Map.Length;
            int w = Map[0].Length;
            bool[,] used = new bool[h, w];
            var rects = new List<Rect>();

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (used[y, x] || Map[y][x] != '#') continue;
                    int right = x;
                    while (right + 1 < w && !used[y, right + 1] && Map[y][right + 1] == '#') right++;
                    int bottom = y;
                    bool canGrow = true;
                    while (canGrow && bottom + 1 < h)
                    {
                        for (int cx = x; cx <= right; cx++)
                            if (used[bottom + 1, cx] || Map[bottom + 1][cx] != '#') { canGrow = false; break; }
                        if (canGrow) bottom++;
                    }
                    for (int yy = y; yy <= bottom; yy++)
                        for (int xx = x; xx <= right; xx++)
                            used[yy, xx] = true;
                    rects.Add(new Rect(x, y, right - x + 1, bottom - y + 1));
                }
            }
            return rects;
        }

        static Vector3 CellCenter(int x, int y)
        {
            return new Vector3(OriginX + x + 0.5f, OriginY - y - 0.5f, 0f);
        }

        static void BuildProps(Transform parent)
        {
            var propsGo = new GameObject("Props");
            propsGo.transform.SetParent(parent, false);
            int wallLayer = LayerMask.NameToLayer("Wall");

            for (int y = 0; y < Map.Length; y++)
            {
                string row = Map[y];
                for (int x = 0; x < row.Length; x++)
                {
                    char c = row[x];
                    if (c == '.' || c == '#' || c == 'P') continue;

                    string spritePath;
                    float target;
                    bool collide;
                    int order;
                    float visibleFrac;
                    switch (c)
                    {
                        case 'c': spritePath = P_Crate; target = 1.0f; collide = true; order = -800; visibleFrac = 0.83f; break;
                        case 'r': spritePath = P_Rock; target = 0.95f; collide = true; order = -800; visibleFrac = 0.76f; break;
                        case 'g': spritePath = P_Grass; target = 1.0f; collide = false; order = -900; visibleFrac = 0.84f; break;
                        default: spritePath = P_Debris; target = 0.9f; collide = false; order = -880; visibleFrac = 0.87f; break;
                    }

                    var go = new GameObject(c + "_" + x + "_" + y);
                    go.transform.SetParent(propsGo.transform, false);
                    go.transform.position = CellCenter(x, y) + new Vector3(Random.Range(-0.12f, 0.12f), Random.Range(-0.12f, 0.12f), 0f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    var sp = S(spritePath);
                    sr.sprite = sp;
                    sr.sortingOrder = order;
                    sr.color = c == 'g' ? new Color(0.72f, 0.76f, 0.7f, 0.9f) : new Color(0.86f, 0.87f, 0.9f, 1f);
                    if (sp != null)
                    {
                        float maxDim = Mathf.Max(sp.bounds.size.x, sp.bounds.size.y);
                        if (maxDim > 0.001f) go.transform.localScale = Vector3.one * (target / (maxDim * visibleFrac));
                    }
                    if (collide)
                    {
                        go.layer = wallLayer >= 0 ? wallLayer : 0;
                        var box = go.AddComponent<BoxCollider2D>();
                        box.size = new Vector2(0.85f, 0.8f);
                    }
                }
            }
        }

        // ================================================================== player + rig

        static GameObject BuildPlayer(ArenaLayout layout, out PlayerRig playerRig)
        {
            var go = new GameObject("Player");
            var pc = go.AddComponent<PlayerController>();
            var weapons = go.AddComponent<PlayerCombat>();
            playerRig = go.AddComponent<PlayerRig>();
            playerRig.baseWeight = 1f;

            var art = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(P_Neo));
            art.name = "neo";
            art.transform.SetParent(go.transform, false);
            FitVisual(art, 1.92f);

            pc.VisualRoot = art.transform;
            pc.Rig = playerRig;
            pc.Weapons = weapons;

            // ---- IK rig on the right arm (feedback #10 / #17) ----
            var gun = FindDeep(art.transform, "neo_gun");
            var hand = gun != null ? gun.parent : FindDeep(art.transform, "neo_hand1");
            var forearm = hand != null ? hand.parent : null;
            var upperArm = forearm != null ? forearm.parent : null;
            var muzzle = gun != null ? FindDeep(gun, "weapon_huoqiang_huoguang") : null;

            if (gun != null && hand != null && forearm != null && upperArm != null)
            {
                var rigGo = new GameObject("AimRig");
                rigGo.transform.SetParent(art.transform, false);
                var rig = rigGo.AddComponent<Rig>();

                var ikGo = new GameObject("RightArmIK");
                ikGo.transform.SetParent(rigGo.transform, false);
                var ik = ikGo.AddComponent<TwoBoneIKConstraint>();
                ik.data.root = upperArm;
                ik.data.mid = forearm;
                ik.data.tip = hand;
                ik.data.targetPositionWeight = 1f;
                ik.data.targetRotationWeight = 0f;
                ik.data.hintWeight = 1f;

                var aimTarget = new GameObject("AimTarget");
                aimTarget.transform.SetParent(rigGo.transform, false);
                var aimHint = new GameObject("AimHint");
                aimHint.transform.SetParent(rigGo.transform, false);

                ik.data.target = aimTarget.transform;
                ik.data.hint = aimHint.transform;

                var builder = art.GetComponent<RigBuilder>();
                if (builder == null) builder = art.AddComponent<RigBuilder>();
                builder.layers.Clear();
                builder.layers.Add(new RigLayer(rig, true));

                playerRig.AimTarget = aimTarget.transform;
                playerRig.AimHint = aimHint.transform;
                playerRig.Shoulder = upperArm;
                playerRig.Hand = hand;
                playerRig.Gun = gun;
                playerRig.Muzzle = muzzle;

                // machete / bow models, hidden until that weapon is equipped
                playerRig.SwordVisual = BuildHeldVisual(hand, gun, "SwordModel", "Assets/Resources/Player/weapon_sword.png", 0.62f);
                playerRig.BowVisual = BuildHeldVisual(hand, gun, "BowModel", "Assets/Resources/Player/weapon_bow.png", 0.72f);
                playerRig.AimRig = rig;
                playerRig.ArmIk = ik;
                Debug.Log("[Ares] IK rig built on " + upperArm.name + " -> " + forearm.name + " -> " + hand.name + ", gun=" + gun.name);
            }
            else
            {
                Debug.LogWarning("[Ares] Could not find the right arm chain; IK rig skipped.");
            }

            var arcGo = new GameObject("MeleeArc");
            arcGo.transform.SetParent(go.transform, false);
            var arcSr = arcGo.AddComponent<SpriteRenderer>();
            arcSr.sprite = S(GeneratedDir + "/melee_arc.png");
            arcSr.color = new Color(1f, 0.94f, 0.78f, 0f);
            arcSr.sortingOrder = -640;
            var arcView = go.AddComponent<MeleeArcView>();
            arcView.Arc = arcSr;
            arcView.Owner = go.transform;
            Vector2 start = Vector2.zero;
            for (int y = 0; y < Map.Length; y++)
            {
                int idx = Map[y].IndexOf('P');
                if (idx >= 0) { Vector3 c = CellCenter(idx, y); start = new Vector2(c.x, c.y); break; }
            }
            layout.PlayerStart = start;
            go.transform.position = new Vector3(start.x, start.y, 0f);
            return go;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static void FitVisual(GameObject root, float targetHeight)
        {
            Bounds b = ComputeBounds(root);
            if (b.size.y > 0.0001f)
            {
                float s = targetHeight / b.size.y;
                root.transform.localScale = new Vector3(s, s, 1f);
            }
            b = ComputeBounds(root);
            root.transform.localPosition += new Vector3(-b.center.x, -b.min.y - 0.5f, 0f);
        }

        static Bounds ComputeBounds(GameObject root)
        {
            var rends = root.GetComponentsInChildren<SpriteRenderer>(true);
            bool has = false;
            Bounds b = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in rends)
            {
                if (r == null || r.sprite == null) continue;
                if (IsShadowSprite(r.sprite)) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!has) return new Bounds(Vector3.zero, new Vector3(1f, 1f, 0f));
            return b;
        }

        static bool IsShadowSprite(Sprite s)
        {
            if (s == null) return false;
            string n = s.name.ToLowerInvariant();
            return n.Contains("yinying") || n.Contains("shadow");
        }

        static SpriteRenderer FirstRenderer(GameObject root)
        {
            var rends = root.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer best = null;
            float bestArea = -1f;
            foreach (var r in rends)
            {
                if (r == null || r.sprite == null) continue;
                if (IsShadowSprite(r.sprite)) continue;
                float a = r.sprite.bounds.size.x * r.sprite.bounds.size.y;
                if (a > bestArea) { bestArea = a; best = r; }
            }
            if (best == null && rends.Length > 0) best = rends[0];
            return best;
        }

        // ================================================================== enemy prefabs

        static GameObject BuildMeleePrefab(bool simple)
        {
            string path = PrefabDir + "/Enemies/Melee_" + (simple ? "Simple" : "Full") + ".prefab";
            var go = new GameObject(simple ? "MeleeEnemy_Simple" : "MeleeEnemy_Full");
            var eb = go.AddComponent<MeleeEnemy>();
            eb.Kind = EnemyKind.Melee;
            eb.SimpleVisual = simple;

            if (simple)
            {
                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(go.transform, false);
                var sr = bodyGo.AddComponent<SpriteRenderer>();
                sr.sprite = S(P_ChushouBody);
                FitSprite(sr, 1.2f);
                eb.Body = sr;
            }
            else
            {
                var art = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(P_Chushouguai));
                art.name = "chushouguai";
                art.transform.SetParent(go.transform, false);
                FitVisual(art, 1.72f);
                eb.VisualRoot = art.transform;
                eb.Body = FirstRenderer(art);
            }

            var asset = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return asset;
        }

        static GameObject BuildRangedPrefab(bool simple)
        {
            string path = PrefabDir + "/Enemies/Ranged_" + (simple ? "Simple" : "Full") + ".prefab";
            var go = new GameObject(simple ? "RangedEnemy_Simple" : "RangedEnemy_Full");
            var eb = go.AddComponent<RangedEnemy>();
            eb.Kind = EnemyKind.Ranged;
            eb.SimpleVisual = simple;

            if (simple)
            {
                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(go.transform, false);
                var sr = bodyGo.AddComponent<SpriteRenderer>();
                sr.sprite = S(P_OutuzheBody);
                FitSprite(sr, 1.25f);
                eb.Body = sr;
            }
            else
            {
                var art = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(P_Outuzhe));
                art.name = "outuzhe";
                art.transform.SetParent(go.transform, false);
                FitVisual(art, 1.6f);
                eb.VisualRoot = art.transform;
                eb.Body = FirstRenderer(art);
            }

            var asset = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return asset;
        }

        static GameObject BuildBossPrefab()
        {
            string path = PrefabDir + "/Enemies/Boss.prefab";
            var go = new GameObject("BossEnemy");

            var ringGo = new GameObject("BossRing");
            ringGo.transform.SetParent(go.transform, false);
            var ringSr = ringGo.AddComponent<SpriteRenderer>();
            ringSr.sprite = S(GeneratedDir + "/boss_ring.png");
            ringSr.color = new Color(1f, 0.22f, 0.18f, 0.75f);
            ringSr.sortingOrder = -600;
            ringGo.transform.localPosition = new Vector3(0f, -0.42f, 0f);
            ringGo.transform.localScale = new Vector3(1.5f, 0.9f, 1f);

            var eb = go.AddComponent<BossEnemy>();
            eb.Kind = EnemyKind.Boss;
            eb.SimpleVisual = false;
            eb.BossRing = ringSr;

            var art = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(P_Chushouguai));
            art.name = "chushouguai";
            art.transform.SetParent(go.transform, false);
            FitVisual(art, 1.95f);
            eb.VisualRoot = art.transform;
            eb.Body = FirstRenderer(art);

            var asset = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return asset;
        }

        static void FitSprite(SpriteRenderer sr, float targetHeight)
        {
            if (sr == null || sr.sprite == null) return;
            float h = sr.sprite.bounds.size.y;
            if (h > 0.0001f) sr.transform.localScale = Vector3.one * (targetHeight / h);
        }

        // ================================================================== fx prefabs

        static Projectile BuildProjectilePrefab()
        {
            var go = new GameObject("Projectile");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = S(P_PlayerBullet);
            sr.sortingOrder = 100;
            var pr = go.AddComponent<Projectile>();
            pr.Renderer = sr;
            var asset = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/Projectile.prefab");
            Object.DestroyImmediate(go);
            return asset.GetComponent<Projectile>();
        }

        static HitSpark BuildSparkPrefab()
        {
            var go = new GameObject("HitSpark");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = S(P_Spark);
            sr.sortingOrder = 200;
            var sp = go.AddComponent<HitSpark>();
            sp.Renderer = sr;
            var asset = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/HitSpark.prefab");
            Object.DestroyImmediate(go);
            return asset.GetComponent<HitSpark>();
        }

        static LootPickup BuildPickupPrefab()
        {
            var go = new GameObject("LootPickup");

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);
            var icon = iconGo.AddComponent<SpriteRenderer>();
            icon.sortingOrder = 60;

            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(go.transform, false);
            var glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sprite = BuiltinSprite();
            glow.color = new Color(1f, 0.88f, 0.45f, 0.45f);
            glow.sortingOrder = 59;
            glowGo.transform.localScale = new Vector3(0.9f, 0.9f, 1f);

            var lp = go.AddComponent<LootPickup>();
            lp.Icon = icon;
            lp.Glow = glow;

            var asset = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + "/LootPickup.prefab");
            Object.DestroyImmediate(go);
            return asset.GetComponent<LootPickup>();
        }

        static Sprite _builtin;

        static Sprite BuiltinSprite()
        {
            if (_builtin != null) return _builtin;
            _builtin = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (_builtin == null) _builtin = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            return _builtin;
        }

        static Sprite S(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogWarning("[Ares] sprite not found: " + path);
            return s;
        }

        // ================================================================== config asset

        public static GameConfig EnsureGameConfig()
        {
            const string path = "Assets/Resources/GameConfig.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
            if (existing != null) return existing;
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(cfg, path);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        // ================================================================== helpers

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        public static void EnsureLayer(string name)
        {
            if (LayerMask.NameToLayer(name) >= 0) return;
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0) return;
            var so = new SerializedObject(asset[0]);
            var layers = so.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var el = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(el.stringValue))
                {
                    el.stringValue = name;
                    so.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    return;
                }
            }
        }
    }
}
