using System;
using System.Collections.Generic;
using Miniscript;
using UnityEngine;

namespace ThingsEditor.Scripting
{
    internal static class MiniscriptUtils
    {
        public static readonly ValString StrX = new("x");
        public static readonly ValString StrY = new("y");

        public static Value ToValue(this List<string> strings)
        {
            if (strings == null) return null;
            var result = new ValList();
            foreach (var name in strings) result.values.Add(new ValString(name));
            return result;
        }

        public static Value ToValue(this List<float> floats)
        {
            if (floats == null) return null;
            var result = new ValList();
            foreach (var f in floats)
                switch (f)
                {
                    case 0:
                        result.values.Add(ValNumber.zero);
                        break;
                    case 1:
                        result.values.Add(ValNumber.one);
                        break;
                    default:
                        result.values.Add(new ValNumber(f));
                        break;
                }

            return result;
        }

        public static List<string> ToStrings(this Value value)
        {
            if (value is ValList list)
            {
                var result = new List<string>(list.values.Count);
                foreach (var val in list.values) result.Add(val.ToString());
                return result;
            }

            return new List<string>(value.ToString().Split(new[] { "\r\n", "\n", "\r" },
                StringSplitOptions.None));
        }

        public static int ToInt(this Value value, int defaultValue = 0)
        {
            return value?.IntValue() ?? defaultValue;
        }

        public static bool ToBool(this Value value, bool defaultValue = false)
        {
            if (value == null) return defaultValue;
            return value.BoolValue();
        }

        public static Vector2 ToVector2(this Value item)
        {
            var pos = Vector2.zero;
            switch (item)
            {
                case ValList list:
                {
                    var itemVals = list.values;
                    if (itemVals.Count > 0) pos.x = itemVals[0].FloatValue();
                    if (itemVals.Count > 1) pos.y = itemVals[1].FloatValue();
                    break;
                }
                case ValMap valMap:
                {
                    var map = valMap;
                    pos.x = map.Lookup(StrX).FloatValue();
                    pos.y = map.Lookup(StrY).FloatValue();
                    break;
                }
                default:
                {
                    if (item != null) pos.x = item.FloatValue();

                    break;
                }
            }

            return pos;
        }

        public static List<Vector2> ToVector2List(this ValList value)
        {
            var result = new List<Vector2>(value.values.Count);
            for (var i = 0; i < value.values.Count; i++)
            {
                var item = value.values[i];
                result.Add(item.ToVector2());
            }

            return result;
        }

        public static List<int> ToIntList(this ValList value)
        {
            var result = new List<int>(value.values.Count);
            for (var i = 0; i < value.values.Count; i++)
            {
                var item = value.values[i];
                result.Add(item.IntValue());
            }

            return result;
        }

        public static List<float> ToFloatList(this ValList value)
        {
            var result = new List<float>(value.values.Count);
            for (var i = 0; i < value.values.Count; i++)
            {
                var item = value.values[i];
                result.Add(item.FloatValue());
            }

            return result;
        }

        public static Value ToValue(this Vector2 v)
        {
            var item = new ValList();
            item.values.Add(new ValNumber(v.x));
            item.values.Add(new ValNumber(v.y));
            return item;
        }

        public static ValList ToValue(this List<Vector2> vectors)
        {
            var result = new ValList();
            for (var i = 0; i < vectors.Count; i++)
            {
                var v = vectors[i];
                var item = new ValList();
                item.values.Add(new ValNumber(v.x));
                item.values.Add(new ValNumber(v.y));
                result.values.Add(item);
            }

            return result;
        }

        public static string JoinToString(this Value value, string delimiter = "\n")
        {
            if (value == null) return null;
            if (value is ValList)
            {
                var result = new List<string>(((ValList)value).values.Count);
                foreach (var val in ((ValList)value).values) result.Add(val.ToString());
                return string.Join(delimiter, result.ToArray());
            }

            return value.ToString();
        }

        public static float GetFloat(this ValMap map, string key, float defaultValue)
        {
            if (!map.TryGetValue(key, out var val) || val == null) return defaultValue;
            return val.FloatValue();
        }

        public static double GetDouble(this ValMap map, string key, float defaultValue)
        {
            if (!map.TryGetValue(key, out var val) || val == null) return defaultValue;
            return val.DoubleValue();
        }

        public static int GetInt(this ValMap map, string key, int defaultValue)
        {
            if (!map.TryGetValue(key, out var val) || val == null) return defaultValue;
            return val.IntValue();
        }

        public static bool GetBool(this ValMap map, string key, bool defaultValue = false)
        {
            if (!map.TryGetValue(key, out var val) || val == null) return defaultValue;
            return val.BoolValue();
        }

        public static string GetString(this ValMap map, string key, string defaultValue = null)
        {
            if (!map.TryGetValue(key, out var val) || val == null) return defaultValue;
            return val.ToString();
        }

        public static ValMap GetMap(this ValMap map, string key, bool createIfNotFound = false)
        {
            if (map.TryGetValue(key, out var val) && val is ValMap valMap) return valMap;
            if (!createIfNotFound) return null;
            val = new ValMap();
            map[key] = val;
            return (ValMap)val;
        }

        public static Vector2 GetVector2(this ValMap map, string key, Vector2 defValue = default)
        {
            if (!map.TryGetValue(key, out var val)) return defValue;
            return val.ToVector2();
        }
    }
}