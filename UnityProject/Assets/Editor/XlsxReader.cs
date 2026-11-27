using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;

namespace Ares.EditorTools
{
    /// <summary>
    /// Tiny dependency free .xlsx reader (no Excel install, no third party DLL).
    /// Reads shared strings plus the first worksheet cell values of a named sheet.
    /// </summary>
    public static class XlsxReader
    {
        public static Dictionary<string, List<List<string>>> ReadWorkbook(string path)
        {
            var result = new Dictionary<string, List<List<string>>>(StringComparer.OrdinalIgnoreCase);
            using (var zip = ZipFile.OpenRead(path))
            {
                var shared = ReadSharedStrings(zip);
                var sheetTargets = MapSheetTargets(zip);

                foreach (var kv in sheetTargets)
                {
                    var entry = FindEntry(zip, kv.Value);
                    if (entry == null) continue;
                    result[kv.Key] = ReadWorksheet(entry, shared);
                }
            }
            return result;
        }

        private static ZipArchiveEntry FindEntry(ZipArchive zip, string name)
        {
            string normalized = name.Replace("\\", "/").TrimStart('/');
            foreach (var e in zip.Entries)
            {
                if (string.Equals(e.FullName, normalized, StringComparison.OrdinalIgnoreCase)) return e;
            }
            // fall back to suffix match (some writers prefix with "/xl/")
            foreach (var e in zip.Entries)
            {
                if (e.FullName.EndsWith(normalized, StringComparison.OrdinalIgnoreCase)) return e;
            }
            return null;
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            var entry = FindEntry(zip, "xl/sharedStrings.xml");
            if (entry == null) return list;
            using (var s = entry.Open())
            {
                var doc = XDocument.Load(s);
                foreach (var si in doc.Root.Elements())
                {
                    // concatenate every <t> inside the <si> (rich text runs)
                    var text = "";
                    foreach (var t in si.Descendants())
                    {
                        if (t.Name.LocalName == "t") text += t.Value;
                    }
                    list.Add(text);
                }
            }
            return list;
        }

        private static Dictionary<string, string> MapSheetTargets(ZipArchive zip)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var rels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var relEntry = FindEntry(zip, "xl/_rels/workbook.xml.rels");
            if (relEntry != null)
            {
                using (var s = relEntry.Open())
                {
                    var doc = XDocument.Load(s);
                    foreach (var rel in doc.Root.Elements())
                    {
                        var id = rel.Attribute("Id");
                        var target = rel.Attribute("Target");
                        if (id != null && target != null) rels[id.Value] = target.Value;
                    }
                }
            }

            var wbEntry = FindEntry(zip, "xl/workbook.xml");
            if (wbEntry == null) return map;
            using (var s = wbEntry.Open())
            {
                var doc = XDocument.Load(s);
                foreach (var sheet in doc.Descendants())
                {
                    if (sheet.Name.LocalName != "sheet") continue;
                    var name = sheet.Attribute("name");
                    XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                    var rid = sheet.Attribute(r + "id") ?? sheet.Attribute("id");
                    if (name == null || rid == null) continue;
                    string target;
                    if (!rels.TryGetValue(rid.Value, out target))
                    {
                        continue;
                    }
                    string full = target.Replace("\\", "/").TrimStart('/');
                    if (!full.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) full = "xl/" + full;
                    map[name.Value] = full;
                }
            }
            return map;
        }

        private static List<List<string>> ReadWorksheet(ZipArchiveEntry entry, List<string> shared)
        {
            var rows = new List<List<string>>();
            using (var s = entry.Open())
            {
                var doc = XDocument.Load(s);
                foreach (var rowEl in doc.Descendants())
                {
                    if (rowEl.Name.LocalName != "row") continue;
                    var cells = new List<string>();
                    int nextCol = 0;
                    foreach (var c in rowEl.Elements())
                    {
                        if (c.Name.LocalName != "c") continue;
                        int col = nextCol;
                        var rAttr = c.Attribute("r");
                        if (rAttr != null) col = ColumnIndex(rAttr.Value);
                        while (cells.Count < col) cells.Add("");
                        cells.Add(CellValue(c, shared));
                        nextCol = col + 1;
                    }
                    rows.Add(cells);
                }
            }
            return rows;
        }

        private static string CellValue(XElement c, List<string> shared)
        {
            var t = c.Attribute("t");
            string type = t != null ? t.Value : "";

            if (type == "inlineStr")
            {
                foreach (var d in c.Descendants())
                {
                    if (d.Name.LocalName == "t") return d.Value;
                }
                return "";
            }

            XElement v = null;
            foreach (var child in c.Elements())
            {
                if (child.Name.LocalName == "v") { v = child; break; }
            }
            if (v == null) return "";

            if (type == "s")
            {
                int idx;
                if (int.TryParse(v.Value, out idx) && idx >= 0 && idx < shared.Count) return shared[idx];
                return "";
            }
            return v.Value;
        }

        private static int ColumnIndex(string cellRef)
        {
            int col = 0;
            for (int i = 0; i < cellRef.Length; i++)
            {
                char ch = cellRef[i];
                if (ch >= 'A' && ch <= 'Z') col = col * 26 + (ch - 'A' + 1);
                else if (ch >= 'a' && ch <= 'z') col = col * 26 + (ch - 'a' + 1);
                else break;
            }
            return Math.Max(0, col - 1);
        }

        /// <summary>Diagnostics helper used by the editor tooling / tests.</summary>
        public static string DebugDump(string path)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("exists=" + File.Exists(path));
            using (var zip = ZipFile.OpenRead(path))
            {
                sb.AppendLine("entries=" + zip.Entries.Count);
                foreach (var e in zip.Entries) sb.AppendLine("  " + e.FullName);

                var shared = ReadSharedStrings(zip);
                sb.AppendLine("sharedStrings=" + shared.Count);

                var targets = MapSheetTargets(zip);
                sb.AppendLine("sheetTargets=" + targets.Count);
                foreach (var kv in targets) sb.AppendLine("  " + kv.Key + " -> " + kv.Value);

                foreach (var kv in targets)
                {
                    var entry = FindEntry(zip, kv.Value);
                    sb.AppendLine("  entry(" + kv.Key + ")=" + (entry != null ? entry.FullName : "NULL"));
                    if (entry == null) continue;
                    var rows = ReadWorksheet(entry, shared);
                    sb.AppendLine("    rows=" + rows.Count);
                    for (int i = 0; i < Math.Min(4, rows.Count); i++)
                    {
                        sb.AppendLine("      r" + i + ": " + string.Join(" | ", rows[i].ToArray()));
                    }
                }
            }
            return sb.ToString();
        }
    }
}
