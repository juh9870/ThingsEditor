using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JetBrains.Annotations;
using Miniscript;
using ThingsEditor.IO.Filesystem;

// ReSharper disable MemberCanBePrivate.Global

namespace ThingsEditor.Scripting.Modules
{
    public class ImportApi : MiniscriptAPI
    {
        public const string FromType = "type";
        public const string FromValue = "value";
        public const string FromTypeLibrary = "library";
        public const string FromTypeMap = "map";
        public const string FromInto = "target";
        public const string Dirname = "_dirname";
        public const string Filename = "_filename";
        public const string IsFreshImport = "_isFreshImport";
        public const string Exports = "_exports";
        public static readonly ValString ValFromType = new(FromType);
        public static readonly ValString ValFromValue = new(FromValue);
        public static readonly ValString ValFromTypeLibrary = new(FromTypeLibrary);
        public static readonly ValString ValFromTypeMap = new(FromTypeMap);
        public static readonly ValString ValFromInto = new(FromInto);

        private static ValMap importModule;
        private static ValMap fromClass;

        private static readonly Dictionary<string, Dictionary<string, ValFunction>> LazyImportIntrinsicCache = new();
        private static readonly Dictionary<string, Function> CodeCache = new();
        private static readonly HashSet<string> ValidLibrariesCache = new();
        private static readonly Dictionary<string, Value> ImportsCache = new();
        private static readonly Dictionary<string, string> LibrariesMap = new();
        private static readonly HashSet<string> ImportsHistory = new();

        [ApiModuleInitializer]
        private static void Initialize()
        {
            ImportModule(); // note that it's not registered, just called. The only way to access it is via `import`
            FromClass(); // also not registered
            ScriptingManager.OnReset += ClearImportCache;
        }

        public static ValMap ImportModule()
        {
            if (importModule != null) return importModule;
            return importModule = BuildClass(
                VarargMethod(DefaultInvocation.ToString(), 32, ImportFunction(false, false, ImportModule),
                    globalIntrinsicName: "import"),
                VarargMethod("fresh", 32, ImportFunction(true, false, null)),
                VarargMethod("lazy", 32, ImportFunction(false, true, null)),
                Method("export", ExportFunc, "export").Params("target", "force"),
                Method("ensureFresh", EnsureFreshFunc).Param("fresh", ValNumber.Truth(true)),
                Method("defineLib", DefineLibFunc).Params("libname", "path")
            );
        }

        public static ValMap FromClass()
        {
            if (fromClass != null) return fromClass;
            return fromClass = BuildClass(
                Method(DefaultInvocation.ToString(), FromFunc, "from").Param("source"),
                Method("into", IntoFunc).Param("target")
            );
        }

        public static void ClearImportCache()
        {
            CodeCache.Clear();
            ValidLibrariesCache.Clear();
            ImportsCache.Clear();
            LibrariesMap.Clear();
            PopulateLibraries();
        }

        public static (Value type, Value value, ValMap into) UnwrapFrom(ValMap map)
        {
            if (!map.TryGetValue(FromType, out var type) ||
                !map.TryGetValue(FromValue, out var value)) throw new RuntimeException("Invalid 'From' map");
            if (!map.TryGetValue(FromInto, out var into)) into = null;
            if (into is ValNull) into = null;
            if (into != null && into is not ValMap) throw new RuntimeException("Invalid 'From.into' target");
            return (type, value, (ValMap)into);
        }

        private static void PopulateLibraries()
        {
            foreach (var fullPath in new[] { "/sys/lib/" })
            {
                var path = fullPath;
                var disk = DiskController.Instance.GetDisk(ref path);
                var files = disk.GetFileNames(path);
                foreach (var file in files)
                {
                    var filePath = PathUtils.Combine(path, file);
                    if (disk.GetFileInfo(filePath).isDirectory) continue;
                    LibrariesMap.Add(file.Replace(".ms", "").ToLowerInvariant(), PathUtils.Combine(fullPath, file));
                }
            }
        }

        private static Intrinsic.Result ExportFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var target = context.GetLocal("target");
            var forceVal = context.GetLocal("force");
            var force = forceVal != null && forceVal.BoolValue();

            ValString exportName;
            Value exportObject;
            var exportTarget = context.parent.GetVar(Exports);
            if (exportTarget == null)
            {
                exportTarget = new ValMap();
                context.parent.SetVar(Exports, exportTarget);
            }

            if (exportTarget is not ValMap exportMap) throw new RuntimeException($"'{Exports}' is not a map");
            switch (target)
            {
                case ValString valString:
                    exportObject = context.parent.GetVar(valString.ToString());
                    exportName = valString;
                    break;
                case ValMap map when map.IsA(AsApi.AsClass, context.vm):
                    var (source, alias) = AsApi.Unwrap(map);
                    exportObject = source;
                    exportName = alias as ValString;
                    if (exportName == null) throw new RuntimeException("Alias must be a string");
                    break;
                default:
                    throw new RuntimeException("Can't export object without 'as' alias");
            }

            if (exportMap.map.ContainsKey(exportName) && !force)
                throw new RuntimeException($"Member with name {exportName} is already exported");

            exportMap.SetElem(exportName, exportObject);
            return Intrinsic.Result.Null;
        }

        /// <summary>
        ///     Ensures that current module fresh-import status, throwing a Runtime Exception otherwise
        /// </summary>
        private static Intrinsic.Result EnsureFreshFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var requiredFresh = context.GetLocal("fresh");
            var required = requiredFresh != null && requiredFresh.BoolValue();
            var isFreshVal = context.parent.GetVar(IsFreshImport);
            var isFresh = isFreshVal != null && isFreshVal.BoolValue();
            if (isFresh == required) return Intrinsic.Result.Null;
            if (required)
                throw new RuntimeException($"File '{context.parent.GetVar(Filename)}' can only be fresh-imported");
            throw new RuntimeException($"File '{context.parent.GetVar(Filename)}' can't be fresh-imported");
        }

        private static Intrinsic.Result DefineLibFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var libName = context.GetLocal("libname");
            var path = context.GetLocal("path");
            if (libName is not ValString) throw new RuntimeException($"library name must be a string, got {libName}");
            if (path is not ValString) throw new RuntimeException($"Library path must be a string, got {path}");
            var fullPath = FindLib(context, path.ToString());
            LibrariesMap.Add(libName.ToString().ToLowerInvariant(), fullPath);
            return Intrinsic.Result.Null;
        }

        #region importing code

        /// <summary>
        ///     Looks up exact library file path by checking both lib.ms and lib/index.ms
        /// </summary>
        private static string ResolveLibraryFile(string path)
        {
            try
            {
                return ResolveSingleLibraryFile(path);
            }
            catch (Exception e)
            {
                try
                {
                    return ResolveSingleLibraryFile(PathUtils.Combine(path, "index.ms"));
                }
                catch (Exception)
                {
                    throw e;
                }
            }
        }

        private static string ResolveSingleLibraryFile(string path)
        {
            var resolvedPath = DiskUtils.ResolvePath(path, out var error);
            if (resolvedPath == null) throw new RuntimeException(error);
            if (!resolvedPath.EndsWith(".ms")) resolvedPath += ".ms";
            var localPath = resolvedPath;
            if (ValidLibrariesCache.Contains(resolvedPath)) return resolvedPath;
            var disk = DiskController.Instance.GetDisk(ref localPath);
            if (disk == null) throw new RuntimeException($"Disk not found for path {resolvedPath}");
            var fi = disk.GetFileInfo(localPath);
            if (fi == null || fi.isDirectory) throw new RuntimeException($"Path {resolvedPath} is not a file");
            ValidLibrariesCache.Add(resolvedPath);
            return resolvedPath;
        }

        /// <summary>
        ///     Looks up full library path using given import path. Supports either absolute path, relative path, or library
        ///     name
        /// </summary>
        private static string FindLib(TAC.Context context, string libName)
        {
            libName = libName.ToLowerInvariant();
            // absolute path
            if (libName.StartsWith("/")) return ResolveLibraryFile(libName);

            // relative path
            if (libName.StartsWith("./"))
            {
                var callerContext = context.parent;
                var dirname = callerContext.GetVar(Dirname);
                if (dirname is not ValString) throw new RuntimeException($"{Dirname} is corrupted!");
                return ResolveLibraryFile(PathUtils.Combine(dirname.ToString(), libName));
            }

            // library import
            if (LibrariesMap.TryGetValue(libName, out var path) && path != null) return path;
            throw new RuntimeException($"Library {libName} is not found");
        }

        /// <summary>
        ///     Actually loads library code given a full path
        /// </summary>
        private static Function LoadLib(string fullPath, bool freshImport)
        {
            var cacheKey = $"{fullPath}::{freshImport}";
            if (CodeCache.TryGetValue(cacheKey, out var function)) return function;

            var diskPath = fullPath;
            var disk = DiskController.Instance.GetDisk(ref diskPath);
            var code = disk.ReadText(diskPath);

            if (code == null) throw new RuntimeException("import: library not found: " + fullPath);

            code = CreateImport(code, fullPath, freshImport);

            // Now, parse that code, and build a function around it that returns
            // its own locals as its result.  Push a manual call.
            var parser = new Parser
            {
                errorContext = fullPath
            };
            parser.Parse(code);
            return CodeCache[cacheKey] = parser.CreateImport();
        }

        /// <summary>
        ///     Appends technical code to the source file, to ensure full functionality
        /// </summary>
        private static string CreateImport(string code, string fullPath, bool isFresh)
        {
            var dirname = Path.GetDirectoryName(fullPath)?.Replace("\\", "/");
            var sb = new StringBuilder();
            sb.Append($"{Dirname} = \"{dirname}\";");
            sb.Append($"{Filename} = \"{fullPath}\";");
            sb.Append($"{IsFreshImport} = {(isFresh ? "true" : "false")};");
            sb.Append($"{Exports} = null;");
            sb.AppendLine(code);
            sb.AppendLine($"if {Exports} != null then; return {Exports}; end if;");
            return sb.ToString();
        }

        private static Intrinsic.Result FromFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var from = context.GetLocal("source");

            return new Intrinsic.Result(From(context, from));
        }

        private static Intrinsic.Result IntoFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var into = context.GetLocal("target");
            if (into is not ValMap) throw new RuntimeException($"{into} is not a valid from.into target");
            context.self.SetElem(ValFromInto, into);
            return new Intrinsic.Result(context.self);
        }

        private static ValMap From(TAC.Context context, Value from)
        {
            // Multiple implementations:
            ValMap result;
            switch (from)
            {
                case null:
                    return FromClass();
                case ValString libname:
                    result = _From(ValFromTypeLibrary, libname);
                    break;
                case ValMap valMap when valMap.IsA(FromClass(), context.vm):
                    return valMap;
                case ValMap valMap:
                    result = _From(ValFromTypeMap, valMap);
                    break;
                default:
                    throw new TypeException($"Got {from} where string or map was expected");
            }

            result.SetElem(ValFromInto, ValNull.instance);

            return result;
        }

        private static ValMap _From(ValString type, Value value)
        {
            var map = new ValMap();
            map.SetElem(ValString.magicIsA, FromClass());
            map.SetElem(ValFromType, type);
            map.SetElem(ValFromValue, value);
            return map;
        }

        private static IntrinsicCode ImportFunction(bool isFresh, bool isLazy, [CanBeNull] Func<Value> defaultResult)
        {
            return RecursiveMultiStepFunction((TAC.Context context, out Intrinsic.Result result) =>
            {
                result = null;
                var rawArg = context.GetLocal(DefaultVarargsName);
                if (rawArg is not ValList valArguments)
                    throw new RuntimeException("Couldn't find varargs passed to import function");
                var arguments = valArguments.values;
                ValMap from;
                string asAlias;
                Dictionary<string, string> requiredMembers = null;

                switch (arguments.Count)
                {
                    // 0 argument: return module
                    case 0 when defaultResult != null:
                    {
                        result = new Intrinsic.Result(defaultResult());
                        return null;
                    }
                    case 0:
                        throw new RuntimeException("Not enough arguments");
                    // 1 argument: plain old import
                    case 1:
                        ParseFrom(context, arguments[0], out from, out asAlias);
                        break;
                    default:
                    {
                        requiredMembers = new Dictionary<string, string>();
                        ParseFrom(context, arguments[^1], out from, out asAlias);
                        for (var i = 0; i < arguments.Count - 1; i++)
                        {
                            var member = arguments[i];
                            string key;
                            string alias;
                            switch (member)
                            {
                                case ValString:
                                    key = alias = member.ToString();
                                    break;
                                case ValMap map when map.IsA(AsApi.AsClass, context.vm):
                                {
                                    var data = AsApi.Unwrap(map);
                                    if (data.source is not ValString)
                                        throw new RuntimeException("Member name must be a string");
                                    if (data.alias is not ValString)
                                        throw new RuntimeException("Member alias must be a string");
                                    key = data.source.ToString();
                                    alias = data.alias.ToString();
                                    break;
                                }
                                default:
                                    throw new RuntimeException(
                                        "Member import must be either member name string, or member name alias");
                            }

                            if (requiredMembers.ContainsKey(alias))
                                throw new RuntimeException($"Duplicate imported member name: {alias}");
                            requiredMembers.Add(key, alias);
                        }

                        break;
                    }
                }

                var (fromType, fromValue, intoMap) = UnwrapFrom(from);
                intoMap ??= context.parent.GetVar("locals") as ValMap;

                if (fromType == ValFromTypeMap)
                {
                    if (fromValue is not ValMap map)
                        throw new RuntimeException($"Expected map in map-type 'from', got: {fromValue}");
                    if (isFresh) map = map.EvalCopy(context);

                    result = ResolveMembersImports(requiredMembers, MapExtractor(map), asAlias, null, intoMap);
                    return null;
                }

                if (fromType == ValFromTypeLibrary)
                {
                    // Resolve library path first
                    var fullLibPath = FindLib(context, fromValue.ToString());

                    if (!isFresh)
                    {
                        // non-fresh imports look up cache before loading
                        if (ImportsCache.TryGetValue(fullLibPath, out var lib))
                        {
                            result = ResolveMembersImports(requiredMembers, MapExtractor(lib), asAlias,
                                fromValue.ToString(),
                                intoMap);
                            return null;
                        }

                        if (isLazy)
                        {
                            result = ResolveMembersImports(requiredMembers, LazyImportExtractor(fullLibPath), asAlias,
                                fromValue.ToString(),
                                intoMap);
                            return null;
                        }
                    }

                    PushImportHistory(fullLibPath);
                    
                    // Loading library code
                    var code = LoadLib(fullLibPath, isFresh);
                    // Manually pushing call of the imported code to load it
                    context.interpreter.vm.ManuallyPushCall(new ValFunction(code), new ValTemp(0));
                    // ReSharper disable once VariableHidesOuterVariable
                    return (TAC.Context context, out Intrinsic.Result result) =>
                    {
                        // When we're invoked with a partial result, it means that the import
                        // function has finished, and stored its result (the values that were
                        // created by the import code) in Temp 0.
                        var importedValues = context.GetTemp(0) as ValMap;
                        if (!isFresh) ImportsCache[fullLibPath] = importedValues;
                        result = ResolveMembersImports(requiredMembers, MapExtractor(importedValues), asAlias,
                            fromValue.ToString(), intoMap);

                        PopImportHistory(fullLibPath);
                        
                        return null;
                    };
                }

                throw new RuntimeException($"Unknown 'from' source: {fromType}");
            });
        }

        private static Intrinsic.Result ResolveMembersImports(Dictionary<string, string> requiredMembers,
            Func<string, Value> source,
            string asAlias, string defaultName, ValMap into)
        {
            var libName = asAlias ?? defaultName;
            if (requiredMembers == null)
            {
                if (asAlias == null && defaultName == null)
                    throw new RankException("No 'as' alias or imported members are defined");
                TrySetMapValue(into, libName, source(""));
            }
            else
            {
                // if (source is not ValMap map) throw new RuntimeException("Can't import members from non-map value");
                foreach (var (name, alias) in requiredMembers)
                {
                    // if (!map.TryGetValue(name, out var value))
                    //     throw new RuntimeException($"Can't import missing member {name}");
                    TrySetMapValue(into, alias, source(name));
                }

                if (asAlias != null) TrySetMapValue(into, asAlias, source(""));
            }

            return Intrinsic.Result.Null;
        }

        private static void TrySetMapValue(ValMap map, string key, Value value)
        {
            var keyVal = new ValString(key);
            if (map.ContainsKey(keyVal)) throw new RuntimeException($"Can't import into conflicting member {key}");
            map.SetElem(keyVal, value);
        }

        private static void ParseFrom(TAC.Context context, Value value, out ValMap from, out string asAlias)
        {
            if (value is ValMap map && map.IsA(AsApi.AsClass, context.vm))
            {
                var (source, alias) = AsApi.Unwrap(map);
                if (alias is not ValString) throw new RuntimeException("Alias must be a string");
                asAlias = alias.ToString();
                from = From(context, source);
            }
            else
            {
                asAlias = null;
                from = From(context, value);
            }
        }


        private static Func<string, Value> MapExtractor(Value source) => key => GetOrThrow(source, key);

        private static Value GetOrThrow(Value maybeMap, string key)
        {
            if (string.IsNullOrEmpty(key)) return maybeMap;
            if (maybeMap is not ValMap map) throw new RuntimeException("Can't import members from non-map value");
            if (!map.TryGetValue(key, out var value))
                throw new RuntimeException($"Can't import missing member {key}");
            return value;
        }

        private static Func<string, Value> LazyImportExtractor(string fullLibPath)
        {
            if (!LazyImportIntrinsicCache.TryGetValue(fullLibPath, out var map))
            {
                map = LazyImportIntrinsicCache[fullLibPath] = new Dictionary<string, ValFunction>();
            }

            return name =>
            {
                if (!map.TryGetValue(name, out var function))
                {
                    function = CreateLazyImportFunction(fullLibPath, name);
                }

                return function;
            };
        }

        private static ValFunction CreateLazyImportFunction(string fullLibPath, string member)
        {
            var i = Intrinsic.Create("");
            i.code = (context, partialResult) =>
            {
                if (partialResult != null)
                {
                    // When we're invoked with a partial result, it means that the import
                    // function has finished, and stored its result (the values that were
                    // created by the import code) in Temp 0.
                    var importedValues = context.GetTemp(0) as ValMap;
                    ImportsCache[fullLibPath] = importedValues;
                    
                    PopImportHistory(fullLibPath);

                    return new Intrinsic.Result(importedValues);
                }

                if (ImportsCache.TryGetValue(fullLibPath, out var lib))
                {
                    return new Intrinsic.Result(GetOrThrow(lib, member));
                }
                
                PushImportHistory(fullLibPath);
                
                // Loading library code
                var code = LoadLib(fullLibPath, false);
                // Manually pushing call of the imported code to load it
                context.interpreter.vm.ManuallyPushCall(new ValFunction(code), new ValTemp(0));
                return new Intrinsic.Result(UnsetParameter, false);
            };
            return i.GetFunc();
        }

        private static void PushImportHistory(string fullLibPath)
        {
            if (ImportsHistory.Contains(fullLibPath))
                throw new RuntimeException($"Circular reference detected while importing \"{fullLibPath}\", try using Lazy imports");
            ImportsHistory.Add(fullLibPath);
        }
        private static void PopImportHistory(string fullLibPath)
        {
            if (!ImportsHistory.Remove(fullLibPath))
                throw new Exception("Popped library is not the same as the pushed one, this is backend issue and should never happen");
        }
        #endregion
    }
}
