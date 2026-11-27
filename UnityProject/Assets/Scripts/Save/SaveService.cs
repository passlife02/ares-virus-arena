using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Persists the backpack (and equipped weapon slots) to a real SQLite file so loot survives
    /// a full client restart.
    ///
    ///   CREATE TABLE inventory (slot INTEGER PRIMARY KEY, item_id TEXT NOT NULL,
    ///                           count INTEGER NOT NULL, durability INTEGER NOT NULL);
    ///   CREATE TABLE meta      (key TEXT PRIMARY KEY, value TEXT);
    ///
    /// If the native engine is unavailable the same data is written to JSON instead.
    /// </summary>
    public class SaveService : MonoBehaviour
    {
        public bool UseSqlite = true;

        private SqliteDb _db;
        private string _dbPath;
        private string _fallbackPath;
        private bool _sqliteOk;
        private InventoryModel _bound;

        public string DatabasePath { get { return _dbPath; } }
        public bool SqliteActive { get { return _sqliteOk; } }
        public int SaveCount { get; private set; }

        public void Init()
        {
            string dir = Path.Combine(Application.persistentDataPath, "AresSave");
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); }
            catch (Exception e) { Debug.LogError("[Ares] Cannot create save dir: " + e.Message); }

            _dbPath = Path.Combine(dir, "ares_save.db");
            _fallbackPath = Path.Combine(dir, "ares_save_fallback.json");

            if (UseSqlite)
            {
                try
                {
                    _db = new SqliteDb();
                    if (_db.Open(_dbPath))
                    {
                        int rc1 = _db.Exec("CREATE TABLE IF NOT EXISTS inventory (slot INTEGER PRIMARY KEY, item_id TEXT NOT NULL, count INTEGER NOT NULL, durability INTEGER NOT NULL DEFAULT 0);");
                        int rc2 = _db.Exec("CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT);");
                        _db.Exec("CREATE TABLE IF NOT EXISTS stash (slot INTEGER PRIMARY KEY, item_id TEXT NOT NULL, count INTEGER NOT NULL, durability INTEGER NOT NULL DEFAULT 0);");
                        _sqliteOk = rc1 == 0 && rc2 == 0;
                        if (!_sqliteOk) Debug.LogWarning("[Ares] SQLite schema failed: " + _db.LastError);
                    }
                    else Debug.LogWarning("[Ares] SQLite open failed, falling back to JSON.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Ares] SQLite unavailable (" + e.GetType().Name + "), using JSON fallback.");
                    _sqliteOk = false;
                }
            }

            Debug.Log("[Ares] Save backend = " + (_sqliteOk ? "SQLite (" + _dbPath + ")" : "JSON (" + _fallbackPath + ")"));
        }

        public void Bind(InventoryModel model)
        {
            if (_bound != null) _bound.Changed -= OnInventoryChanged;
            _bound = model;
            if (_bound != null) _bound.Changed += OnInventoryChanged;
        }

        private void OnInventoryChanged()
        {
            SaveInventory(_bound);
        }

        public void SaveInventory(InventoryModel model)
        {
            if (model == null) return;
            if (_sqliteOk) SaveSqlite(model);
            else SaveJson(model);
            SaveCount++;
        }

        public void LoadInventory(InventoryModel model)
        {
            if (model == null) return;
            List<InventorySlot> data = _sqliteOk ? LoadSqlite() : LoadJson();
            model.LoadFrom(data);
            Debug.Log("[Ares] Inventory loaded: " + model.UsedSlots + " slots, " + model.TotalItemCount + " items.");
        }

        public bool HasSave()
        {
            if (_sqliteOk)
            {
                using (var st = _db.Prepare("SELECT COUNT(*) FROM inventory;"))
                    if (st != null && st.Step()) return st.GetInt(0) > 0;
                return false;
            }
            return File.Exists(_fallbackPath);
        }

        public void SetString(string key, string value)
        {
            if (!_sqliteOk) return;
            using (var st = _db.Prepare("INSERT OR REPLACE INTO meta(key, value) VALUES(?1, ?2);"))
            {
                if (st == null) return;
                st.BindText(1, key);
                st.BindText(2, value);
                st.Step();
            }
        }

        public string GetString(string key)
        {
            if (!_sqliteOk) return null;
            using (var st = _db.Prepare("SELECT value FROM meta WHERE key = ?1;"))
            {
                if (st == null) return null;
                st.BindText(1, key);
                if (st.Step()) return st.GetText(0);
            }
            return null;
        }

        public void SetInt(string key, int value) { SetString(key, value.ToString()); }

        public int GetInt(string key, int fallback)
        {
            string s = GetString(key);
            int v;
            return (s != null && int.TryParse(s, out v)) ? v : fallback;
        }

        public void SaveEquipment(EquipmentModel eq)
        {
            if (eq == null) return;
            SetInt("equip.melee", eq.meleeSlot);
            SetInt("equip.ranged", eq.rangedSlot);
            SetInt("equip.active", eq.meleeActive ? 1 : 0);
        }

        public void LoadEquipment(EquipmentModel eq)
        {
            if (eq == null) return;
            eq.meleeSlot = GetInt("equip.melee", -1);
            eq.rangedSlot = GetInt("equip.ranged", -1);
            eq.meleeActive = GetInt("equip.active", 0) == 1;
            eq.Refresh();
        }

        public void WipeInventory(InventoryModel model)
        {
            if (_sqliteOk) _db.Exec("DELETE FROM inventory;");
            else if (File.Exists(_fallbackPath)) File.Delete(_fallbackPath);
            if (model != null) model.Clear();
        }

        // ---------------------------------------------------------------- sqlite

        /// <summary>Shelter stash: same layout as the backpack, different table.</summary>
        public void SaveStash(InventoryModel stash) { if (_sqliteOk) SaveSqliteTo(stash, "stash"); else SaveJson(stash); }

        public void LoadStash(InventoryModel stash)
        {
            if (stash == null) return;
            List<InventorySlot> data = _sqliteOk ? LoadSqliteFrom("stash") : LoadJson();
            stash.LoadFrom(data);
        }

        private void SaveSqlite(InventoryModel model) { SaveSqliteTo(model, "inventory"); }

        private void SaveSqliteTo(InventoryModel model, string table)
        {
            try
            {
                _db.Exec("BEGIN TRANSACTION;");
                _db.Exec("DELETE FROM " + table + ";");
                using (var st = _db.Prepare("INSERT INTO " + table + "(slot, item_id, count, durability) VALUES(?1, ?2, ?3, ?4);"))
                {
                    if (st == null) return;
                    for (int i = 0; i < InventoryModel.Capacity; i++)
                    {
                        var s = model.Slots[i];
                        if (s.IsEmpty) continue;
                        st.Reset();
                        st.BindInt(1, i);
                        st.BindText(2, s.itemId);
                        st.BindInt(3, s.count);
                        st.BindInt(4, s.durability);
                        st.Step();
                    }
                }
                _db.Exec("COMMIT;");
            }
            catch (Exception e)
            {
                Debug.LogError("[Ares] SQLite save failed: " + e.Message);
                SaveJson(model);
            }
        }

        private List<InventorySlot> LoadSqlite() { return LoadSqliteFrom("inventory"); }

        private List<InventorySlot> LoadSqliteFrom(string table)
        {
            var list = new List<InventorySlot>(InventoryModel.Capacity);
            for (int i = 0; i < InventoryModel.Capacity; i++) list.Add(new InventorySlot());
            try
            {
                using (var st = _db.Prepare("SELECT slot, item_id, count, durability FROM " + table + " ORDER BY slot;"))
                {
                    if (st == null) return list;
                    while (st.Step())
                    {
                        int slot = st.GetInt(0);
                        if (slot < 0 || slot >= InventoryModel.Capacity) continue;
                        if (st.IsNull(1)) continue;
                        list[slot].itemId = st.GetText(1);
                        list[slot].count = st.GetInt(2);
                        list[slot].durability = st.GetInt(3);
                    }
                }
            }
            catch (Exception e) { Debug.LogError("[Ares] SQLite load failed: " + e.Message); }
            return list;
        }

        // ---------------------------------------------------------------- json fallback

        [Serializable]
        private class JsonSlot
        {
            public int slot;
            public string itemId;
            public int count;
            public int durability;
        }

        [Serializable]
        private class JsonBag
        {
            public List<JsonSlot> slots = new List<JsonSlot>();
        }

        private void SaveJson(InventoryModel model)
        {
            try
            {
                var bag = new JsonBag();
                for (int i = 0; i < InventoryModel.Capacity; i++)
                {
                    var s = model.Slots[i];
                    if (s.IsEmpty) continue;
                    bag.slots.Add(new JsonSlot { slot = i, itemId = s.itemId, count = s.count, durability = s.durability });
                }
                File.WriteAllText(_fallbackPath, JsonUtility.ToJson(bag, true));
            }
            catch (Exception e) { Debug.LogError("[Ares] JSON save failed: " + e.Message); }
        }

        private List<InventorySlot> LoadJson()
        {
            var list = new List<InventorySlot>(InventoryModel.Capacity);
            for (int i = 0; i < InventoryModel.Capacity; i++) list.Add(new InventorySlot());
            try
            {
                if (!File.Exists(_fallbackPath)) return list;
                var bag = JsonUtility.FromJson<JsonBag>(File.ReadAllText(_fallbackPath));
                if (bag == null || bag.slots == null) return list;
                for (int i = 0; i < bag.slots.Count; i++)
                {
                    var js = bag.slots[i];
                    if (js == null || js.slot < 0 || js.slot >= InventoryModel.Capacity) continue;
                    list[js.slot].itemId = js.itemId;
                    list[js.slot].count = js.count;
                    list[js.slot].durability = js.durability;
                }
            }
            catch (Exception e) { Debug.LogError("[Ares] JSON load failed: " + e.Message); }
            return list;
        }

        private void OnDestroy()
        {
            if (_bound != null) _bound.Changed -= OnInventoryChanged;
            if (_db != null) _db.Dispose();
        }

        private void OnApplicationQuit()
        {
            if (_bound != null) SaveInventory(_bound);
            if (_db != null) _db.Dispose();
        }
    }
}
