using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Game flow: boot -> menu -> playing -> paused -> victory/defeat -> restart.
    /// Also the only place that decides which inputs are legal per phase, and owns the
    /// cursor policy (locked during play, released on Esc, feedback #4).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("Scene references (wired by the Scene Builder)")]
        public ArenaLayout Arena;
        public MapTransition MapFx;
        public Trailer Trailer;

        /// <summary>Set from the menu; a run uses this map instead of cycling.</summary>
        public bool MapPicked { get; set; }

        /// <summary>Current layout, cycled every run.</summary>
        public MapVariant Map { get; set; }
        public string MapName { get { return MapBuilder.Names[(int)Map]; } }

        /// <summary>Enemy budget multiplier: bigger arenas get more monsters.</summary>
        public float EnemyScale { get; private set; }
        public PlayerController Player;
        public CameraRig Camera;
        public CombatSystem Combat;
        public EnemySpawner Spawner;
        public ItemDatabase Items;
        public SaveService Save;
        public CraftingSystem Crafting;
        public HudUI Hud;
        public InventoryUI Inventory;
        public MenuUI Menu;
        public MinimapUI Minimap;
        public StoryUI Story;
        public LoreUI Lore;
        public CodexUI Codex;
        public ShelterUI Shelter;

        public GamePhase Phase { get; private set; }
        public int Kills { get; private set; }
        public int Wave { get; private set; }
        public bool StressMode { get; private set; }
        public int StressCount { get; private set; }
        public int TotalWaves { get; private set; }

        /// <summary>Rebakes the nav grid and re-fits the camera after a layout change.</summary>
        public void RebuildNavigation()
        {
            if (Game.Nav != null && Arena != null)
                Game.Nav.Bake(Arena.Walls, Game.Cfg != null ? Game.Cfg.playerRadius : 0.28f);

            if (Camera != null && Arena != null)
                Camera.Configure(Camera.GetComponent<Camera>(), Player != null ? Player.transform : transform,
                    ArenaMin, ArenaMax, Game.Cfg != null ? Game.Cfg.cameraSize : 5.9f);

            float area = (ArenaMax.x - ArenaMin.x) * (ArenaMax.y - ArenaMin.y);
            const float baseArea = 44f * 26f;
            EnemyScale = Mathf.Clamp(area / baseArea, 1f, 1.5f);
        }

        public Vector2 ArenaMin { get { return Arena != null ? Arena.BoundsMin : new Vector2(-22f, -13f); } }
        public Vector2 ArenaMax { get { return Arena != null ? Arena.BoundsMax : new Vector2(22f, 13f); } }

        private bool _resultScheduled;
        private bool _lastStress;
        private int _lastStressCount;
        private int _lastTotalWaves;
        private Coroutine _hitStop;
        private bool _storyPlayed;
        private bool _booted;
        private bool _startingRun;
        private float _bootAt;
        private int _runFrame = -1;
        private readonly System.Collections.Generic.List<KeyValuePair<string, int>> _loadout =
            new System.Collections.Generic.List<KeyValuePair<string, int>>();
        private float _watchdogTimer;

        private void Awake()
        {
            Game.Manager = this;
            Game.Cfg = GameConfig.Get();
            Game.ResetRuntime();
            Phase = GamePhase.Boot;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;
        }

        private void Start()
        {
            Boot();
        }

        private void Boot()
        {
            var cfg = Game.Cfg;

            EnsureAudio();
            AudioManager.PlayMusic("music_menu");

            if (Items != null) { Items.Init(); Game.Items = Items; }
            if (Crafting != null) { Game.Crafting = Crafting; }

            if (Save != null) { Save.Init(); Game.Save = Save; }

            Game.Inventory = new InventoryModel();
            if (Save != null)
            {
                Save.Bind(Game.Inventory);
                Save.LoadInventory(Game.Inventory);
            }

            Game.Stash = new InventoryModel();
            if (Save != null)
            {
                Save.LoadStash(Game.Stash);
                Game.Stash.Changed += () => Save.SaveStash(Game.Stash);
            }

            Game.Equipment = new EquipmentModel();
            Game.Equipment.Bind(Game.Inventory);
            GrantStarterKit();

            Game.Nav = Arena != null
                ? new GridNav(Arena.Bounds, Arena.CellSize)
                : new GridNav(new Rect(-22f, -13f, 44f, 26f), 0.5f);
            Game.Nav.Bake(Arena != null ? Arena.Walls : new System.Collections.Generic.List<Rect>(), cfg.playerRadius);

            Game.Spawner = Spawner;
            Game.Combat = Combat;
            Game.Player = Player;
            Game.Combat2 = Player != null ? Player.Weapons : null;

            if (Combat != null) Combat.Init();
            if (Spawner != null) Spawner.Init();

            if (Player != null) Player.Spawn(Arena != null ? Arena.PlayerStart : Vector2.zero);

            if (Camera != null)
            {
                Game.Camera = Camera;
                Camera.Configure(Camera.GetComponent<Camera>(), Player != null ? Player.transform : transform, ArenaMin, ArenaMax, cfg.cameraSize);
            }

            if (Hud != null) { Hud.Build(); Game.Hud = Hud; }
            if (Inventory != null) Inventory.Build();
            if (Menu != null) Menu.Build();
            if (Minimap != null) { Minimap.Build(); Game.Minimap = Minimap; }
            if (Story != null) Story.Build();
            if (Codex != null) Codex.Build();
            if (Shelter != null) Shelter.Build();
            if (Lore != null) Lore.Build();

            if (MapFx == null) MapFx = gameObject.AddComponent<MapTransition>();
            MapFx.Build();

            MapBuilder.BuildAll(this);
            MapBuilder.Show(Map, this);

            _booted = true;
            _bootAt = Time.unscaledTime;
            GoToMenu();

            // opening trailer, then the menu is revealed
            if (Trailer == null) Trailer = gameObject.AddComponent<Trailer>();
            Trailer.Build();
            if (Menu != null) Menu.ShowNone();
            StartCoroutine(PlayTrailer());
        }

        /// <summary>First launch: hand out a gun, its ammo and a knife (feedback #13).</summary>
        private void GrantStarterKit()
        {
            var inv = Game.Inventory;
            if (inv == null || Game.Items == null) return;

            bool hasWeapon = false;
            for (int i = 0; i < InventoryModel.Capacity; i++)
            {
                var s = (Game.Stash != null ? Game.Stash : inv).Slots[i];
                if (s.IsEmpty) continue;
                var d = Game.Items.Get(s.itemId);
                if (d != null && d.IsWeapon) { hasWeapon = true; break; }
            }
            if (hasWeapon) return;

            // Nothing is granted automatically any more: when both containers are empty the
            // merchant hands out the relief package (feedback #5 item 1).
            if (Game.Stash != null && Game.Stash.TotalItemCount == 0 && Game.Inventory != null && Game.Inventory.TotalItemCount == 0)
                Debug.Log("[Ares] Both containers are empty - collect the relief package from the merchant.");
        }

        // ------------------------------------------------------------------ flow

        private System.Collections.IEnumerator PlayTrailer()
        {
            if (Trailer != null)
            {
                yield return Trailer.Play();
                if (Menu != null) Menu.ShowMain();
            }
        }

        private void GoToMenu()
        {
            if (Time.frameCount == _runFrame) return;   // a run started this frame
            Phase = GamePhase.MainMenu;
            Time.timeScale = 1f;
            if (Spawner != null) Spawner.ClearAll();
            if (Combat != null) Combat.ClearAll();
            if (Items != null) Items.ClearPickups();
            if (Hud != null) Hud.SetVisible(false);
            if (Minimap != null) Minimap.SetVisible(false);
            if (Inventory != null) Inventory.SetOpen(false);
            if (Menu != null) Menu.ShowMain();
            if (Player != null) Player.SetFrozen(true);
            if (Camera != null) Camera.SetSize(Game.Cfg.cameraSize * 0.46f, false);
            SetCursor(false);
        }

        /// <summary>Old saves kept loot in the backpack; the shelter now owns everything.</summary>
        private void MigrateBagToStash()
        {
            var bag = Game.Inventory;
            var stash = Game.Stash;
            if (bag == null || stash == null || bag.TotalItemCount == 0) return;
            DepositToShelter();
            if (Save != null) { Save.SaveStash(stash); Save.SaveInventory(bag); }
            Debug.Log("[Ares] Existing backpack contents moved into the shelter stash.");
        }

        /// <summary>Moves the shelter stash into the battle backpack (feedback #10).</summary>
        public void DeployFromShelter()
        {
            var bag = Game.Inventory;
            var stash = Game.Stash;
            if (bag == null || stash == null) return;
            if (bag.TotalItemCount > 0) return;   // already carrying

            for (int i = 0; i < InventoryModel.Capacity; i++)
            {
                var s = stash.Slots[i];
                if (s.IsEmpty) continue;
                var def = Game.Items != null ? Game.Items.Get(s.itemId) : null;

                if (def != null && def.IsWeapon)
                {
                    int free = -1;
                    for (int j = 0; j < InventoryModel.Capacity; j++)
                        if (bag.Slots[j].IsEmpty) { free = j; break; }
                    if (free < 0) continue;
                    bag.Slots[free].itemId = s.itemId;
                    bag.Slots[free].count = 1;
                    bag.Slots[free].durability = s.durability;
                    s.Clear();
                    continue;
                }

                int added = bag.TryAdd(s.itemId, s.count);
                if (added > 0) stash.RemoveCount(s.itemId, added);
            }

            bag.RaiseChanged();
            if (Game.Equipment != null) { Game.Equipment.Refresh(); Game.Equipment.meleeActive = false; }
            if (Save != null) Save.SaveStash(stash);
        }

        /// <summary>Moves one stack between the stash and the backpack (weapons keep durability).</summary>
        public bool TryTransfer(InventoryModel from, int fromIndex, InventoryModel to)
        {
            if (from == null || to == null) return false;
            if (fromIndex < 0 || fromIndex >= InventoryModel.Capacity) return false;

            var s = from.Slots[fromIndex];
            if (s.IsEmpty) return false;

            var def = Game.Items != null ? Game.Items.Get(s.itemId) : null;
            if (def != null && def.IsWeapon)
            {
                int free = -1;
                for (int i = 0; i < InventoryModel.Capacity; i++)
                    if (to.Slots[i].IsEmpty) { free = i; break; }
                if (free < 0) return false;
                to.Slots[free].itemId = s.itemId;
                to.Slots[free].count = 1;
                to.Slots[free].durability = s.durability;
                s.Clear();
            }
            else
            {
                int added = to.TryAdd(s.itemId, s.count);
                if (added <= 0) return false;
                s.count -= added;
                if (s.count <= 0) s.Clear();
            }

            from.RaiseChanged();
            to.RaiseChanged();
            if (Game.Equipment != null) Game.Equipment.Refresh();
            if (Save != null) { Save.SaveInventory(Game.Inventory); Save.SaveStash(Game.Stash); }
            return true;
        }

        /// <summary>Victory: everything carried back goes into the shelter stash.</summary>
        public void DepositToShelter()
        {
            var bag = Game.Inventory;
            var stash = Game.Stash;
            if (bag == null || stash == null) return;

            for (int i = 0; i < InventoryModel.Capacity; i++)
            {
                var s = bag.Slots[i];
                if (s.IsEmpty) continue;
                var def = Game.Items != null ? Game.Items.Get(s.itemId) : null;

                if (def != null && def.IsWeapon)
                {
                    int free = -1;
                    for (int j = 0; j < InventoryModel.Capacity; j++)
                        if (stash.Slots[j].IsEmpty) { free = j; break; }
                    if (free < 0) continue;
                    stash.Slots[free].itemId = s.itemId;
                    stash.Slots[free].count = 1;
                    stash.Slots[free].durability = s.durability;
                    s.Clear();
                    continue;
                }

                int added = stash.TryAdd(s.itemId, s.count);
                if (added > 0) s.count -= added;
                if (s.count <= 0) s.Clear();
            }

            bag.RaiseChanged();
            stash.RaiseChanged();
            if (Game.Equipment != null) Game.Equipment.Refresh();
            if (Save != null) Save.SaveStash(stash);
            Debug.Log("[Ares] Loot deposited into the shelter stash.");
        }

        /// <summary>Defeat or leaving the run early: the carried bag is lost.</summary>
        /// <summary>
        /// Dying costs progress: one or two upgrade lines lose a couple of levels.
        /// The loss per line is capped so a bad run never wipes the whole build.
        /// </summary>
        private void ApplyDeathPenalty()
        {
            var keys = new List<string>();
            var levels = new List<int>();

            for (int i = 0; i < WeaponMods.All.Length; i++)
            {
                int l = WeaponMods.Level(WeaponMods.All[i].id);
                if (l > 0) { keys.Add("m:" + WeaponMods.All[i].id); levels.Add(l); }
            }
            if (Game.Items != null)
            {
                foreach (var def in Game.Items.AllItems)
                {
                    if (def == null) continue;
                    int l = Enhancement.Level(def.id);
                    if (l > 0) { keys.Add("e:" + def.id); levels.Add(l); }
                    int t = Enhancement.TraitLevel(def.id);
                    if (t > 0) { keys.Add("t:" + def.id); levels.Add(t); }
                }
            }
            if (keys.Count == 0) return;

            // never wipe a build: if there is barely anything left, skip the penalty entirely
            int totalLevels = 0;
            for (int i = 0; i < levels.Count; i++) totalLevels += levels[i];
            if (totalLevels <= 2) return;

            int hits = Mathf.Min(keys.Count, Random.Range(1, 3));
            string log = "";
            for (int n = 0; n < hits; n++)
            {
                int pick = Random.Range(0, keys.Count);
                string key = keys[pick];
                int lvl = levels[pick];
                int drop = Mathf.Clamp(Mathf.RoundToInt(lvl * 0.25f), 1, 2);
                if (lvl - drop < 0) continue;

                string id = key.Substring(2);
                if (key[0] == 'm') WeaponMods.SetLevel(id, lvl - drop);
                else if (key[0] == 'e') Enhancement.SetLevel(id, lvl - drop);
                else Enhancement.SetTraitLevel(id, lvl - drop);

                log += (key[0] == 'm' ? WeaponMods.Get(id).name : (Game.Items.Get(id) != null ? Game.Items.Get(id).name : id))
                     + " -" + drop + "级  ";
            }
            Debug.Log("[Ares] Death penalty: " + log);
            if (Hud != null) Hud.ShowPrompt("死亡惩罚：" + log, 2.4f);
        }

        /// <summary>Fills an empty backpack with weapons, ammo and bandages from the stash.</summary>
        public void AutoLoadKit()
        {
            var bag = Game.Inventory;
            var stash = Game.Stash;
            if (bag == null || stash == null) return;

            // weapons and ammo first, then consumables, then whatever is left
            string[] priority = new string[] { "wep_huoqiang", "wep_gong", "wep_dao_jing", "wep_dao",
                                               "ammo_huoyao", "ammo_yumaojian", "tool_bengdai",
                                               "tool_grenade", "tool_fence" };

            for (int pass = 0; pass < 2; pass++)
            {
                for (int p = 0; p < priority.Length; p++)
                {
                    string id = priority[p];
                    if (pass == 0 && (id.StartsWith("mat_"))) continue;
                    for (int i = 0; i < InventoryModel.Capacity; i++)
                    {
                        if (stash.Slots[i].IsEmpty || stash.Slots[i].itemId != id) continue;
                        TryTransfer(stash, i, bag);
                    }
                }
                if (bag.UsedSlots > 0) break;
            }

            if (bag.UsedSlots == 0)
                for (int i = 0; i < InventoryModel.Capacity; i++)
                {
                    if (bag.UsedSlots >= 8) break;
                    if (!stash.Slots[i].IsEmpty) TryTransfer(stash, i, bag);
                }

            if (Save != null) { Save.SaveStash(stash); Save.SaveInventory(bag); }
        }

        public void DiscardBattleBag()
        {
            if (Game.Inventory == null) return;
            Game.Inventory.Clear();
            if (Game.Equipment != null) Game.Equipment.Refresh();
            if (Save != null) { Save.SaveInventory(Game.Inventory); Save.SaveStash(Game.Stash); }
        }

        /// <summary>Closes every overlay so a fast click cannot leave a panel open in a run.</summary>
        public void CloseAllPanels()
        {
            if (Shelter != null) Shelter.SetOpen(false);
            if (Codex != null) Codex.SetOpen(false);
            if (Inventory != null) Inventory.SetOpen(false);
            if (Menu != null) Menu.ShowNone();
            if (Lore != null) Lore.SetOpen(false);
            SetCursor(true);
        }

        private void EnsureAudio()
        {
            if (GetComponent<AudioManager>() == null) gameObject.AddComponent<AudioManager>();
        }

        private void EnsureBarrelRoot()
        {
            if (Game.BarrelRoot != null) return;
            var go = new GameObject("Barrels");
            go.transform.SetParent(transform, false);
            Game.BarrelRoot = go.transform;

            if (Game.EffectRoot == null)
            {
                var fx = new GameObject("Effects");
                fx.transform.SetParent(transform, false);
                Game.EffectRoot = fx.transform;
            }
        }

        public void StartRun(bool stress, int stressCount, int totalWaves)
        {
            // A click can arrive before Boot finished (or twice in one frame). Both used to leave
            // the run half-initialised: player frozen, no HUD, no waves.
            if (!_booted) return;
            if (_startingRun) return;
            if (Time.unscaledTime - _bootAt < 0.25f) return;   // swallow the window-focus click

            _startingRun = true;
            try
            {
                StartRunInternal(stress, stressCount, totalWaves);
            }
            finally
            {
                _startingRun = false;
            }
        }

        private void StartRunInternal(bool stress, int stressCount, int totalWaves)
        {
            _runFrame = Time.frameCount;
            // Play the short intro once per session before the first normal run.
            if (Story != null && !_storyPlayed && !stress)
            {
                _storyPlayed = true;
                if (Menu != null) Menu.ShowNone();
                Story.Play(() => StartRun(stress, stressCount, totalWaves));
                return;
            }

            // If the backpack is empty but the stash has gear, load a sensible kit automatically.
            // (Forcing the player to drag items every run made "开始游戏" look broken.)
            if (!stress && Game.Inventory != null && Game.Inventory.UsedSlots == 0
                && Game.Stash != null && Game.Stash.UsedSlots > 0)
            {
                AutoLoadKit();
            }

            SnapshotLoadout();

            // menu pick wins; otherwise rotate to the next layout before anything spawns
            if (!MapPicked) Map = MapBuilder.Next(Map);
            MapPicked = false;
            MapBuilder.Show(Map, this);
            MapBuilder.Show(Map, this);

            EnsureAudio();
            EnsureBarrelRoot();
            Barrel.ClearAll();
            Fence.ClearAll();

            CloseAllPanels();

            Phase = GamePhase.Playing;
            AudioManager.PlayMusic("music_battle");
            Time.timeScale = 1f;
            _resultScheduled = false;

            Kills = 0;
            Wave = 1;
            StressMode = stress;
            StressCount = stressCount;
            TotalWaves = totalWaves;
            _lastStress = stress;
            _lastStressCount = stressCount;
            _lastTotalWaves = totalWaves;

            if (Spawner != null) Spawner.ClearAll();
            if (Combat != null) Combat.ClearAll();
            if (Items != null) Items.ClearPickups();

            Vector2 start = Arena != null ? Arena.PlayerStart : Vector2.zero;
            if (Player != null) { Player.Spawn(start); Player.SetFrozen(false); }
            if (Camera != null) { Camera.SetSize(Game.Cfg.cameraSize, false); Camera.SnapToTarget(); }

            if (Menu != null) Menu.ShowNone();
            if (Hud != null) Hud.SetVisible(true);
            if (Minimap != null) Minimap.SetVisible(true);
            _watchdogTimer = 0f;
            if (Inventory != null) Inventory.SetOpen(false);
            if (Game.Nav != null) Game.Nav.Recompute(start);

            if (Spawner != null)
            {
                if (stress) Spawner.BeginStress(stressCount);
                else Spawner.BeginWaves(totalWaves);
            }

            if (Hud != null)
                Hud.ShowBanner(stress ? ("压力测试 " + stressCount) : (totalWaves > 0 ? ("生存 " + totalWaves + " 波") : "无尽模式"));
            SetCursor(true);
        }

        /// <summary>Remembers what the run started with so a retry is not an empty-handed run.</summary>
        private void SnapshotLoadout()
        {
            _loadout.Clear();
            var bag = Game.Inventory;
            if (bag == null) return;
            for (int i = 0; i < InventoryModel.Capacity; i++)
            {
                var s = bag.Slots[i];
                if (s.IsEmpty) continue;
                _loadout.Add(new KeyValuePair<string, int>(s.itemId, s.count));
            }
        }

        private void RestoreLoadout()
        {
            var bag = Game.Inventory;
            if (bag == null) return;
            bag.Clear();
            for (int i = 0; i < _loadout.Count; i++)
                bag.TryAdd(_loadout[i].Key, _loadout[i].Value);
            if (bag.UsedSlots == 0) AutoLoadKit();
            if (Game.Equipment != null) Game.Equipment.Refresh();
        }

        /// <summary>Menu hook: rebuild the arena immediately so the choice is visible behind the UI.</summary>
        public void SelectMap(MapVariant v)
        {
            if (MapFx != null && !MapFx.Busy) { MapFx.Switch(v, this); return; }
            Map = v;
            MapBuilder.Show(v, this);
        }

        public void RestartRun()
        {
            RestoreLoadout();
            StartRun(_lastStress, _lastStressCount, _lastTotalWaves);
        }

        public void ReturnToMenu()
        {
            // leaving mid-run without winning forfeits the carried loot
            if (Phase == GamePhase.Playing || Phase == GamePhase.Paused) DiscardBattleBag();
            if (Save != null && Game.Inventory != null) Save.SaveInventory(Game.Inventory);
            if (Save != null && Game.Equipment != null) Save.SaveEquipment(Game.Equipment);
            GoToMenu();
        }

        private void ForceCloseInventory()
        {
            if (Inventory != null && Inventory.IsOpen)
            {
                Inventory.SetOpen(false);
                SetCursor(true);
            }
        }

        public void Pause()
        {
            if (Phase != GamePhase.Playing) return;
            Phase = GamePhase.Paused;
            Time.timeScale = 0f;
            if (Inventory != null) Inventory.SetOpen(false);
            if (Menu != null) Menu.ShowPause();
            SetCursor(false);
        }

        public void Resume()
        {
            if (Phase != GamePhase.Paused) return;
            Phase = GamePhase.Playing;
            Time.timeScale = 1f;
            if (Menu != null) Menu.ShowNone();
            SetCursor(true);
        }

        public void SetCursor(bool locked)
        {
            bool shouldLock = locked && Phase == GamePhase.Playing && (Inventory == null || !Inventory.IsOpen);
            InputService.SetCursorLocked(shouldLock);
        }

        public void HitStop(float duration)
        {
            if (Phase != GamePhase.Playing || duration <= 0f) return;
            if (_hitStop != null) StopCoroutine(_hitStop);
            _hitStop = StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            Time.timeScale = Game.Cfg.hitStopScale;
            yield return new WaitForSecondsRealtime(duration);
            if (Phase == GamePhase.Playing) Time.timeScale = 1f;
            _hitStop = null;
        }

        // ------------------------------------------------------------------ callbacks

        public void OnEnemyKilled(EnemyBase e)
        {
            Kills++;
            Milestones.Bump("kills", 1);
            if (e != null && e.EnemyKind == EnemyKind.Boss) Milestones.Bump("boss", 1);
        }
        public void OnEnemyDied(EnemyBase e) { }
        public void OnWaveChanged(int wave) { Wave = wave; }

        public void OnPlayerDied()
        {
            if (Phase != GamePhase.Playing) return;
            Phase = GamePhase.Defeat;
            ScheduleResult(false);
        }

        public void OnVictory()
        {
            if (Phase != GamePhase.Playing) return;
            CollectLeftovers();
            AudioManager.PlaySfx("sfx_victory", 0.9f, 0f);
            Phase = GamePhase.Victory;
            ScheduleResult(true);
        }

        /// <summary>Cleared the run with loot still on the floor: it goes straight into the stash.</summary>
        private void CollectLeftovers()
        {
            var items = Game.Items;
            var stash = Game.Stash;
            if (items == null || stash == null) return;

            var drops = items.Pickups;
            if (drops == null || drops.Count == 0) return;

            int moved = 0;
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var p = drops[i];
                if (p == null) continue;
                var def = p.Def;
                if (def == null) continue;
                if (stash.TryAdd(def.id, p.Count) > 0) { moved++; items.RemovePickup(p); }
            }
            if (moved > 0)
            {
                Debug.Log("[Ares] Victory: swept " + moved + " drops into the stash");
                if (Hud != null) Hud.ShowPrompt("剩余物资已收入庇护所仓库 x" + moved, 2.2f);
                if (Save != null) Save.SaveStash(stash);
            }
        }

        private void ScheduleResult(bool victory)
        {
            if (_resultScheduled) return;
            _resultScheduled = true;
            StartCoroutine(ResultRoutine(victory));
        }

        private IEnumerator ResultRoutine(bool victory)
        {
            yield return new WaitForSecondsRealtime(1.1f);
            AudioManager.PlayMusic(victory ? "music_victory" : "music_defeat", 0.6f);
            if (victory)
            {
                DepositToShelter();
                if (Save != null)
                {
                    Save.SetInt("clear_wave", Mathf.Max(Save.GetInt("clear_wave", 0), Game.Spawner != null ? Game.Spawner.Wave : 1));
                    Save.SetInt("clear_reward", 1);   // a reward is waiting at the merchant
                }
            }
            else { DiscardBattleBag(); ApplyDeathPenalty(); }
            if (Save != null && Game.Inventory != null) Save.SaveInventory(Game.Inventory);
            if (Save != null && Game.Equipment != null) Save.SaveEquipment(Game.Equipment);
            if (Menu != null) Menu.ShowResult(victory, Kills, Wave, Game.Inventory != null ? Game.Inventory.TotalItemCount : 0);
            if (Player != null) Player.SetFrozen(true);
            Phase = victory ? GamePhase.Victory : GamePhase.Defeat;
            SetCursor(false);
        }

        public void QuitGame()
        {
            if (Save != null && Game.Inventory != null) Save.SaveInventory(Game.Inventory);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public bool IsInsideArena(Vector2 p) { return Arena == null || Arena.IsInside(p); }

        // ------------------------------------------------------------------ input routing

        private void Update()
        {
            // Safety net: gameplay must never run at a leaked slow-motion time scale.
            bool inventoryOpen = Inventory != null && Inventory.IsOpen;
            if (Phase == GamePhase.Playing && _hitStop == null && !inventoryOpen && !Mathf.Approximately(Time.timeScale, 1f))
                Time.timeScale = 1f;

            // Self-healing: if a run somehow ends up without HUD, waves or a movable player,
            // repair it instead of leaving the player stuck in an empty arena.
            if (Phase == GamePhase.Playing)
            {
                _watchdogTimer += Time.unscaledDeltaTime;
                if (_watchdogTimer > 0.5f)
                {
                    _watchdogTimer = 0f;
                    if (Hud != null && !Hud.IsVisible) Hud.SetVisible(true);
                    if (Minimap != null) Minimap.SetVisible(true);
                    if (Game.Player != null && Game.Player.IsFrozen) Game.Player.SetFrozen(false);
                    if (!InputService.CursorLocked) SetCursor(true);
                    if (Spawner != null && !Spawner.IsRunning && !_resultScheduled)
                    {
                        Debug.Log("[Ares] Watchdog: restarting waves");
                        if (StressMode) Spawner.BeginStress(StressCount);
                        else Spawner.BeginWaves(TotalWaves);
                    }
                }
            }

            switch (Phase)
            {
                case GamePhase.Playing:
                    if (InputService.PausePressed) { Pause(); break; }
                    if (InputService.InventoryPressed)
                    {
                        if (Inventory != null)
                        {
                            Inventory.Toggle();
                            if (Inventory.IsOpen)
                            {
                                SetCursor(false);
                                Time.timeScale = 0f;      // browse and upgrade without taking hits
                            }
                            else
                            {
                                SetCursor(true);
                                Time.timeScale = 1f;
                            }
                        }
                        break;
                    }
                    if (Game.UiBlocking) break;
                    if (InputService.PickupPressed) TryPickup();
                    if (Input.GetKeyDown(KeyCode.F1)) StartRun(true, 100, 0);
                    else if (Input.GetKeyDown(KeyCode.F2)) StartRun(true, 500, 0);
                    else if (Input.GetKeyDown(KeyCode.F3)) StartRun(true, 1000, 0);
                    break;

                case GamePhase.Paused:
                    if (InputService.PausePressed) Resume();
                    break;

                case GamePhase.MainMenu:
                    if (Lore != null && Lore.IsOpen && InputService.PausePressed) { Lore.SetOpen(false); break; }
                    if (Codex != null && Codex.IsOpen && InputService.PausePressed) Codex.SetOpen(false);
                    else if (Shelter != null && Shelter.IsOpen && InputService.PausePressed) Shelter.SetOpen(false);
                    break;
            }
        }

        private void TryPickup()
        {
            AudioManager.PlaySfx("sfx_pickup", 0.7f, 0.08f);
            DoPickup();
        }

        private void DoPickup()
        {
            var items = Game.Items;
            var inv = Game.Inventory;
            var player = Game.Player;
            if (items == null || inv == null || player == null) return;

            var cfg = Game.Cfg;
            var near = items.NearestPickup(player.Position, cfg.pickupRadius, inv);
            if (near != null)
            {
                int added = inv.TryAdd(near.ItemId, near.Count);
                if (added > 0)
                {
                    near.Consume(added);
                    if (Hud != null) Hud.ShowPrompt("+ " + added + "  " + near.Def.name, 1.1f);
                    if (Game.Equipment != null) Game.Equipment.Refresh();
                    return;
                }
            }

            var any = items.NearestPickupAny(player.Position, cfg.pickupRadius);
            if (any != null && Hud != null) Hud.ShowPrompt("背包已满，无法拾取 " + any.Def.name, 1.4f);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && Phase == GamePhase.Playing) Pause();
        }
    }
}
