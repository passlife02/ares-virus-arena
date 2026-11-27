using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace Ares.EditorTools
{
    /// <summary>
    /// Converts the designer workbook (Assets/Config/GameConfig.xlsx) into the JSON the game
    /// loads at runtime: items (with weapon stats), drops (incl. boss), recipes, and it pushes
    /// the Balance sheet back onto the GameConfig asset.
    /// </summary>
    public static class ExcelConfigImporter
    {
        public const string XlsxPath = "Assets/Config/GameConfig.xlsx";
        public const string OutDir = "Assets/Resources/Config";

        [MenuItem("Tools/Ares/Import Excel Config", false, 20)]
        public static void Import()
        {
            if (!File.Exists(XlsxPath))
            {
                Debug.LogError("[Ares] Missing workbook: " + XlsxPath);
                return;
            }
            var sheets = XlsxReader.ReadWorkbook(XlsxPath);
            if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);

            int items = ImportItems(sheets);
            int drops = ImportDrops(sheets);
            int recipes = ImportRecipes(sheets);
            int balance = ApplyBalance(sheets);

            AssetDatabase.Refresh();
            Debug.Log(string.Format("[Ares] Excel imported: items={0} drops={1} recipes={2} balanceFields={3}", items, drops, recipes, balance));
        }

        // ---------------------------------------------------------------- helpers

        private static Dictionary<string, int> Header(List<List<string>> rows, int headerRow)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (headerRow < 0 || headerRow >= rows.Count) return map;
            var head = rows[headerRow];
            for (int i = 0; i < head.Count; i++)
            {
                string key = (head[i] ?? "").Trim();
                if (key.Length > 0 && !map.ContainsKey(key)) map[key] = i;
            }
            return map;
        }

        private static int FindHeader(List<List<string>> rows, string firstKey)
        {
            int limit = Mathf.Min(rows.Count, 12);
            for (int r = 0; r < limit; r++)
            {
                var row = rows[r];
                if (row == null || row.Count == 0) continue;
                if (string.Equals((row[0] ?? "").Trim(), firstKey, StringComparison.OrdinalIgnoreCase)) return r;
            }
            return 0;
        }

        private static string Get(List<string> row, Dictionary<string, int> h, string key, string fallback = "")
        {
            int i;
            if (!h.TryGetValue(key, out i)) return fallback;
            if (i < 0 || i >= row.Count) return fallback;
            return (row[i] ?? "").Trim();
        }

        private static float GetF(List<string> row, Dictionary<string, int> h, string key, float fallback)
        {
            float v;
            if (float.TryParse(Get(row, h, key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) return v;
            return fallback;
        }

        private static int GetI(List<string> row, Dictionary<string, int> h, string key, int fallback)
        {
            int v;
            if (int.TryParse(Get(row, h, key), out v)) return v;
            float f;
            if (float.TryParse(Get(row, h, key), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) return Mathf.RoundToInt(f);
            return fallback;
        }

        private static List<List<string>> Sheet(Dictionary<string, List<List<string>>> sheets, string name)
        {
            List<List<string>> rows;
            if (sheets.TryGetValue(name, out rows)) return rows;
            Debug.LogWarning("[Ares] Sheet missing: " + name);
            return new List<List<string>>();
        }

        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
        }

        private static string Num(float v)
        {
            return v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- sheets

        private static int ImportItems(Dictionary<string, List<List<string>>> sheets)
        {
            var rows = Sheet(sheets, "Items");
            int hdr = FindHeader(rows, "id");
            var h = Header(rows, hdr);
            var sb = new StringBuilder();
            sb.Append("{\n  \"items\": [\n");
            int count = 0;

            for (int r = hdr + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string id = Get(row, h, "id");
                if (string.IsNullOrEmpty(id)) continue;

                if (count > 0) sb.Append(",\n");
                sb.Append("    {");
                sb.Append("\"id\":\"").Append(Esc(id)).Append("\",");
                sb.Append("\"name\":\"").Append(Esc(Get(row, h, "name", id))).Append("\",");
                sb.Append("\"icon\":\"").Append(Esc(Get(row, h, "icon"))).Append("\",");
                sb.Append("\"stackMax\":").Append(Mathf.Max(1, GetI(row, h, "stackMax", 1))).Append(",");
                sb.Append("\"category\":\"").Append(Esc(Get(row, h, "category", "misc"))).Append("\",");
                sb.Append("\"desc\":\"").Append(Esc(Get(row, h, "desc"))).Append("\",");
                sb.Append("\"weaponKind\":").Append(GetI(row, h, "weaponKind", 0)).Append(",");
                sb.Append("\"damage\":").Append(GetI(row, h, "damage", 0)).Append(",");
                sb.Append("\"attackInterval\":").Append(Num(GetF(row, h, "attackInterval", 0.3f))).Append(",");
                sb.Append("\"range\":").Append(Num(GetF(row, h, "range", 1.2f))).Append(",");
                sb.Append("\"durabilityMax\":").Append(GetI(row, h, "durabilityMax", 0)).Append(",");
                sb.Append("\"ammoItemId\":\"").Append(Esc(Get(row, h, "ammoItemId"))).Append("\",");
                sb.Append("\"ammoPerShot\":").Append(GetI(row, h, "ammoPerShot", 1)).Append(",");
                sb.Append("\"projectileSpeed\":").Append(Num(GetF(row, h, "projectileSpeed", 19f))).Append(",");
                sb.Append("\"projectileLife\":").Append(Num(GetF(row, h, "projectileLife", 1.05f))).Append(",");
                sb.Append("\"healAmount\":").Append(GetI(row, h, "healAmount", 0)).Append(",");
                sb.Append("\"fx\":\"").Append(Esc(Get(row, h, "fx"))).Append("\"");
                sb.Append("}");
                count++;
            }
            sb.Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(OutDir, "items.json"), sb.ToString(), new UTF8Encoding(false));
            return count;
        }

        private static int ImportDrops(Dictionary<string, List<List<string>>> sheets)
        {
            var rows = Sheet(sheets, "Drops");
            int hdr = FindHeader(rows, "enemy");
            var h = Header(rows, hdr);
            var sb = new StringBuilder();
            sb.Append("{\n  \"drops\": [\n");
            int count = 0;

            for (int r = hdr + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string enemy = Get(row, h, "enemy");
                string item = Get(row, h, "item");
                if (string.IsNullOrEmpty(enemy) || string.IsNullOrEmpty(item)) continue;
                if (count > 0) sb.Append(",\n");
                sb.Append("    {");
                sb.Append("\"enemy\":\"").Append(Esc(enemy)).Append("\",");
                sb.Append("\"item\":\"").Append(Esc(item)).Append("\",");
                sb.Append("\"chance\":").Append(Num(Mathf.Clamp01(GetF(row, h, "chance", 1f)))).Append(",");
                sb.Append("\"min\":").Append(Mathf.Max(0, GetI(row, h, "min", 1))).Append(",");
                sb.Append("\"max\":").Append(Mathf.Max(GetI(row, h, "min", 1), GetI(row, h, "max", 1)));
                sb.Append("}");
                count++;
            }
            sb.Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(OutDir, "drops.json"), sb.ToString(), new UTF8Encoding(false));
            return count;
        }

        private static int ImportRecipes(Dictionary<string, List<List<string>>> sheets)
        {
            var rows = Sheet(sheets, "Recipes");
            int hdr = FindHeader(rows, "id");
            var h = Header(rows, hdr);
            var sb = new StringBuilder();
            sb.Append("{\n  \"recipes\": [\n");
            int count = 0;

            for (int r = hdr + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string id = Get(row, h, "id");
                string output = Get(row, h, "output");
                if (string.IsNullOrEmpty(output)) continue;
                if (count > 0) sb.Append(",\n");
                sb.Append("    {");
                sb.Append("\"id\":\"").Append(Esc(string.IsNullOrEmpty(id) ? output : id)).Append("\",");
                sb.Append("\"output\":\"").Append(Esc(output)).Append("\",");
                sb.Append("\"outputCount\":").Append(Mathf.Max(1, GetI(row, h, "outputCount", 1))).Append(",");
                sb.Append("\"input1\":\"").Append(Esc(Get(row, h, "input1"))).Append("\",");
                sb.Append("\"count1\":").Append(Mathf.Max(0, GetI(row, h, "count1", 0))).Append(",");
                sb.Append("\"input2\":\"").Append(Esc(Get(row, h, "input2"))).Append("\",");
                sb.Append("\"count2\":").Append(Mathf.Max(0, GetI(row, h, "count2", 0))).Append(",");
                sb.Append("\"category\":\"").Append(Esc(Get(row, h, "category", "武器"))).Append("\"");
                sb.Append("}");
                count++;
            }
            sb.Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(OutDir, "recipes.json"), sb.ToString(), new UTF8Encoding(false));
            return count;
        }

        private static int ApplyBalance(Dictionary<string, List<List<string>>> sheets)
        {
            List<List<string>> rows;
            if (!sheets.TryGetValue("Balance", out rows) || rows.Count == 0) return 0;

            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Resources/GameConfig.asset");
            if (cfg == null) cfg = AresSetup.EnsureGameConfig();
            if (cfg == null) return 0;

            int applied = 0;
            int hdr = FindHeader(rows, "key");
            for (int r = hdr + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count < 2) continue;
                string key = (row[0] ?? "").Trim();
                string val = (row[1] ?? "").Trim();
                if (key.Length == 0 || val.Length == 0) continue;

                var f = typeof(GameConfig).GetField(key, BindingFlags.Public | BindingFlags.Instance);
                if (f == null) { Debug.LogWarning("[Ares] Balance: unknown field " + key); continue; }
                try
                {
                    if (f.FieldType == typeof(float)) f.SetValue(cfg, float.Parse(val, System.Globalization.CultureInfo.InvariantCulture));
                    else if (f.FieldType == typeof(int)) f.SetValue(cfg, int.Parse(val));
                    else if (f.FieldType == typeof(bool)) f.SetValue(cfg, val == "1" || val.ToLower() == "true");
                    else continue;
                    applied++;
                }
                catch (Exception e) { Debug.LogWarning("[Ares] Balance: " + key + " -> " + e.Message); }
            }
            if (applied > 0)
            {
                EditorUtility.SetDirty(cfg);
                AssetDatabase.SaveAssets();
            }
            return applied;
        }
    }
}
