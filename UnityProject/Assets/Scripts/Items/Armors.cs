using UnityEngine;

namespace Ares
{
    /// <summary>Equipment definition (feedback #12): crafted and upgraded at the blacksmith.</summary>
    public class ArmorDef
    {
        public string id;
        public string name;
        public string icon;
        public string desc;
        public int defensePercent;     // damage reduction at level 0
        public int durabilityMax;
        public string costA;
        public int costACount;
        public string costB;
        public int costBCount;
    }

    /// <summary>
    /// Three armour slots with their own durability and upgrade level. Worn pieces soak part of
    /// every hit and lose durability; broken pieces stop protecting until repaired.
    /// </summary>
    public static class Armors
    {
        public const int MaxLevel = 10;

        public static readonly ArmorDef[] All = new ArmorDef[]
        {
            new ArmorDef { id = "armor_head",  name = "防暴头盔", icon = "UI/icon_helmet",
                desc = "从废车场翻出来的防暴头盔，能挡住大部分碎屑。",
                defensePercent = 6, durabilityMax = 60,
                costA = "mat_shengtie", costACount = 6, costB = "mat_jiaodai", costBCount = 3 },
            new ArmorDef { id = "armor_chest", name = "拼装护甲", icon = "UI/icon_chest",
                desc = "钢板和皮带拼起来的上身护甲，是庇护所里最值钱的东西。",
                defensePercent = 12, durabilityMax = 90,
                costA = "mat_shengtie", costACount = 12, costB = "mat_masheng", costBCount = 4 },
            new ArmorDef { id = "armor_feet",  name = "加固靴",   icon = "UI/icon_boots",
                desc = "鞋底加了钢片，踩过碎玻璃和骨刺都没事。",
                defensePercent = 6, durabilityMax = 50,
                costA = "mat_shengtie", costACount = 4, costB = "mat_mutou", costBCount = 6 },
        };

        public static ArmorDef Get(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].id == id) return All[i];
            return null;
        }

        private static string Key(string id) { return "armor_" + id; }

        // ---- state -------------------------------------------------------------

        public static bool Owned(string id)
        {
            if (Game.Save == null) return false;
            return !string.IsNullOrEmpty(Game.Save.GetString(Key(id)));
        }

        public static int Level(string id)
        {
            if (!Owned(id)) return 0;
            return Mathf.Clamp(Parse(Game.Save.GetString(Key(id)), 0), 0, MaxLevel);
        }

        public static int Durability(string id)
        {
            if (!Owned(id)) return 0;
            return Mathf.Max(0, Parse(Game.Save.GetString(Key(id)), 1));
        }

        private static int Parse(string raw, int index)
        {
            if (string.IsNullOrEmpty(raw)) return 0;
            string[] parts = raw.Split('|');
            if (index >= parts.Length) return 0;
            int v;
            return int.TryParse(parts[index], out v) ? v : 0;
        }

        private static void SaveState(string id, int level, int durability)
        {
            if (Game.Save == null) return;
            Game.Save.SetString(Key(id), level + "|" + durability);
        }

        public static void Craft(string id)
        {
            var def = Get(id);
            if (def == null) return;
            SaveState(id, 0, def.durabilityMax);
        }

        public static void Upgrade(string id)
        {
            int lvl = Level(id);
            if (lvl >= MaxLevel) return;
            SaveState(id, lvl + 1, Mathf.Min(DurabilityMax(id), Durability(id) + 10));
        }

        public static int DurabilityMax(string id)
        {
            var def = Get(id);
            if (def == null) return 0;
            return def.durabilityMax + Level(id) * 8;
        }

        public static int DefensePercent(string id)
        {
            var def = Get(id);
            if (def == null || !Owned(id)) return 0;
            if (Durability(id) <= 0) return 0;              // broken gear gives nothing
            int bonus = Level(id) * 2;
            int max = def.defensePercent * 2;               // hard cap per piece
            return Mathf.Min(def.defensePercent + bonus, max);
        }

        /// <summary>Total damage reduction (0..0.45).</summary>
        public static float Defense01()
        {
            int sum = 0;
            for (int i = 0; i < All.Length; i++) sum += DefensePercent(All[i].id);
            return Mathf.Clamp(sum / 100f, 0f, 0.45f);
        }

        /// <summary>Wears one random worn piece by a point. Returns the piece that broke, if any.</summary>
        public static ArmorDef Wear(int amount)
        {
            int total = 0;
            for (int i = 0; i < All.Length; i++)
                if (Owned(All[i].id) && Durability(All[i].id) > 0) total++;
            if (total == 0) return null;

            int pick = Random.Range(0, total);
            for (int i = 0; i < All.Length; i++)
            {
                var def = All[i];
                if (!Owned(def.id) || Durability(def.id) <= 0) continue;
                if (pick-- > 0) continue;

                int d = Durability(def.id) - amount;
                if (d <= 0)
                {
                    SaveState(def.id, Level(def.id), 0);
                    return def;
                }
                SaveState(def.id, Level(def.id), d);
                return null;
            }
            return null;
        }

        /// <summary>Repairs one piece with materials.</summary>
        public static void Repair(string id)
        {
            SaveState(id, Level(id), DurabilityMax(id));
        }
    }
}
