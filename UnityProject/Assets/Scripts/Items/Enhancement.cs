using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Weapon enhancement levels (feedback #5 item 4/5). Stored in the save's meta table so a
    /// reinforced weapon keeps its extra damage between runs.
    /// </summary>
    public static class Enhancement
    {
        public const int MaxLevel = 10;
        public const int DamagePerLevel = 6;

        private static readonly Dictionary<string, int> _cache = new Dictionary<string, int>();

        public static int Level(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int lvl;
            if (_cache.TryGetValue(itemId, out lvl)) return lvl;
            lvl = Game.Save != null ? Game.Save.GetInt("enh_" + itemId, 0) : 0;
            _cache[itemId] = lvl;
            return lvl;
        }

        public static void SetLevel(string itemId, int level)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            level = Mathf.Clamp(level, 0, MaxLevel);
            _cache[itemId] = level;
            if (Game.Save != null) Game.Save.SetInt("enh_" + itemId, level);
        }

        public static int Bonus(string itemId) { return Level(itemId) * DamagePerLevel; }

        // Escalating costs: the next level always asks for more than the last one.
        public static int DamageIronCost(int currentLevel) { return 3 + currentLevel * 2; }
        public static int DamagePowderCost(int currentLevel) { return 1 + currentLevel; }
        public static int TraitIronCost(int currentLevel) { return 3 + currentLevel * 2; }
        public static int TraitWoodCost(int currentLevel) { return 4 + currentLevel * 3; }

        // ---- secondary trait: melee reach / ranged piercing -------------------

        public const int MaxTrait = 10;
        public const float ReachPerTrait = 0.12f;

        private static readonly Dictionary<string, int> _traitCache = new Dictionary<string, int>();

        public static int TraitLevel(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            int lvl;
            if (_traitCache.TryGetValue(itemId, out lvl)) return lvl;
            lvl = Game.Save != null ? Game.Save.GetInt("ent_" + itemId, 0) : 0;
            _traitCache[itemId] = lvl;
            return lvl;
        }

        public static void SetTraitLevel(string itemId, int level)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            level = Mathf.Clamp(level, 0, MaxTrait);
            _traitCache[itemId] = level;
            if (Game.Save != null) Game.Save.SetInt("ent_" + itemId, level);
        }

        /// <summary>Melee weapons gain reach; ranged weapons gain piercing.</summary>
        public static float ReachBonus(string itemId)
        {
            return TraitLevel(itemId) * ReachPerTrait;
        }

        public static int PierceBonus(string itemId)
        {
            int t = TraitLevel(itemId);
            return (t >= 2 ? 1 : 0) + (t >= 3 ? 1 : 0);
        }

        public static int UpgradedDamage(ItemDef def)
        {
            if (def == null) return 0;
            return def.damage + Bonus(def.id);
        }

        public static void ClearCache() { _cache.Clear(); }
    }
}
