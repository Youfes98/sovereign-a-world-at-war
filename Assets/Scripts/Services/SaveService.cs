// SaveService.cs
// Port of SaveSystem.gd — F5 quicksave, F9 quickload.
// Uses custom serialization because JsonUtility cannot serialize Dictionary<K,V>.

using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using WarStrategy.Data;

namespace WarStrategy.Core
{
    public class SaveService : MonoBehaviour
    {
        public event System.Action GameLoaded;

        private string SaveDir => Path.Combine(Application.persistentDataPath, "saves");
        private string QuicksavePath => Path.Combine(SaveDir, "quicksave.json");

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5))
                Quicksave();
            if (Input.GetKeyDown(KeyCode.F9))
                Quickload();
        }

        public void Quicksave()
        {
            if (string.IsNullOrEmpty(Services.GameState.PlayerIso))
            {
                Debug.LogWarning("[Save] Cannot save — no player country selected.");
                return;
            }

            Directory.CreateDirectory(SaveDir);

            var sb = new StringBuilder(1024 * 64);
            sb.Append("{\n");

            // Date
            var date = Services.Clock.Date;
            sb.Append($"  \"year\": {date.Year},\n");
            sb.Append($"  \"month\": {date.Month},\n");
            sb.Append($"  \"day\": {date.Day},\n");
            sb.Append($"  \"hour\": {date.Hour},\n");
            sb.Append($"  \"totalDays\": {Services.Clock.TotalDays},\n");
            sb.Append($"  \"playerIso\": \"{EscapeJson(Services.GameState.PlayerIso)}\",\n");

            // Countries — serialize key fields
            sb.Append("  \"countries\": {\n");
            int ci = 0;
            foreach (var kvp in Services.GameState.Countries)
            {
                if (ci > 0) sb.Append(",\n");
                var c = kvp.Value;
                sb.Append($"    \"{EscapeJson(kvp.Key)}\": {{");
                sb.Append($"\"treasury\":{c.Treasury:F2},");
                sb.Append($"\"debtToGdp\":{c.DebtToGdp:F4},");
                sb.Append($"\"creditRating\":{c.CreditRating:F1},");
                sb.Append($"\"stability\":{c.Stability:F1},");
                sb.Append($"\"infrastructure\":{c.Infrastructure:F1},");
                sb.Append($"\"taxRate\":{c.TaxRate:F4},");
                sb.Append($"\"budgetMilitary\":{c.BudgetMilitary:F3},");
                sb.Append($"\"budgetInfrastructure\":{c.BudgetInfrastructure:F3},");
                sb.Append($"\"budgetResearch\":{c.BudgetResearch:F3},");
                sb.Append($"\"population\":{c.Population},");
                sb.Append($"\"gdpRawBillions\":{c.GdpRawBillions:F4},");
                sb.Append($"\"gdpNormalized\":{c.GdpNormalized:F2},");
                sb.Append($"\"monthlyBalance\":{c.MonthlyBalance:F2},");
                sb.Append($"\"inflation\":{c.Inflation:F4}");
                sb.Append("}");
                ci++;
            }
            sb.Append("\n  },\n");

            // Territory ownership
            sb.Append("  \"territory\": {\n");
            int ti = 0;
            foreach (var kvp in Services.GameState.TerritoryOwnership)
            {
                if (ti > 0) sb.Append(",\n");
                sb.Append($"    \"{EscapeJson(kvp.Key)}\": \"{EscapeJson(kvp.Value)}\"");
                ti++;
            }
            sb.Append("\n  },\n");

            // Memories
            sb.Append("  \"memories\": ");
            sb.Append(JsonUtility.ToJson(new MemoryListWrapper { Items = Services.WorldMemory.Memories }));
            sb.Append(",\n");

            // Reputations
            sb.Append("  \"reputations\": {\n");
            int ri = 0;
            foreach (var kvp in Services.WorldMemory.Reputations)
            {
                if (ri > 0) sb.Append(",\n");
                sb.Append($"    \"{EscapeJson(kvp.Key)}\": {kvp.Value:F4}");
                ri++;
            }
            sb.Append("\n  }\n");

            sb.Append("}");

            File.WriteAllText(QuicksavePath, sb.ToString());
            Debug.Log($"[Save] Quicksaved to {QuicksavePath} ({ci} countries, {ti} territories)");
        }

        public void Quickload()
        {
            if (!File.Exists(QuicksavePath))
            {
                Debug.LogWarning("[Save] No quicksave found.");
                return;
            }

            string json = File.ReadAllText(QuicksavePath);

            // Parse using our custom approach — read key-value pairs
            // For now, use a simple state-machine parser for the top-level object
            try
            {
                var root = MiniJson.Parse(json);
                if (root == null) { Debug.LogError("[Save] Failed to parse quicksave."); return; }

                // Restore clock
                int year = root.GetInt("year", 2026);
                int month = root.GetInt("month", 1);
                int day = root.GetInt("day", 1);
                int hour = root.GetInt("hour", 0);
                int totalDays = root.GetInt("totalDays", 0);
                Services.Clock.RestoreDate(new DateData(year, month, day, hour), totalDays);

                // Restore countries
                var countries = root.GetObject("countries");
                if (countries != null)
                {
                    foreach (var kvp in countries.Fields)
                    {
                        if (Services.GameState.Countries.TryGetValue(kvp.Key, out var c))
                        {
                            var obj = kvp.Value as MiniJson.JsonObject;
                            if (obj == null) continue;
                            c.Treasury = obj.GetFloat("treasury", c.Treasury);
                            c.DebtToGdp = obj.GetFloat("debtToGdp", c.DebtToGdp);
                            c.CreditRating = obj.GetFloat("creditRating", c.CreditRating);
                            c.Stability = obj.GetFloat("stability", c.Stability);
                            c.Infrastructure = obj.GetFloat("infrastructure", c.Infrastructure);
                            c.TaxRate = obj.GetFloat("taxRate", c.TaxRate);
                            c.BudgetMilitary = obj.GetFloat("budgetMilitary", c.BudgetMilitary);
                            c.BudgetInfrastructure = obj.GetFloat("budgetInfrastructure", c.BudgetInfrastructure);
                            c.BudgetResearch = obj.GetFloat("budgetResearch", c.BudgetResearch);
                            c.Population = obj.GetLong("population", c.Population);
                            c.GdpRawBillions = obj.GetFloat("gdpRawBillions", c.GdpRawBillions);
                            c.GdpNormalized = obj.GetFloat("gdpNormalized", c.GdpNormalized);
                            c.MonthlyBalance = obj.GetFloat("monthlyBalance", c.MonthlyBalance);
                            c.Inflation = obj.GetFloat("inflation", c.Inflation);
                        }
                    }
                }

                // Restore territory
                var territory = root.GetObject("territory");
                if (territory != null)
                {
                    Services.GameState.TerritoryOwnership.Clear();
                    foreach (var kvp in territory.Fields)
                    {
                        if (kvp.Value is string s)
                            Services.GameState.TerritoryOwnership[kvp.Key] = s;
                    }
                }

                // Restore memories
                // Memories use JsonUtility-compatible List<MemoryRecord>, works via wrapper
                string memoriesJson = root.GetRaw("memories");
                if (!string.IsNullOrEmpty(memoriesJson))
                {
                    var wrapper = JsonUtility.FromJson<MemoryListWrapper>(memoriesJson);
                    if (wrapper != null)
                    {
                        var reps = new Dictionary<string, float>();
                        var reputations = root.GetObject("reputations");
                        if (reputations != null)
                        {
                            foreach (var kvp in reputations.Fields)
                            {
                                if (kvp.Value is float f) reps[kvp.Key] = f;
                                else if (kvp.Value is double d) reps[kvp.Key] = (float)d;
                            }
                        }
                        Services.WorldMemory.RestoreFromSave(wrapper.Items, reps);
                    }
                }

                // Restore player
                string playerIso = root.GetString("playerIso", "");
                if (!string.IsNullOrEmpty(playerIso))
                    Services.GameState.SetPlayerCountry(playerIso);

                GameLoaded?.Invoke();
                Debug.Log($"[Save] Quickloaded from {QuicksavePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Save] Quickload failed: {ex.Message}");
            }
        }

        private static string EscapeJson(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        [System.Serializable]
        private class MemoryListWrapper
        {
            public List<MemoryRecord> Items;
        }
    }

    /// <summary>
    /// Minimal JSON parser for save files. Handles objects, strings, numbers.
    /// NOT a general-purpose parser — only needs to read what SaveService writes.
    /// </summary>
    internal static class MiniJson
    {
        internal class JsonObject
        {
            public Dictionary<string, object> Fields = new();

            public string GetString(string key, string def = "")
            {
                return Fields.TryGetValue(key, out var v) && v is string s ? s : def;
            }

            public int GetInt(string key, int def = 0)
            {
                if (!Fields.TryGetValue(key, out var v)) return def;
                if (v is double d) return (int)d;
                if (v is float f) return (int)f;
                if (v is int i) return i;
                if (v is long l) return (int)l;
                return def;
            }

            public long GetLong(string key, long def = 0)
            {
                if (!Fields.TryGetValue(key, out var v)) return def;
                if (v is double d) return (long)d;
                if (v is float f) return (long)f;
                if (v is long l) return l;
                return def;
            }

            public float GetFloat(string key, float def = 0f)
            {
                if (!Fields.TryGetValue(key, out var v)) return def;
                if (v is double d) return (float)d;
                if (v is float f) return f;
                if (v is int i) return i;
                return def;
            }

            public JsonObject GetObject(string key)
            {
                return Fields.TryGetValue(key, out var v) && v is JsonObject obj ? obj : null;
            }

            public string GetRaw(string key)
            {
                return Fields.TryGetValue(key, out var v) && v is string s ? s : null;
            }
        }

        internal static JsonObject Parse(string json)
        {
            int idx = 0;
            return ParseObject(json, ref idx);
        }

        private static JsonObject ParseObject(string json, ref int idx)
        {
            SkipWhitespace(json, ref idx);
            if (idx >= json.Length || json[idx] != '{') return null;
            idx++; // skip {

            var obj = new JsonObject();
            SkipWhitespace(json, ref idx);

            while (idx < json.Length && json[idx] != '}')
            {
                SkipWhitespace(json, ref idx);
                if (json[idx] == '}') break;
                if (json[idx] == ',') { idx++; continue; }

                string key = ParseString(json, ref idx);
                SkipWhitespace(json, ref idx);
                if (idx < json.Length && json[idx] == ':') idx++;
                SkipWhitespace(json, ref idx);

                // Determine value type
                if (json[idx] == '{')
                {
                    // Check if this is a memories-style complex object or simple k:v
                    // For "memories", store as raw JSON string
                    if (key == "memories")
                    {
                        int start = idx;
                        SkipValue(json, ref idx);
                        obj.Fields[key] = json.Substring(start, idx - start);
                    }
                    else
                    {
                        obj.Fields[key] = ParseObject(json, ref idx);
                    }
                }
                else if (json[idx] == '"')
                {
                    obj.Fields[key] = ParseString(json, ref idx);
                }
                else if (json[idx] == '[')
                {
                    // Store arrays as raw JSON
                    int start = idx;
                    SkipValue(json, ref idx);
                    obj.Fields[key] = json.Substring(start, idx - start);
                }
                else
                {
                    // Number or boolean
                    obj.Fields[key] = ParseNumber(json, ref idx);
                }

                SkipWhitespace(json, ref idx);
                if (idx < json.Length && json[idx] == ',') idx++;
            }

            if (idx < json.Length) idx++; // skip }
            return obj;
        }

        private static string ParseString(string json, ref int idx)
        {
            if (json[idx] != '"') return "";
            idx++; // skip opening "
            int start = idx;
            while (idx < json.Length)
            {
                if (json[idx] == '\\') { idx += 2; continue; }
                if (json[idx] == '"') break;
                idx++;
            }
            string result = json.Substring(start, idx - start);
            if (idx < json.Length) idx++; // skip closing "
            return result;
        }

        private static double ParseNumber(string json, ref int idx)
        {
            int start = idx;
            while (idx < json.Length && (char.IsDigit(json[idx]) || json[idx] == '.' || json[idx] == '-' || json[idx] == 'e' || json[idx] == 'E' || json[idx] == '+'))
                idx++;
            // Handle true/false/null
            if (idx == start)
            {
                if (json.Length - idx >= 4 && json.Substring(idx, 4) == "true") { idx += 4; return 1; }
                if (json.Length - idx >= 5 && json.Substring(idx, 5) == "false") { idx += 5; return 0; }
                if (json.Length - idx >= 4 && json.Substring(idx, 4) == "null") { idx += 4; return 0; }
                idx++;
                return 0;
            }
            double.TryParse(json.Substring(start, idx - start), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double result);
            return result;
        }

        private static void SkipValue(string json, ref int idx)
        {
            SkipWhitespace(json, ref idx);
            if (idx >= json.Length) return;

            char c = json[idx];
            if (c == '"') { ParseString(json, ref idx); }
            else if (c == '{')
            {
                int depth = 1; idx++;
                while (idx < json.Length && depth > 0)
                {
                    if (json[idx] == '{') depth++;
                    else if (json[idx] == '}') depth--;
                    else if (json[idx] == '"') { ParseString(json, ref idx); continue; }
                    idx++;
                }
            }
            else if (c == '[')
            {
                int depth = 1; idx++;
                while (idx < json.Length && depth > 0)
                {
                    if (json[idx] == '[') depth++;
                    else if (json[idx] == ']') depth--;
                    else if (json[idx] == '"') { ParseString(json, ref idx); continue; }
                    idx++;
                }
            }
            else { ParseNumber(json, ref idx); }
        }

        private static void SkipWhitespace(string json, ref int idx)
        {
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\n' || json[idx] == '\r' || json[idx] == '\t'))
                idx++;
        }
    }
}
