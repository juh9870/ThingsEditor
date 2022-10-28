using System;
using System.Collections.Generic;
using System.IO;
using Miniscript;
using ThingsEditor.IO.Filesystem;

namespace ThingsEditor.Scripting.Modules
{
    public class FSApi : MiniscriptAPI
    {
        private static ValMap fileModule;

        private static ValMap fileHandleClass;

        [ApiModuleInitializer]
        private static void Initialize()
        {
            RegisterClass("file", FileModule);
            RegisterClass("FileHandle", FileHandleClass);
        }

        protected static IntrinsicBuilder PathMethod(string name, IntrinsicCode code = null,
            string globalIntrinsicName = "")
        {
            return Method(name, code, globalIntrinsicName).Param("path", "");
        }

        public static ValMap FileModule()
        {
            if (fileModule != null) return fileModule;
            fileModule = BuildClass(
                PathMethod("makedir", MakeDirFunc),
                PathMethod("children", ChildrenFunc),
                PathMethod("name", NameFunc),
                PathMethod("parent", ParentFunc),
                PathMethod("exists", ExistsFunc),
                PathMethod("info", InfoFunc),
                Method("child", ChildFunc).StringParams("basePath", "subpath"),
                PathMethod("delete", DeleteFunc),
                Method("move", MoveFunc).StringParams("oldPath", "newPath"),
                Method("copy", CopyFunc).StringParams("oldPath", "newPath"),
                PathMethod("open", OpenFunc).Param("mode", "rw+"),
                PathMethod("readLines", ReadLinesFunc),
                PathMethod("writeLines", WriteLinesFunc).Param("lines"),
                PathMethod("mount", MountFunc).Param("diskName")
            );

            return fileModule;
        }

        public static ValMap FileHandleClass()
        {
            if (fileHandleClass != null) return fileHandleClass;

            fileHandleClass = BuildClass(
                SelfMethod("isOpen", IsOpenFunc),
                SelfMethod("position", PositionFunc),
                SelfMethod("atEnd", AtEndFunc),
                SelfMethod("write", WriteFunc).Param("s", ""),
                SelfMethod("writeLine", WriteLineFunc).Param("s", ""),
                SelfMethod("read", ReadFunc).Param("codePointCount", ""),
                SelfMethod("readLine", ReadLineFunc),
                SelfMethod("close", CloseFunc)
            );
            return fileHandleClass;
        }

        /// <summary>
        ///     Helper method to find the OpenFile referred to by a method on
        ///     a FileHandle object.  Returns the object, or null and sets error.
        /// </summary>
        private static OpenFile GetOpenFile(TAC.Context context, out string err)
        {
            err = null;
            var self = context.GetVar("self") as ValMap;
            self!.TryGetValue("_handle", out var handle);
            if (handle is not ValWrapper wrapper)
            {
                err = "Error: file handle invalid";
                return null;
            }

            var result = wrapper.content as OpenFile;
            if (result == null) err = "Error: file handle not set";
            return result;
        }

        #region File module methods

        private static Intrinsic.Result MakeDirFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var path = context.GetLocalString("path");

            path = DiskUtils.ResolvePath(path, out var err);
            if (path == null) return new Intrinsic.Result(err);

            var disk = DiskController.Instance.GetDisk(ref path);
            if (!disk.IsWriteable()) return new Intrinsic.Result("Error: disk is not writeable");
            if (!path.EndsWith("/")) path += "/";
            if (disk.Exists(path)) return new Intrinsic.Result("Error: file already exists");
            disk.MakeDir(path, out err);
            if (err == null) return Intrinsic.Result.Null;
            return new Intrinsic.Result(err);
        }

        private static Intrinsic.Result ChildrenFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var path = context.GetLocalString("path");
            path = DiskUtils.ResolvePath(path, out var err);
            if (path == null) return new Intrinsic.Result(err);
            if (path == "/")
            {
                // Special case: listing the disks.
                var disks = new List<Value>();
                var diskNames = new List<string>(DiskController.Instance.GetDiskNames());
                diskNames.Sort();
                foreach (var name in diskNames) disks.Add(new ValString("/" + name));

                return new Intrinsic.Result(new ValList(disks));
            }

            var disk = DiskController.Instance.GetDisk(ref path);
            if (disk == null) return Intrinsic.Result.Null;
            var result = disk.GetFileNames(path).ToValue();
            return new Intrinsic.Result(result);
        }

        private static Intrinsic.Result NameFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var path = context.GetLocalString("path");
            path = DiskUtils.ResolvePath(path, out var err);
            if (path == null) return new Intrinsic.Result(err);
            return new Intrinsic.Result(Path.GetFileName(path));
        }

        private static Intrinsic.Result ParentFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var path = context.GetLocalString("path");
            path = DiskUtils.ResolvePath(path, out var err);
            if (path == null) return new Intrinsic.Result(err);
            var pos = path.LastIndexOf("/", StringComparison.Ordinal);
            if (pos == 0) return new Intrinsic.Result("/");
            if (pos < 0) return Intrinsic.Result.Null;
            return new Intrinsic.Result(path[..pos]);
        }

        private static Intrinsic.Result ExistsFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var path = context.GetLocalString("path");
            path = DiskUtils.ResolvePath(path, out var err);
            if (path == null) return new Intrinsic.Result(err);
            return DiskController.Instance.Exists(path) ? Intrinsic.Result.True : Intrinsic.Result.False;
        }

        private static Intrinsic.Result InfoFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var path = context.GetLocalString("path");
            path = DiskUtils.ResolvePath(path, out var err);
            if (path == null) return new Intrinsic.Result(err);
            var info = DiskController.Instance.GetInfo(path);
            if (info == null) return Intrinsic.Result.Null;
            var result = new ValMap
            {
                ["path"] = new ValString(path),
                ["isDirectory"] = ValNumber.Truth(info.isDirectory),
                ["size"] = new ValNumber(info.size),
                ["date"] = new ValString(info.date),
                ["comment"] = new ValString(info.comment)
            };
            return new Intrinsic.Result(result);
        }

        private static Intrinsic.Result ChildFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var basePath = context.GetLocalString("basePath");
            var subpath = context.GetLocalString("subpath");
            return new Intrinsic.Result(PathUtils.Combine(basePath, subpath));
        }

        private static Intrinsic.Result DeleteFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var path = context.GetLocalString("path");
            path = DiskUtils.ResolvePath(path, out var err);
            if (path == null) return new Intrinsic.Result(err);
            err = DiskController.Instance.Delete(path);
            if (err == null) return Intrinsic.Result.Null;
            return new Intrinsic.Result(err);
        }

        private static Intrinsic.Result MoveFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var oldPath = context.GetLocalString("oldPath");
            oldPath = DiskUtils.ResolvePath(oldPath, out var err);
            if (oldPath == null) return new Intrinsic.Result(err);

            var newPath = context.GetLocalString("newPath");
            newPath = DiskUtils.ResolvePath(newPath, out err);
            if (newPath == null) return new Intrinsic.Result(err);

            err = DiskController.Instance.MoveOrCopy(oldPath, newPath, true, false);
            if (err == null) return Intrinsic.Result.Null;
            return new Intrinsic.Result(err);
        }

        private static Intrinsic.Result CopyFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var oldPath = context.GetLocalString("oldPath");
            oldPath = DiskUtils.ResolvePath(oldPath, out var err);
            if (oldPath == null) return new Intrinsic.Result(err);

            var newPath = context.GetLocalString("newPath");
            newPath = DiskUtils.ResolvePath(newPath, out err);
            if (newPath == null) return new Intrinsic.Result(err);

            err = DiskController.Instance.MoveOrCopy(oldPath, newPath, false, false);
            if (err == null) return Intrinsic.Result.Null;
            return new Intrinsic.Result(err);
        }

        private static Intrinsic.Result OpenFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var path = context.GetLocalString("path");
            var mode = context.GetLocalString("mode").ToLower();
            if (mode.Contains("b")) return new Intrinsic.Result("Error: binary mode not supported");
            path = DiskUtils.ResolvePath(path, out var err);
            if (path == null) return new Intrinsic.Result(err);
            if (mode is "r" or "r+" && !DiskController.Instance.Exists(path))
                return new Intrinsic.Result("Error: file not found");
            var file = new OpenFile(DiskController.Instance, path, mode);
            var result = new ValMap();
            result.SetElem(ValString.magicIsA, FileHandleClass());
            result.map[Handle] = new ValWrapper(file);
            result.assignOverride = (key, value) =>
            {
                if (key.ToString() == "position") file.position = value.IntValue();
                return true;
            };
            return new Intrinsic.Result(result);
        }

        private static Intrinsic.Result ReadLinesFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var path = context.GetLocalString("path");
            path = DiskUtils.ResolvePath(path, out var err);
            if (path == null) return new Intrinsic.Result(err);
            var disk = DiskController.Instance.GetDisk(ref path);
            if (disk == null) return Intrinsic.Result.Null;
            var result = disk.ReadLines(path).ToValue();
            return new Intrinsic.Result(result);
        }

        private static Intrinsic.Result WriteLinesFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var origPath = context.GetLocalString("path");
            var linesVal = context.GetLocal("lines");
            if (linesVal == null) return new Intrinsic.Result("Error: lines parameter is required");

            var path = DiskUtils.ResolvePath(origPath, out var err);
            if (path == null) return new Intrinsic.Result(err);

            try
            {
                var disk = DiskController.Instance.GetDisk(ref path);
                if (!disk.IsWriteable()) return new Intrinsic.Result("Error: disk is not writeable");
                disk.WriteLines(path, linesVal.ToStrings());
            }
            catch (Exception)
            {
                return new Intrinsic.Result("Error: unable to write " + origPath);
            }

            return Intrinsic.Result.Null;
        }

        private static Intrinsic.Result MountFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var path = context.GetLocalString("path");
            path = DiskUtils.ResolvePath(path, out var err);
            if (path == null) return new Intrinsic.Result(err);
            var mountTo = context.GetLocalString("diskName");
            if (mountTo == null) return new Intrinsic.Result("Invalid disc name");
            err = DiskController.Instance.Mount(path, mountTo);
            if (err == null) return Intrinsic.Result.Null;
            return new Intrinsic.Result(err);
        }

        #endregion

        #region FileHandle class methods

        private static Intrinsic.Result IsOpenFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var file = GetOpenFile(context, out var err);
            if (err != null) return new Intrinsic.Result(err);
            if (file is not { isOpen: true }) return Intrinsic.Result.False;
            return Intrinsic.Result.True;
        }

        private static Intrinsic.Result PositionFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var file = GetOpenFile(context, out var _);
            if (file == null) return Intrinsic.Result.False;
            return new Intrinsic.Result(file.position);
        }

        private static Intrinsic.Result AtEndFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var file = GetOpenFile(context, out var _);
            if (file is not { isAtEnd: true }) return Intrinsic.Result.False;
            return Intrinsic.Result.True;
        }

        private static Intrinsic.Result WriteFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var file = GetOpenFile(context, out var err);
            if (err != null) return new Intrinsic.Result(err);
            var s = context.GetLocalString("s");
            file.Write(s);
            if (file.error == null) return Intrinsic.Result.Null;
            return new Intrinsic.Result(file.error);
        }

        private static Intrinsic.Result WriteLineFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var file = GetOpenFile(context, out var err);
            if (err != null) return new Intrinsic.Result(err);
            var s = context.GetLocalString("s");
            file.Write(s);
            file.Write("\n");
            if (file.error == null) return Intrinsic.Result.Null;
            return new Intrinsic.Result(file.error);
        }

        private static Intrinsic.Result ReadFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var file = GetOpenFile(context, out var err);
            if (err != null) return Intrinsic.Result.Null;
            string s;
            var count = context.GetLocal("codePointCount");
            if (count == null) s = file.ReadToEnd();
            else s = file.ReadChars(count.IntValue());
            return new Intrinsic.Result(s);
        }

        private static Intrinsic.Result ReadLineFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var file = GetOpenFile(context, out var err);
            if (err != null) return Intrinsic.Result.Null;
            var s = file.ReadLine();
            return new Intrinsic.Result(s);
        }

        private static Intrinsic.Result CloseFunc(TAC.Context context, Intrinsic.Result partialResult)
        {
            var file = GetOpenFile(context, out var err);
            if (err != null) return new Intrinsic.Result(err);
            if (file != null) file.Close();
            return Intrinsic.Result.Null;
        }

        #endregion
    }
}