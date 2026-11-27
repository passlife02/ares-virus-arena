using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>Weapon modification (feedback #6 / #9): a global, levelled upgrade line.</summary>
    public class WeaponMod
    {
        public string id;
        public string name;
        public string desc;
        public bool melee;
        public string costA;
        public int costACount;
        public string costB;
        public int costBCount;
        public string unlockKey;   // milestone that unlocks level 1 ("" = always available)
        public float perLevel;     // magnitude added by each level
        public string perLevelLabel;
    }

    /// <summary>
    /// Mods are no longer installed on one specific weapon: the melee line and the ranged line
    /// are independent and each mod levels from 1 to 10 (feedback #9 items 3 and 5).
    /// </summary>
    public static class WeaponMods
    {
        public const int MaxLevel = 10;

        public static readonly WeaponMod[] All = new WeaponMod[]
        {
            new WeaponMod { id = "long",   name = "长刃",   melee = true,  desc = "近战攻击距离",
                costA = "mat_shengtie", costACount = 4, costB = "mat_jiaodai", costBCount = 2, unlockKey = "kill100",
                perLevel = 0.10f, perLevelLabel = "距离 +0.10" },
            new WeaponMod { id = "heavy",  name = "重刃",   melee = true,  desc = "近战伤害",
                costA = "mat_shengtie", costACount = 6, costB = "mat_mutou", costBCount = 6, unlockKey = "kill100",
                perLevel = 0.06f, perLevelLabel = "伤害 +6%" },
            new WeaponMod { id = "serra",  name = "锯齿",   melee = true,  desc = "近战暴击率",
                costA = "mat_shengtie", costACount = 5, costB = "mat_yumao", costBCount = 4, unlockKey = "wave10",
                perLevel = 0.03f, perLevelLabel = "暴击 +3%" },
            new WeaponMod { id = "spread", name = "散射弹", melee = false, desc = "远程弹丸数量",
                costA = "ammo_huoyao", costACount = 30, costB = "mat_shengtie", costBCount = 4, unlockKey = "wave10",
                perLevel = 1f, perLevelLabel = "每 4 级 +1 发" },
            new WeaponMod { id = "burst",  name = "爆裂弹", melee = false, desc = "命中爆炸",
                costA = "ammo_huoyao", costACount = 40, costB = "mat_shengtie", costBCount = 8, unlockKey = "boss3",
                perLevel = 0.05f, perLevelLabel = "爆炸伤害 +5%" },
            new WeaponMod { id = "pierce", name = "穿透弹", melee = false, desc = "弹丸穿透",
                costA = "ammo_yumaojian", costACount = 30, costB = "mat_shengtie", costBCount = 6, unlockKey = "kill500",
                perLevel = 0.5f, perLevelLabel = "每 2 级 +1 穿透" },
        };

        private static readonly Dictionary<string, int> _cache = new Dictionary<string, int>();

        public static WeaponMod Get(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].id == id) return All[i];
            return null;
        }

        public static int Level(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            int lvl;
            if (_cache.TryGetValue(id, out lvl)) return lvl;
            lvl = Game.Save != null ? Game.Save.GetInt("wmod_" + id, 0) : 0;
            _cache[id] = lvl;
            return lvl;
        }

        public static void SetLevel(string id, int level)
        {
            if (string.IsNullOrEmpty(id)) return;
            level = Mathf.Clamp(level, 0, MaxLevel);
            _cache[id] = level;
            if (Game.Save != null) Game.Save.SetInt("wmod_" + id, level);
        }

        /// <summary>Cost of the next level: it grows quickly so each rank feels different.</summary>
        public static int CostA(WeaponMod m) { int l = Level(m.id); return m.costACount * (1 + l); }
        public static int CostB(WeaponMod m) { int l = Level(m.id); return m.costBCount * (1 + l) / 2 + 1; }

        // ---- aggregated effects (global, not per weapon) ------------------------

        public static float RangeBonus() { return Level("long") * 0.10f; }
        public static float DamageMul() { return 1f + Level("heavy") * 0.06f; }
        public static float CritBonus() { return Level("serra") * 0.03f; }
        public static bool Explosive() { return Level("burst") > 0; }
        public static float ExplodeRadius() { return 1.5f + Level("burst") * 0.12f; }
        public static float ExplodeMul() { return 0.45f + Level("burst") * 0.05f; }
        public static int Pierce() { return Mathf.FloorToInt(Level("pierce") * 0.5f); }
        public static bool Spread() { return Level("spread") > 0; }
        public static int Pellets() { return Level("spread") <= 0 ? 1 : 2 + Mathf.CeilToInt(Level("spread") / 4f); }

        public static void ClearCache() { _cache.Clear(); }
    }

    /// <summary>Merchant blessing: a temporary buff bought before a run (feedback #6 item 2).</summary>
    public static class Blessing
    {
        public struct Def
        {
            public string id;
            public string name;
            public string desc;
            public float damageMul;
            public int maxHpBonus;
            public float speedMul;
            public float critBonus;
        }

        public static readonly Def[] All = new Def[]
        {
            new Def { id = "fury",  name = "战意",  desc = "本局伤害 +18%",     damageMul = 1.18f, speedMul = 1f,    maxHpBonus = 0,  critBonus = 0f },
            new Def { id = "vigor", name = "坚韧",  desc = "本局生命上限 +25",  damageMul = 1f,    speedMul = 1f,    maxHpBonus = 25, critBonus = 0f },
            new Def { id = "swift", name = "疾行",  desc = "本局移动速度 +12%", damageMul = 1f,    speedMul = 1.12f, maxHpBonus = 0,  critBonus = 0f },
            new Def { id = "keen",  name = "锐目",  desc = "本局暴击率 +12%",   damageMul = 1f,    speedMul = 1f,    maxHpBonus = 0,  critBonus = 0.12f },
        };

        public static Def Current()
        {
            string id = Game.Save != null ? Game.Save.GetString("blessing") : "";
            for (int i = 0; i < All.Length; i++) if (All[i].id == id) return All[i];
            return default(Def);
        }

        public static bool Active { get { return !string.IsNullOrEmpty(Current().id); } }

        public static void Set(string id)
        {
            if (Game.Save != null) Game.Save.SetString("blessing", id ?? "");
        }

        public static void Clear() { Set(""); }
    }

    /// <summary>Milestones that gate the stronger mods and shop stock (feedback #6 item 3).</summary>
    public static class Milestones
    {
        public struct Def
        {
            public string key;
            public string name;
            public string desc;
            public string stat;    // kills / wave / boss / loot
            public int target;
        }

        public static readonly Def[] All = new Def[]
        {
            new Def { key = "kill100",  name = "清道夫",   desc = "累计击杀 30 只感染者",    stat = "kills", target = 30 },
            new Def { key = "wave10",   name = "守夜人",   desc = "单局抵达第 5 波",         stat = "wave",  target = 5 },
            new Def { key = "boss3",    name = "屠兽者",   desc = "累计击杀 1 只变异体 BOSS", stat = "boss",  target = 1 },
            new Def { key = "kill500",  name = "幸存者",   desc = "累计击杀 120 只感染者",   stat = "kills", target = 120 },
            new Def { key = "wave15",   name = "老兵",     desc = "单局抵达第 15 波",        stat = "wave",  target = 15 },
            new Def { key = "kill300",  name = "猎手",     desc = "累计击杀 300 只感染者",   stat = "kills", target = 300 },
            new Def { key = "boss5",    name = "猎兽大师", desc = "累计击杀 5 只变异体 BOSS", stat = "boss",  target = 5 },
            new Def { key = "wave30",   name = "守夜人·终", desc = "单局抵达第 30 波",       stat = "wave",  target = 30 },
            new Def { key = "swing200", name = "刀客",     desc = "累计挥砍 200 次",          stat = "swing", target = 200 },
            new Def { key = "charge50", name = "蓄势",     desc = "完成 50 次蓄力重击",       stat = "charge", target = 50 },
            new Def { key = "shot300",  name = "枪手",     desc = "累计射击 300 次",          stat = "shot",  target = 300 },
            new Def { key = "bow80",    name = "弓手",     desc = "用猎弓射击 80 次",         stat = "bow",   target = 80 },
        };

        public static int Value(string stat)
        {
            if (Game.Save == null) return 0;
            return Game.Save.GetInt("ms_" + stat, 0);
        }

        public static void Bump(string stat, int amount)
        {
            if (Game.Save == null || string.IsNullOrEmpty(stat) || amount <= 0) return;
            Game.Save.SetInt("ms_" + stat, Value(stat) + amount);
        }

        /// <summary>Raises a cumulative counter to at least this value (for "reached wave N").</summary>
        public static void AtLeast(string stat, int amount)
        {
            if (Game.Save == null) return;
            if (Value(stat) < amount) Game.Save.SetInt("ms_" + stat, amount);
        }

        public static bool Unlocked(string key)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].key == key) return Value(All[i].stat) >= All[i].target;
            return true;   // no gate
        }

        public static Def ByKey(string key)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].key == key) return All[i];
            return default(Def);
        }

        public static float Progress01(string key)
        {
            var d = ByKey(key);
            if (string.IsNullOrEmpty(d.key)) return 1;
            return Mathf.Clamp01(Value(d.stat) / (float)d.target);
        }
    }
}
