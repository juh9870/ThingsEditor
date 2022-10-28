using System;

namespace ThingsEditor.IO.Filesystem
{
    internal static class DiskUtils
    {
        /// <summary>
        ///     Given an absolute path, expand it to a full path, and resolve
        ///     any <c>.</c> and <c>..</c> entries in it to get a proper full
        ///     path. If the path is invalid, return null and set error.
        /// </summary>
        /// <param name="path">absolute path to resolve</param>
        /// <param name="error">error output</param>
        public static string ResolvePath(string path, out string error)
        {
            if (path.StartsWith("/")) return PathUtils.CollapsePath(path, out error);
            error = $"Relative paths are not supported. Tried to access {path}";
            return null;
        }

        /// <summary>
        ///     Given a (possibly partial) path, expand it to a full path from our
        ///     current working directory, and resolve any <c>.</c> and <c>..</c>
        ///     entries in it to get a proper full path. If the path is invalid,
        ///     return null and set error.
        ///     <param name="curdir">current working directory</param>
        ///     <param name="path">relative path to resolve</param>
        ///     <param name="error">error output</param>
        /// </summary>
        public static string ResolvePath(string curdir, string path, out string error)
        {
            if (!path.StartsWith("/")) path = PathUtils.Combine(curdir, path);
            return PathUtils.CollapsePath(path, out error);
        }

        private static MemoryDirectory BuildMemorySubDirectory(RealFileDisk disk, string dirPath, FileInfo dirInfo)
        {
            MemoryDirectory subDir = new()
            {
                DirInfo = dirInfo
            };
            foreach (var filename in disk.GetFileNames(dirPath))
            {
                var filePath = PathUtils.Combine(dirPath, filename);
                var fileInfo = disk.GetFileInfo(filePath);
                if (fileInfo.isDirectory)
                    subDir.Subdirectories.Add(filename, BuildMemorySubDirectory(disk, filePath, fileInfo));
                else
                    subDir.Files.Add(filename, Tuple.Create(fileInfo, disk.ReadBinary(filePath)));
            }

            return subDir;
        }

        public static MemoryDirectory BuildMemoryDirectory(this RealFileDisk disk)
        {
            MemoryDirectory memDir = new();
            // Get root directory entries
            foreach (var filename in disk.GetFileNames(""))
            {
                var fileInfo = disk.GetFileInfo(filename);
                if (fileInfo.isDirectory)
                    memDir.Subdirectories.Add(filename, BuildMemorySubDirectory(disk, filename, fileInfo));
                else
                    memDir.Files.Add(filename, Tuple.Create(fileInfo, disk.ReadBinary(filename)));
            }

            return memDir;
        }
    }
}
