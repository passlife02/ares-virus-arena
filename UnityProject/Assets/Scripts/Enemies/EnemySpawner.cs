using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Wave director. Handles the configurable run length (1..endless), the countdown to the
    /// next wave, a difficulty curve that ramps and then caps, a boss every N waves, and the
    /// pressure-test mode that keeps a fixed number of enemies alive.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Prefabs")]
        public EnemyBase MeleeFull;
        public EnemyBase RangedFull;
        public EnemyBase MeleeSimple;
        public EnemyBase RangedSimple;
        public EnemyBase BossPrefab;

        /// <summary>The most recently spawned enemy (used by spider eggs to place their child).</summary>
        public EnemyBase LastSpawned { get; private set; }

        public Transform EnemyRoot;

        [Header("Tuning")]
        public bool ForceFullVisuals = false;
        public float ThinkInterval = 0.1f;
        public float RepathInterval = 0.12f;

        public readonly SpatialHash Hash = new SpatialHash(1.0f);
        public readonly List<EnemyBase> NeighbourBuffer = new List<EnemyBase>(48);

        private readonly List<EnemyBase> _alive = new List<EnemyBase>(2048);
        private readonly Dictionary<EnemyBase, Stack<EnemyBase>> _pools = new Dictionary<EnemyBase, Stack<EnemyBase>>();
        private readonly List<EnemyBase> _query = new List<EnemyBase>(64);
        private readonly System.Random _rng = new System.Random(20260101);

        private float _repathTimer;
        private float _stressTimer;
        private int _wave;
        private int _totalWaves;
        private float _waveTimer;
        private bool _waveMode;
        private bool _awaitingClear;
        private bool _stressMode;
        private int _stressTarget;
        private int _totalSpawned;
        private int _totalKilled;
        private int _liveCount;

        public int LiveCount { get { return _liveCount; } }
        public int AliveCount { get { return _alive.Count; } }
        public int Wave { get { return _wave; } }
        public int TotalWaves { get { return _totalWaves; } }
        public int TotalSpawned { get { return _totalSpawned; } }
        public int TotalKilled { get { return _totalKilled; } }
        public bool StressMode { get { return _stressMode; } }
        public int StressTarget { get { return _stressTarget; } }
        public float NextWaveIn { get { return Mathf.Max(0f, _waveTimer); } }

        /// <summary>Fixed runs count waves down; endless runs never run out.</summary>
        public bool HasNextWave { get { return _totalWaves <= 0 || _wave < _totalWaves; } }

        /// <summary>True on the last wave of a fixed run (or any wave in endless).</summary>
        public bool IsFinalWave { get { return _totalWaves > 0 && _wave >= _totalWaves; } }
        public bool AwaitingClear { get { return _awaitingClear; } }
        public bool BossAlive
        {
            get
            {
                for (int i = 0; i < _alive.Count; i++)
                    if (_alive[i] != null && _alive[i].IsAlive && _alive[i].EnemyKind == EnemyKind.Boss) return true;
                return false;
            }
        }

        public void Init()
        {
            if (EnemyRoot == null)
            {
                var go = new GameObject("Enemies");
                go.transform.SetParent(transform, false);
                EnemyRoot = go.transform;
            }
            _alive.Clear();
            _pools.Clear();
            _liveCount = 0;
        }

        // ------------------------------------------------------------------ modes

        public void BeginWaves(int totalWaves)
        {
            ClearAll();
            _waveMode = true;
            _awaitingClear = false;
            _stressMode = false;
            _stressTarget = 0;
            _totalSpawned = 0;
            _totalKilled = 0;
            _wave = 0;
            _totalWaves = totalWaves;
            _waveTimer = 1.4f;
        }

        public void BeginStress(int count)
        {
            ClearAll();
            _waveMode = false;
            _awaitingClear = false;
            _stressMode = true;
            _stressTarget = Mathf.Max(1, count);
            _totalSpawned = 0;
            _totalKilled = 0;
            _wave = 0;
            _totalWaves = 0;
            _waveTimer = 0f;
            _stressTimer = 0f;
            SpawnStressBatch(_stressTarget);
        }

        /// <summary>True while a wave or stress run is ticking.</summary>
        public bool IsRunning { get { return _waveMode || _stressMode; } }

        public void StopAll()
        {
            _waveMode = false;
            _awaitingClear = false;
            _stressMode = false;
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            if (Game.Manager == null || Game.Manager.Phase != GamePhase.Playing) return;

            Hash.Clear();
            for (int i = 0; i < _alive.Count; i++)
            {
                var e = _alive[i];
                if (e != null && e.IsAlive) Hash.Insert(e);
            }

            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                _repathTimer = RepathInterval;
                if (Game.Player != null && Game.Nav != null) Game.Nav.Recompute(Game.Player.Position);
            }

            TickSupplyDrops();

            if (_waveMode) TickWaves();
            else if (_awaitingClear) TickClear();
            else if (_stressMode) TickStress();
        }

        // ---------------------------------------------------------- military supply drops
        private float _supplyTimer = 18f;
        private static readonly string[] SupplyTable = new string[]
        {
            "tool_bengdai", "tool_bengdai", "ammo_huoyao", "ammo_huoyao",
            "tool_grenade", "tool_fence", "ammo_yumaojian", "mat_yaocao",
        };

        /// <summary>Random military crates: bandages, powder, grenades — the Ares survival staples.</summary>
        private void TickSupplyDrops()
        {
            if (Game.Nav == null || Game.Items == null || Game.Player == null) return;
            if (!_waveMode && !_stressMode && !_awaitingClear) return;

            _supplyTimer -= Time.deltaTime;
            if (_supplyTimer > 0f) return;
            _supplyTimer = Random.Range(22f, 34f);

            Vector2 spot;
            if (!Game.Nav.TryRandomOpenPoint(new System.Random(Random.Range(1, 999999)), 6, out spot)) return;

            string id = SupplyTable[Random.Range(0, SupplyTable.Length)];
            int count = id == "tool_bengdai" ? 2 : (id.StartsWith("ammo_") ? 12 : 1);
            if (Game.Items.SpawnPickup(id, count, spot) == null) return;

            // a faint gold ring on the ground so the crate is findable
            Fx.Play("fx_range", spot, 1.7f, 1.1f, 0f, new Color(1f, 0.82f, 0.42f, 0.55f), -160);
            Fx.Play("fx_range", spot, 1.1f, 1.6f, 0f, new Color(1f, 0.9f, 0.6f, 0.35f), -161);
            if (Game.Hud != null) Game.Hud.ShowPrompt("发现军事补给：靠近拾取", 1.8f);
        }

        private void TickWaves()
        {
            // Last wave of a fixed run: no countdown and no new wave — clearing the field ends it.
            if (_totalWaves > 0 && _wave >= _totalWaves)
            {
                if (_liveCount == 0)
                {
                    _waveMode = false;
                    _awaitingClear = true;
                }
                return;
            }

            _waveTimer -= Time.deltaTime;

            // Clearing the field early pulls the next wave forward, but never instantly.
            if (_liveCount == 0 && _wave > 0 && _waveTimer > 1.6f)
                _waveTimer = 1.6f;

            if (_waveTimer <= 0f) StartNextWave();
        }

        private void TickClear()
        {
            if (_liveCount > 0) return;
            _awaitingClear = false;
            if (Game.Manager != null) Game.Manager.OnVictory();
        }

        private void TickStress()
        {
            if (_stressTarget <= 0) return;
            _stressTimer -= Time.deltaTime;
            if (_stressTimer > 0f) return;
            _stressTimer = 0.2f;

            int deficit = _stressTarget - _liveCount;
            if (deficit <= 0) return;
            int budget = Mathf.Min(deficit, 12);
            for (int i = 0; i < budget; i++)
            {
                EnemyKind kind = ((_totalSpawned + i) % 5 == 4) ? EnemyKind.Ranged : EnemyKind.Melee;
                if (!Spawn(kind, 1f, 1f, true)) break;
            }
        }

        // ------------------------------------------------------------------ waves

        private void StartNextWave()
        {
            var cfg = Game.Cfg;
            _wave++;

            if (_totalWaves > 0 && _wave > _totalWaves)
            {
                _waveMode = false;
                _awaitingClear = true;
                if (Game.Hud != null) Game.Hud.ShowBanner("清空残余感染者");
                return;
            }

            Milestones.AtLeast("wave", _wave);
            AudioManager.PlaySfx("sfx_wave", 0.7f, 0.03f);
            int cappedWave = Mathf.Min(_wave, cfg.maxDifficultyWave);
            float hpScale = 1f + (cappedWave - 1) * cfg.hpPerWave;
            float dmgScale = 1f + (cappedWave - 1) * cfg.dmgPerWave;
            int baseCount = Mathf.RoundToInt(2 + cappedWave * cfg.countPerWave * 2f);
            float areaScale = Game.Manager != null ? Game.Manager.EnemyScale : 1f;
            baseCount = Mathf.Max(2, Mathf.RoundToInt(baseCount * areaScale));
            if (_totalWaves > 0) baseCount = Mathf.Min(baseCount, cfg.spawnMaxAliveNormal);

            int melee = Mathf.Max(1, baseCount - Mathf.FloorToInt(cappedWave / 3f));
            int ranged = Mathf.Max(0, baseCount - melee);

            // Boss rules (feedback #8 item 5):
            //  - the final wave of a run always has a boss;
            //  - from wave 11 the chance is +10% per ten waves, capped at 30% after wave 30;
            //  - bosses never exceed 30% of the normal enemies of that wave.
            int bossCount = 0;
            if (BossPrefab != null)
            {
                int normals = melee + ranged;
                int maxBosses = Mathf.Max(1, Mathf.FloorToInt(normals * 0.3f));

                bool finalWave = _totalWaves > 0 && _wave >= _totalWaves;
                float chance = 0f;
                if (_wave > 10)
                {
                    int steps = Mathf.CeilToInt((_wave - 10) / 10f);
                    chance = Mathf.Clamp(steps * 0.10f, 0f, 0.30f);
                }

                if (finalWave || Random.value < chance)
                {
                    bossCount = 1;
                    // later waves can roll a second boss, still under the 30% cap
                    if (!finalWave && _wave >= 20 && Random.value < 0.35f) bossCount = 2;
                    bossCount = Mathf.Min(bossCount, maxBosses);
                }
            }

            for (int i = 0; i < melee; i++) Spawn(EnemyKind.Melee, hpScale, dmgScale, false);
            for (int i = 0; i < ranged; i++) Spawn(EnemyKind.Ranged, hpScale, dmgScale, false);

            if (Game.BarrelRoot != null) Barrel.SpawnForWave(_wave, Game.BarrelRoot);

            if (bossCount > 0)
            {
                AudioManager.PlayMusic("music_boss", 1.2f);
                for (int i = 0; i < bossCount; i++) Spawn(EnemyKind.Boss, hpScale, dmgScale, false);
                if (Game.Hud != null)
                {
                    Game.Hud.ShowBanner(bossCount > 1 ? ("BOSS ×" + bossCount + " 出现！") : "BOSS 出现！");
                    Game.Hud.FlashBossWarning();
                }
                if (Game.Camera != null) Game.Camera.AddShake(0.5f);
            }
            else if (Game.Hud != null)
            {
                Game.Hud.ShowBanner(_totalWaves > 0
                    ? "第 " + _wave + " / " + _totalWaves + " 波"
                    : "第 " + _wave + " 波");
            }

            // 7s at wave 1 ramping to 20s by wave 30, for both fixed and endless runs
            _waveTimer = Mathf.Lerp(cfg.waveInterval, 20f, Mathf.Clamp01((_wave - 1) / 29f));
            if (Game.Manager != null) Game.Manager.OnWaveChanged(_wave);
        }

        private void SpawnStressBatch(int total)
        {
            for (int i = 0; i < total; i++)
            {
                EnemyKind kind = (i % 5 == 4) ? EnemyKind.Ranged : EnemyKind.Melee;
                if (!Spawn(kind, 1f, 1f, true)) break;
            }
            if (Game.Hud != null) Game.Hud.ShowBanner("压力测试：" + total + " 敌人");
        }

        // ------------------------------------------------------------------ spawning

        public bool Spawn(EnemyKind kind, float hpScale, float dmgScale, bool forceStress)
        {
            var cfg = Game.Cfg;
            bool simple = !ForceFullVisuals && (_stressMode || forceStress || _alive.Count >= cfg.stressVisualThreshold);
            EnemyBase prefab;
            if (kind == EnemyKind.Boss) prefab = BossPrefab;
            else if (kind == EnemyKind.Melee) prefab = (simple && MeleeSimple != null) ? MeleeSimple : MeleeFull;
            else prefab = (simple && RangedSimple != null) ? RangedSimple : RangedFull;

            if (prefab == null)
            {
                Debug.LogError("[Ares] Enemy prefab missing for " + kind);
                return false;
            }

            Vector2 pos;
            if (!TryPickSpawnPoint(out pos)) return false;

            EnemyBase e = GetFromPool(prefab);
            if (e == null) return false;

            if (kind == EnemyKind.Boss)
            {
                var boss = e as BossEnemy;
                if (boss != null)
                    boss.KindVariant = Random.value < 0.5f ? BossEnemy.Variant.Armadillo : BossEnemy.Variant.Spider;
            }
            e.transform.SetParent(EnemyRoot, false);
            e.Setup(pos, hpScale, dmgScale, 1f);
            _alive.Add(e);
            _liveCount++;
            _totalSpawned++;
            LastSpawned = e;
            return true;
        }

        private EnemyBase GetFromPool(EnemyBase prefab)
        {
            Stack<EnemyBase> stack;
            if (_pools.TryGetValue(prefab, out stack) && stack.Count > 0) return stack.Pop();
            return Instantiate(prefab, EnemyRoot);
        }

        private bool TryPickSpawnPoint(out Vector2 pos)
        {
            pos = Vector2.zero;
            var nav = Game.Nav;
            if (nav == null) return false;
            var player = Game.Player;
            int attempts = _stressMode ? 120 : 60;
            float occRadius = _stressMode ? 0.45f : 0.9f;

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int x = _rng.Next(2, nav.Width - 2);
                int y = _rng.Next(2, nav.Height - 2);
                if (nav.IsBlocked(x, y)) continue;
                Vector2 c = nav.CellCenter(x, y);
                if (player != null)
                {
                    float d = (c - player.Position).magnitude;
                    float minD = _stressMode ? 5.5f : 9f;
                    if (d < minD) continue;
                }
                if (IsOccupied(c, occRadius)) continue;
                pos = c + Random.insideUnitCircle * 0.25f;
                return true;
            }

            for (int attempt = 0; attempt < 40; attempt++)
            {
                int x = _rng.Next(2, nav.Width - 2);
                int y = _rng.Next(2, nav.Height - 2);
                if (nav.IsBlocked(x, y)) continue;
                pos = nav.CellCenter(x, y);
                return true;
            }
            return false;
        }

        /// <summary>Public helper used by barrel drops: a walkable cell away from the player.</summary>
        public bool TryPickFreePoint(out Vector2 pos, float minPlayerDist, float occRadius)
        {
            pos = Vector2.zero;
            var nav = Game.Nav;
            if (nav == null) return false;
            var player = Game.Player;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                int x = _rng.Next(2, nav.Width - 2);
                int y = _rng.Next(2, nav.Height - 2);
                if (nav.IsBlocked(x, y)) continue;
                Vector2 c = nav.CellCenter(x, y);
                if (player != null && (c - player.Position).magnitude < minPlayerDist) continue;
                if (IsOccupied(c, occRadius)) continue;
                pos = c;
                return true;
            }
            return false;
        }

        private bool IsOccupied(Vector2 p, float radius)
        {
            float r2 = radius * radius;
            for (int i = 0; i < _alive.Count; i++)
            {
                var e = _alive[i];
                if (e == null || !e.IsAlive) continue;
                if ((e.Position - p).sqrMagnitude < r2) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ queries

        public EnemyBase FindEnemyNear(Vector2 point, float radius)
        {
            float r2 = radius * radius;
            EnemyBase best = null;
            float bestD = float.MaxValue;

            _query.Clear();
            Hash.Query(point, _query);
            for (int i = 0; i < _query.Count; i++)
            {
                var e = _query[i];
                if (e == null || !e.IsAlive) continue;
                float d2 = (e.Position - point).sqrMagnitude;
                if (d2 <= r2 + 0.16f && d2 < bestD) { bestD = d2; best = e; }
            }
            return best;
        }

        /// <summary>Used by the crosshair to know whether the cursor is over an enemy.</summary>
        public EnemyBase FindEnemyUnderCursor(Vector2 worldPoint, float radius)
        {
            return FindEnemyNear(worldPoint, radius);
        }

        public void OnEnemyDied(EnemyBase e)
        {
            _totalKilled++;
            _liveCount = Mathf.Max(0, _liveCount - 1);
            if (Game.Manager != null) Game.Manager.OnEnemyDied(e);
            if (Game.Items != null && Game.Manager != null && Game.Manager.Phase == GamePhase.Playing)
                Game.Items.RollDrops(e.EnemyKind, e.Position);
        }

        public void ReleaseEnemy(EnemyBase e)
        {
            if (e == null) return;
            _alive.Remove(e);
            e.gameObject.SetActive(false);
            if (e.SimpleVisual)
            {
                EnemyBase key = e.EnemyKind == EnemyKind.Ranged ? RangedSimple : MeleeSimple;
                if (key != null)
                {
                    Stack<EnemyBase> stack;
                    if (!_pools.TryGetValue(key, out stack))
                    {
                        stack = new Stack<EnemyBase>();
                        _pools[key] = stack;
                    }
                    if (stack.Count < 500) { stack.Push(e); return; }
                }
            }
            Destroy(e.gameObject);
        }

        public void ClearAll()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
                if (_alive[i] != null) Destroy(_alive[i].gameObject);
            _alive.Clear();
            _pools.Clear();
            Hash.Clear();
            _liveCount = 0;
        }

        public IReadOnlyList<EnemyBase> Alive { get { return _alive; } }
    }
}
