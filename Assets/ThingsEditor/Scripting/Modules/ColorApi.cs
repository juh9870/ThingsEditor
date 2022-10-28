using System.Collections.Generic;
using Miniscript;
using ThingsEditor.Utils;
using UnityEngine;

namespace ThingsEditor.Scripting.Modules
{
    public class ColorApi : MiniscriptAPI
    {
        private static ValMap nativeColorMap;

        [ApiModuleInitializer]
        private static void Initialize()
        {
            RegisterClass("_colorNative", ColorNative);
        }

        public static ValMap ColorNative()
        {
            if (nativeColorMap != null) return nativeColorMap;
            return nativeColorMap = BuildClass(
                Method("lerp", LerpColoFunc).Params(("colorA", "#FFFFFF"), ("colorB", "#FFFFFF")).Param("t", 0.5f),
                Method("colorToRGBA", ColorToRgbaFunc).Param("c", "#FFFFFFFF"),
                Method("RGBAtoColor", RgbaToColorFunc).Param("rgbaList"),
                Method("colorToHSVA", ColorToHsvaFunc).Param("c", "#FFFFFFFF"),
                Method("HSVAtoColor", HsvaToColorFunc).Param("hsvaList")
            );
        }

        private static Intrinsic.Result LerpColoFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var c1 = context.variables.GetString("colorA").ToColor32();
            var c2 = context.variables.GetString("colorB").ToColor32();
            var t = context.variables.GetFloat("t", 0.5f);
            var result = Color32.Lerp(c1, c2, t).ToHexString();
            return new Intrinsic.Result(result);
        }

        private static Intrinsic.Result ColorToRgbaFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var c1 = context.variables.GetString("c").ToColor32();
            var list = new List<Value>
                { new ValNumber(c1.r), new ValNumber(c1.g), new ValNumber(c1.b), new ValNumber(c1.a) };
            return new Intrinsic.Result(new ValList(list));
        }

        private static Intrinsic.Result RgbaToColorFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            if (context.GetLocal("rgbaList") is not ValList listVal)
                throw new RuntimeException("List required for rgbaList parameter");
            var values = listVal.ToIntList();
            if (values.Count is < 3 or > 4)
                throw new RuntimeException("rgbaList parameter requires list of 3 or 4 numbers");
            var c = new Color32((byte)values[0], (byte)values[1], (byte)values[2],
                (byte)(values.Count == 4 ? values[3] : 255));
            return new Intrinsic.Result(c.ToHexString());
        }

        private static Intrinsic.Result ColorToHsvaFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var c1 = context.variables.GetString("c").ToColor();
            Color.RGBToHSV(c1, out var h, out var s, out var v);
            var list = new List<Value>
            {
                new ValNumber(Mathf.RoundToInt(h * 255)),
                new ValNumber(Mathf.RoundToInt(s * 255)),
                new ValNumber(Mathf.RoundToInt(v * 255)),
                new ValNumber(Mathf.RoundToInt(c1.a * 255))
            };
            return new Intrinsic.Result(new ValList(list));
        }

        private static Intrinsic.Result HsvaToColorFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            if (context.GetLocal("hsvaList") is not ValList listVal)
                throw new RuntimeException("List required for rgbaList parameter");
            var values = listVal.ToFloatList();
            if (values.Count is < 3 or > 4)
                throw new RuntimeException("hsvaList parameter requires list of 3 or 4 numbers");
            var c = Color.HSVToRGB(values[0] / 255f, values[1] / 255f, values[2] / 255f);
            if (values.Count > 3) c.a = values[3] / 255f;
            return new Intrinsic.Result(c.ToHexString());
        }
    }
}