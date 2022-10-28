using Miniscript;

namespace ThingsEditor.Scripting.Modules
{
    public class AsApi : MiniscriptAPI
    {
        public static readonly ValMap AsClass = BuildClass();
        public static readonly ValString AsSource = new("original");
        public static readonly ValString AsAlias = new("alias");
        private static ValFunction asClassCaller;

        [ApiModuleInitializer]
        private static void Initialize()
        {
            RegisterClass("as", () => AsClass);
            AsFunction();
        }

        public static ValFunction AsFunction()
        {
            if (asClassCaller != null) return asClassCaller;
            asClassCaller = SelfMethod("", AsFunc).Param("alias").Build();
            Intrinsics.MapType()["as"] = asClassCaller;
            Intrinsics.StringType()["as"] = asClassCaller;
            Intrinsics.NumberType()["as"] = asClassCaller;
            Intrinsics.ListType()["as"] = asClassCaller;
            Intrinsics.FunctionType()["as"] = asClassCaller;
            return asClassCaller;
        }

        private static Intrinsic.Result AsFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var self = context.self;
            var alias = context.GetLocal("alias");
            if (alias is null) throw new RuntimeException("alias must not be null");
            var map = new ValMap();
            map.SetElem(ValString.magicIsA, AsClass);
            map.SetElem(AsSource, self);
            map.SetElem(AsAlias, alias);
            return new Intrinsic.Result(map);
        }

        public static (Value source, Value alias) Unwrap(ValMap map)
        {
            if (!map.TryGetValue(AsSource.ToString(), out var source) ||
                !map.TryGetValue(AsAlias.ToString(), out var alias)) throw new RuntimeException("Invalid 'as' map");
            return (source, alias);
        }
    }
}
