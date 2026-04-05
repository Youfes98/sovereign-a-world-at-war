// JsonParser.cs
// High-performance JSON parsing for game data files.
// Single-pass, index-based parsing with zero substring allocations for numeric fields.
// Optimized for our known fixed schemas (countries, provinces, adjacencies).

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace WarStrategy.Data
{
    public static class JsonParser
    {
        // ── Countries ──

        public static List<CountryData> ParseCountries(string json)
        {
            var list = new List<CountryData>(200);
            int i = SkipTo(json, 0, '[');
            if (i < 0) return list;
            i++;

            while (i < json.Length)
            {
                i = SkipWhitespace(json, i);
                if (i >= json.Length) break;
                if (json[i] == ']') break;
                if (json[i] == ',') { i++; continue; }
                if (json[i] != '{') { i++; continue; }

                // Start of object
                i++;
                var c = new CountryData();
                bool valid = false;

                while (i < json.Length)
                {
                    i = SkipWhitespace(json, i);
                    if (i >= json.Length) break;
                    if (json[i] == '}') { i++; break; }
                    if (json[i] == ',') { i++; continue; }

                    // Parse key
                    if (json[i] != '"') { i++; continue; }
                    int keyStart = i + 1;
                    int keyEnd = IndexOfUnescaped(json, '"', keyStart);
                    if (keyEnd < 0) break;
                    int keyLen = keyEnd - keyStart;
                    i = keyEnd + 1;

                    // Skip colon
                    i = SkipWhitespace(json, i);
                    if (i < json.Length && json[i] == ':') i++;
                    i = SkipWhitespace(json, i);

                    // Match key and parse value inline
                    // We match on first char + length for speed, then verify
                    if (keyLen == 3 && json[keyStart] == 'i' && json[keyStart + 1] == 's' && json[keyStart + 2] == 'o')
                    {
                        c.Iso = ParseStringValue(json, ref i);
                        valid = c.Iso.Length > 0;
                    }
                    else if (keyLen == 4 && json[keyStart] == 'i') // iso2
                    {
                        c.Iso2 = ParseStringValue(json, ref i);
                    }
                    else if (keyLen == 4 && json[keyStart] == 'n' && json[keyStart + 1] == 'a') // name
                    {
                        c.Name = ParseStringValue(json, ref i);
                    }
                    else if (keyLen == 7 && json[keyStart] == 'c' && json[keyStart + 1] == 'a' && json[keyStart + 2] == 'p'
                             && json[keyStart + 3] == 'i') // capital
                    {
                        c.Capital = ParseStringValue(json, ref i);
                    }
                    else if (keyLen == 16 && json[keyStart] == 'c' && json[keyStart + 1] == 'a' && json[keyStart + 2] == 'p'
                             && json[keyStart + 3] == 'i' && json[keyStart + 7] == '_') // capital_centroid
                    {
                        c.CapitalCentroid = ParseVector2Value(json, ref i);
                    }
                    else if (keyLen == 6 && json[keyStart] == 'r' && json[keyStart + 1] == 'e') // region
                    {
                        c.Region = ParseStringValue(json, ref i);
                    }
                    else if (keyLen == 9 && json[keyStart] == 's' && json[keyStart + 1] == 'u') // subregion
                    {
                        c.Subregion = ParseStringValue(json, ref i);
                    }
                    else if (keyLen == 10 && json[keyStart] == 'p' && json[keyStart + 1] == 'o'
                             && json[keyStart + 2] == 'p' && json[keyStart + 3] == 'u') // population
                    {
                        c.Population = ParseLongValue(json, ref i);
                    }
                    else if (keyLen == 7 && json[keyStart] == 'a') // area_km2
                    {
                        SkipValue(json, ref i); // not used in CountryData
                    }
                    else if (keyLen == 9 && json[keyStart] == 'l' && json[keyStart + 1] == 'a'
                             && json[keyStart + 2] == 'n') // landlocked
                    {
                        c.Landlocked = ParseBoolValue(json, ref i);
                    }
                    else if (keyLen == 6 && json[keyStart] == 'l' && json[keyStart + 1] == 'a') // latlng
                    {
                        SkipValue(json, ref i);
                    }
                    else if (keyLen == 8 && json[keyStart] == 'c' && json[keyStart + 1] == 'e') // centroid
                    {
                        c.Centroid = ParseVector2Value(json, ref i);
                    }
                    else if (keyLen == 7 && json[keyStart] == 'b' && json[keyStart + 1] == 'o') // borders
                    {
                        SkipValue(json, ref i);
                    }
                    else if (keyLen == 14 && json[keyStart] == 'g' && json[keyStart + 3] == '_'
                             && json[keyStart + 4] == 'n') // gdp_normalized
                    {
                        c.GdpNormalized = ParseFloatValue(json, ref i);
                    }
                    else if (keyLen == 21 && json[keyStart] == 'p' && json[keyStart + 10] == '_'
                             && json[keyStart + 11] == 'n') // population_normalized
                    {
                        c.PopulationNormalized = ParseIntValue(json, ref i);
                    }
                    else if (keyLen == 19 && json[keyStart] == 'm' && json[keyStart + 8] == '_'
                             && json[keyStart + 9] == 'n') // military_normalized
                    {
                        c.MilitaryNormalized = ParseIntValue(json, ref i);
                    }
                    else if (keyLen == 9 && json[keyStart] == 's' && json[keyStart + 1] == 't') // stability
                    {
                        c.Stability = ParseFloatValue(json, ref i);
                    }
                    else if (keyLen == 10 && json[keyStart] == 'p' && json[keyStart + 1] == 'o'
                             && json[keyStart + 5] == '_') // power_tier
                    {
                        c.PowerTier = ParseStringValue(json, ref i);
                    }
                    else if (keyLen == 9 && json[keyStart] == 'm' && json[keyStart + 1] == 'a') // map_color
                    {
                        var col = ParseColor255Value(json, ref i);
                        c.MapColor = col;
                    }
                    else if (keyLen == 10 && json[keyStart] == 'f') // flag_emoji
                    {
                        SkipValue(json, ref i);
                    }
                    else if (keyLen == 16 && json[keyStart] == 'g' && json[keyStart + 4] == 'r') // gdp_raw_billions
                    {
                        c.GdpRawBillions = ParseFloatValue(json, ref i);
                    }
                    else if (keyLen == 11 && json[keyStart] == 'd') // debt_to_gdp
                    {
                        c.DebtToGdp = ParseFloatValue(json, ref i);
                    }
                    else if (keyLen == 13 && json[keyStart] == 'c' && json[keyStart + 1] == 'r') // credit_rating
                    {
                        c.CreditRating = ParseFloatValue(json, ref i);
                    }
                    else if (keyLen == 14 && json[keyStart] == 'i') // infrastructure
                    {
                        c.Infrastructure = ParseFloatValue(json, ref i);
                    }
                    else if (keyLen == 13 && json[keyStart] == 'l' && json[keyStart + 1] == 'i') // literacy_rate
                    {
                        c.LiteracyRate = ParseFloatValue(json, ref i);
                    }
                    else if (keyLen == 15 && json[keyStart] == 'g' && json[keyStart + 1] == 'o') // government_type
                    {
                        c.GovernmentType = ParseStringValue(json, ref i);
                    }
                    else if (keyLen == 7 && json[keyStart] == 'p' && json[keyStart + 1] == 'o'
                             && json[keyStart + 2] == 'l') // polygon
                    {
                        SkipValue(json, ref i); // countries don't need polygon in CountryData
                    }
                    else
                    {
                        SkipValue(json, ref i);
                    }
                }

                if (valid)
                {
                    c.TaxRate = 0.25f;
                    c.TaxMin = 0.1f;
                    c.TaxMax = 0.6f;
                    c.BudgetMilitary = 0.33f;
                    c.BudgetInfrastructure = 0.34f;
                    c.BudgetResearch = 0.33f;
                    c.Treasury = c.GdpRawBillions * 0.05f;
                    list.Add(c);
                }
            }

            return list;
        }

        // ── Provinces ──

        public static List<ProvinceData> ParseProvinces(string json)
        {
            var list = new List<ProvinceData>(4600);
            int i = SkipTo(json, 0, '[');
            if (i < 0) return list;
            i++;

            while (i < json.Length)
            {
                i = SkipWhitespace(json, i);
                if (i >= json.Length) break;
                if (json[i] == ']') break;
                if (json[i] == ',') { i++; continue; }
                if (json[i] != '{') { i++; continue; }

                i++;
                var p = new ProvinceData();
                bool valid = false;

                while (i < json.Length)
                {
                    i = SkipWhitespace(json, i);
                    if (i >= json.Length) break;
                    if (json[i] == '}') { i++; break; }
                    if (json[i] == ',') { i++; continue; }

                    if (json[i] != '"') { i++; continue; }
                    int keyStart = i + 1;
                    int keyEnd = IndexOfUnescaped(json, '"', keyStart);
                    if (keyEnd < 0) break;
                    int keyLen = keyEnd - keyStart;
                    i = keyEnd + 1;

                    i = SkipWhitespace(json, i);
                    if (i < json.Length && json[i] == ':') i++;
                    i = SkipWhitespace(json, i);

                    if (keyLen == 2 && json[keyStart] == 'i' && json[keyStart + 1] == 'd')
                    {
                        p.Id = ParseStringValue(json, ref i);
                        valid = p.Id.Length > 0;
                    }
                    else if (keyLen == 4 && json[keyStart] == 'n') // name
                    {
                        p.Name = ParseStringValue(json, ref i);
                    }
                    else if (keyLen == 10 && json[keyStart] == 'p' && json[keyStart + 7] == 'i') // parent_iso
                    {
                        p.ParentIso = ParseStringValue(json, ref i);
                    }
                    else if (keyLen == 7 && json[keyStart] == 'p' && json[keyStart + 1] == 'o') // polygon
                    {
                        p.Polygon = ParsePolygonValue(json, ref i);
                    }
                    else if (keyLen == 8 && json[keyStart] == 'c' && json[keyStart + 1] == 'e') // centroid
                    {
                        p.Centroid = ParseVector2Value(json, ref i);
                    }
                    else if (keyLen == 12 && json[keyStart] == 'd') // detect_color
                    {
                        p.DetectColor = ParseColor255Value(json, ref i);
                    }
                    else if (keyLen == 7 && json[keyStart] == 't') // terrain
                    {
                        p.Terrain = ParseStringValue(json, ref i);
                    }
                    else if (keyLen == 7 && json[keyStart] == 'c' && json[keyStart + 1] == 'o') // coastal
                    {
                        SkipValue(json, ref i);
                    }
                    else if (keyLen == 12 && json[keyStart] == 'g') // gdp_billions
                    {
                        p.GdpContribution = ParseFloatValue(json, ref i);
                    }
                    else if (keyLen == 14 && json[keyStart] == 'e') // est_population
                    {
                        p.Population = ParseIntValue(json, ref i);
                    }
                    else
                    {
                        SkipValue(json, ref i);
                    }
                }

                if (valid)
                {
                    if (p.Polygon != null && p.Polygon.Length >= 3)
                        p.AreaKm2 = ComputePolygonArea(p.Polygon);
                    list.Add(p);
                }
            }

            return list;
        }

        // ── Adjacencies ──

        public static Dictionary<string, List<string>> ParseAdjacencies(string json)
        {
            var result = new Dictionary<string, List<string>>(512);
            if (string.IsNullOrEmpty(json)) return result;

            int i = SkipTo(json, 0, '{');
            if (i < 0) return result;
            i++;

            while (i < json.Length)
            {
                i = SkipWhitespace(json, i);
                if (i >= json.Length) break;
                if (json[i] == '}') break;
                if (json[i] == ',') { i++; continue; }

                // Key
                if (json[i] != '"') { i++; continue; }
                string key = ParseStringValue(json, ref i);

                i = SkipWhitespace(json, i);
                if (i < json.Length && json[i] == ':') i++;
                i = SkipWhitespace(json, i);

                // Value array of strings
                var values = new List<string>(8);
                if (i < json.Length && json[i] == '[')
                {
                    i++; // skip [
                    while (i < json.Length)
                    {
                        i = SkipWhitespace(json, i);
                        if (i >= json.Length) break;
                        if (json[i] == ']') { i++; break; }
                        if (json[i] == ',') { i++; continue; }
                        if (json[i] == '"')
                        {
                            values.Add(ParseStringValue(json, ref i));
                        }
                        else
                        {
                            i++;
                        }
                    }
                }

                if (key.Length > 0)
                    result[key] = values;
            }

            return result;
        }

        // ══════════════════════════════════════════════════════════
        // Inline value parsers - operate directly on the json string
        // with index i pointing at the start of the value.
        // After return, i points past the consumed value.
        // ══════════════════════════════════════════════════════════

        private static string ParseStringValue(string json, ref int i)
        {
            if (i >= json.Length) return "";
            if (json[i] != '"') { SkipValue(json, ref i); return ""; }
            int start = i + 1;
            int end = IndexOfUnescaped(json, '"', start);
            if (end < 0) { i = json.Length; return ""; }
            i = end + 1;
            return json.Substring(start, end - start);
        }

        private static float ParseFloatValue(string json, ref int i)
        {
            if (i >= json.Length) return 0f;
            // Handle null
            if (json[i] == 'n') { SkipLiteral(json, ref i); return 0f; }

            int start = i;
            bool negative = false;
            if (json[i] == '-') { negative = true; i++; }

            long intPart = 0;
            while (i < json.Length && json[i] >= '0' && json[i] <= '9')
            {
                intPart = intPart * 10 + (json[i] - '0');
                i++;
            }

            double result = intPart;

            if (i < json.Length && json[i] == '.')
            {
                i++;
                double fraction = 0;
                double divisor = 10;
                while (i < json.Length && json[i] >= '0' && json[i] <= '9')
                {
                    fraction += (json[i] - '0') / divisor;
                    divisor *= 10;
                    i++;
                }
                result += fraction;
            }

            // Handle scientific notation (e.g. 1.5e+06)
            if (i < json.Length && (json[i] == 'e' || json[i] == 'E'))
            {
                i++;
                bool expNeg = false;
                if (i < json.Length && json[i] == '+') i++;
                else if (i < json.Length && json[i] == '-') { expNeg = true; i++; }
                int exp = 0;
                while (i < json.Length && json[i] >= '0' && json[i] <= '9')
                {
                    exp = exp * 10 + (json[i] - '0');
                    i++;
                }
                if (expNeg)
                    result /= Math.Pow(10, exp);
                else
                    result *= Math.Pow(10, exp);
            }

            return negative ? (float)-result : (float)result;
        }

        private static int ParseIntValue(string json, ref int i)
        {
            if (i >= json.Length) return 0;
            if (json[i] == 'n') { SkipLiteral(json, ref i); return 0; }

            bool negative = false;
            if (json[i] == '-') { negative = true; i++; }

            long val = 0;
            while (i < json.Length && json[i] >= '0' && json[i] <= '9')
            {
                val = val * 10 + (json[i] - '0');
                i++;
            }

            // Handle decimal point (truncate)
            if (i < json.Length && json[i] == '.')
            {
                i++;
                while (i < json.Length && json[i] >= '0' && json[i] <= '9') i++;
            }

            // Handle scientific notation
            if (i < json.Length && (json[i] == 'e' || json[i] == 'E'))
            {
                // Fallback: re-parse with float
                // This is rare so perf doesn't matter
                return (int)ParseFloatValueFromLong(val, negative, json, ref i);
            }

            return negative ? (int)-val : (int)val;
        }

        private static long ParseLongValue(string json, ref int i)
        {
            if (i >= json.Length) return 0;
            if (json[i] == 'n') { SkipLiteral(json, ref i); return 0; }

            bool negative = false;
            if (json[i] == '-') { negative = true; i++; }

            long val = 0;
            while (i < json.Length && json[i] >= '0' && json[i] <= '9')
            {
                val = val * 10 + (json[i] - '0');
                i++;
            }

            if (i < json.Length && json[i] == '.')
            {
                i++;
                while (i < json.Length && json[i] >= '0' && json[i] <= '9') i++;
            }

            if (i < json.Length && (json[i] == 'e' || json[i] == 'E'))
            {
                return (long)ParseFloatValueFromLong(val, negative, json, ref i);
            }

            return negative ? -val : val;
        }

        private static float ParseFloatValueFromLong(long intPart, bool negative, string json, ref int i)
        {
            // Already consumed integer part, now at 'e'/'E'
            double result = intPart;
            i++; // skip e/E
            bool expNeg = false;
            if (i < json.Length && json[i] == '+') i++;
            else if (i < json.Length && json[i] == '-') { expNeg = true; i++; }
            int exp = 0;
            while (i < json.Length && json[i] >= '0' && json[i] <= '9')
            {
                exp = exp * 10 + (json[i] - '0');
                i++;
            }
            if (expNeg) result /= Math.Pow(10, exp);
            else result *= Math.Pow(10, exp);
            return negative ? (float)-result : (float)result;
        }

        private static bool ParseBoolValue(string json, ref int i)
        {
            if (i >= json.Length) return false;
            if (json[i] == 't') { i += 4; return true; } // true
            if (json[i] == 'f') { i += 5; return false; } // false
            SkipValue(json, ref i);
            return false;
        }

        private static Vector2 ParseVector2Value(string json, ref int i)
        {
            // Expects [x, y]
            if (i >= json.Length || json[i] != '[') { SkipValue(json, ref i); return Vector2.zero; }
            i++; // skip [
            i = SkipWhitespace(json, i);
            float x = ParseFloatValue(json, ref i);
            i = SkipWhitespace(json, i);
            if (i < json.Length && json[i] == ',') i++;
            i = SkipWhitespace(json, i);
            float y = ParseFloatValue(json, ref i);
            i = SkipWhitespace(json, i);
            if (i < json.Length && json[i] == ']') i++;
            return new Vector2(x, y);
        }

        private static Color ParseColor255Value(string json, ref int i)
        {
            // Expects [R, G, B] with 0-255 values
            if (i >= json.Length || json[i] != '[') { SkipValue(json, ref i); return Color.black; }
            i++; // skip [
            i = SkipWhitespace(json, i);
            float r = ParseFloatValue(json, ref i);
            i = SkipWhitespace(json, i);
            if (i < json.Length && json[i] == ',') i++;
            i = SkipWhitespace(json, i);
            float g = ParseFloatValue(json, ref i);
            i = SkipWhitespace(json, i);
            if (i < json.Length && json[i] == ',') i++;
            i = SkipWhitespace(json, i);
            float b = ParseFloatValue(json, ref i);
            i = SkipWhitespace(json, i);
            if (i < json.Length && json[i] == ']') i++;
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        private static Vector2[] ParsePolygonValue(string json, ref int i)
        {
            // Expects [[x,y], [x,y], ...]
            if (i >= json.Length || json[i] != '[') { SkipValue(json, ref i); return null; }
            i++; // skip outer [

            // Pre-count pairs for allocation (scan for inner '[' count)
            int countPos = i;
            int depth = 1;
            int pairCount = 0;
            while (countPos < json.Length && depth > 0)
            {
                char ch = json[countPos];
                if (ch == '[') { depth++; pairCount++; }
                else if (ch == ']') { depth--; }
                else if (ch == '"')
                {
                    countPos++;
                    while (countPos < json.Length && json[countPos] != '"')
                    {
                        if (json[countPos] == '\\') countPos++;
                        countPos++;
                    }
                }
                countPos++;
            }

            var points = new Vector2[pairCount];
            int idx = 0;

            while (i < json.Length)
            {
                i = SkipWhitespace(json, i);
                if (i >= json.Length) break;
                if (json[i] == ']') { i++; break; } // end outer array
                if (json[i] == ',') { i++; continue; }

                if (json[i] == '[')
                {
                    // Inner [x, y]
                    i++;
                    i = SkipWhitespace(json, i);
                    float x = ParseFloatValue(json, ref i);
                    i = SkipWhitespace(json, i);
                    if (i < json.Length && json[i] == ',') i++;
                    i = SkipWhitespace(json, i);
                    float y = ParseFloatValue(json, ref i);
                    i = SkipWhitespace(json, i);
                    if (i < json.Length && json[i] == ']') i++;

                    if (idx < points.Length)
                        points[idx++] = new Vector2(x, y);
                }
                else
                {
                    i++;
                }
            }

            if (idx == 0) return null;
            if (idx < points.Length) Array.Resize(ref points, idx);
            return points;
        }

        // ══════════════════════════════════════════════════════════
        // Navigation helpers
        // ══════════════════════════════════════════════════════════

        private static int SkipWhitespace(string json, int i)
        {
            while (i < json.Length)
            {
                char c = json[i];
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t')
                    i++;
                else
                    break;
            }
            return i;
        }

        private static int SkipTo(string json, int start, char target)
        {
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == target) return i;
            }
            return -1;
        }

        private static int IndexOfUnescaped(string json, char target, int start)
        {
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '\\') { i++; continue; }
                if (json[i] == target) return i;
            }
            return -1;
        }

        /// <summary>
        /// Skip over any JSON value (string, number, bool, null, array, object) starting at i.
        /// </summary>
        private static void SkipValue(string json, ref int i)
        {
            if (i >= json.Length) return;
            char c = json[i];

            if (c == '"')
            {
                // String
                i++;
                while (i < json.Length)
                {
                    if (json[i] == '\\') { i += 2; continue; }
                    if (json[i] == '"') { i++; return; }
                    i++;
                }
            }
            else if (c == '[' || c == '{')
            {
                // Array or object - track depth
                char open = c;
                char close = c == '[' ? ']' : '}';
                int depth = 1;
                i++;
                while (i < json.Length && depth > 0)
                {
                    char ch = json[i];
                    if (ch == open) depth++;
                    else if (ch == close) depth--;
                    else if (ch == '"')
                    {
                        i++;
                        while (i < json.Length)
                        {
                            if (json[i] == '\\') { i += 2; continue; }
                            if (json[i] == '"') break;
                            i++;
                        }
                    }
                    i++;
                }
            }
            else
            {
                // Number, bool, null - advance to delimiter
                SkipLiteral(json, ref i);
            }
        }

        private static void SkipLiteral(string json, ref int i)
        {
            while (i < json.Length)
            {
                char c = json[i];
                if (c == ',' || c == '}' || c == ']' || c == ' ' || c == '\n' || c == '\r' || c == '\t')
                    return;
                i++;
            }
        }

        private static float ComputePolygonArea(Vector2[] polygon)
        {
            float area = 0;
            int n = polygon.Length;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += polygon[i].x * polygon[j].y;
                area -= polygon[j].x * polygon[i].y;
            }
            return Mathf.Abs(area) * 0.5f;
        }
    }
}
