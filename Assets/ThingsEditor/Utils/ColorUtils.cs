using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ThingsEditor.Utils
{
    public static class ColorUtils
    {
        private static readonly Dictionary<string, Color32> StringToColorMap = new();

        private static Color lastColor = Color.black;
        private static string lastHexString = "#000000FF";

        public static Color32 ToColor32(this uint i)
        {
            return new Color32(
                (byte)((i >> 24) & 0xFF),
                (byte)((i >> 16) & 0xFF),
                (byte)((i >> 8) & 0xFF),
                (byte)(i & 0xFF));
        }

        public static Color32 ToColor32(this string s)
        {
            if (string.IsNullOrEmpty(s)) return new Color32(0, 0, 0, 0);
            if (StringToColorMap.TryGetValue(s, out var result)) return result;
            if (s[0] != '#' || s.Length is not (7 or 9)) return default;
            const NumberStyles hexStyle = NumberStyles.HexNumber;
            var r = byte.Parse(s.Substring(1, 2), hexStyle);
            var g = byte.Parse(s.Substring(3, 2), hexStyle);
            var b = byte.Parse(s.Substring(5, 2), hexStyle);
            byte a = 255;
            if (s.Length == 9) a = byte.Parse(s.Substring(7, 2), hexStyle);
            result = new Color32(r, g, b, a);
            StringToColorMap[s] = result;
            return result;
        }

        public static Color ToColor(this string s)
        {
            return s.ToColor32();
        }

        public static uint ToUInt(this Color32 c)
        {
            return ((uint)c.r << 24) | ((uint)c.g << 16) | ((uint)c.b << 8) | c.a;
        }

        public static string ToString(this Color32 c)
        {
            return $"#{c.r:X2}{c.g:X2}{c.b:X2}{c.a:X2}";
        }

        public static string ToHexString(this Color32 c)
        {
            return $"#{c.r:X2}{c.g:X2}{c.b:X2}{c.a:X2}";
        }

        public static uint ToUInt(this Color c)
        {
            return ((Color32)c).ToUInt();
        }

        public static string ToString(this Color c)
        {
            return ((Color32)c).ToString();
        }

        public static string ToHexString(this Color c)
        {
            if (c == lastColor) return lastHexString;
            lastColor = c;
            lastHexString = ((Color32)c).ToHexString();

            return lastHexString;
        }

        public static bool IsEqualTo(this Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.b;
        }

        public static Color32 Times(this Color32 a, Color32 b)
        {
            return new Color32(
                (byte)(a.r * b.r / 255),
                (byte)(a.g * b.g / 255),
                (byte)(a.b * b.b / 255),
                (byte)(a.a * b.a / 255));
        }

        public static void MultiplyBy(ref this Color32 a, Color32 b)
        {
            a.r = (byte)(a.r * b.r / 255);
            a.g = (byte)(a.g * b.g / 255);
            a.b = (byte)(a.b * b.b / 255);
            a.a = (byte)(a.a * b.a / 255);
        }
    }
}