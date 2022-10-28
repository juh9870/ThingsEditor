using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SimpleFileBrowser;

namespace ThingsEditor.IO.Filesystem
{
    public static class PathUtils
    {
        public enum Mode
        {
            Any,
            File,
            Directory
        }


        /// <summary>
        ///     Combines two path strings. Works in the same way as Path.Combine, but only supports forward slash as a path
        ///     separator and doesn't perform any checks
        /// </summary>
        /// <param name="path1"></param>
        /// <param name="path2"></param>
        /// <returns></returns>
        public static string Combine(string path1, string path2)
        {
            if (string.IsNullOrEmpty(path2))
                return path1;

            if (string.IsNullOrEmpty(path1))
                return path2;

            if (path2[0] == '/')
                return path2;

            var ch = path1[^1];
            if (ch != '/')
                return path1 + '/' + path2;
            return path1 + path2;
        }

        /// <summary>
        ///     SAF-friendly way of resolving read path on device
        /// </summary>
        /// <param name="basePath">base directory, system will make sure no writes or reads will be performed outside of it</param>
        /// <param name="relativePath">path relative to base directory</param>
        /// <param name="mode">Reading mode, Set to any if you accept both files and directories</param>
        /// <param name="err">error output</param>
        /// <returns>resolved path, or null if resolving failed</returns>
        public static string GetReadPath(string basePath, string relativePath, Mode mode, out string err)
        {
            relativePath = CollapsePath(relativePath, out err);
            if (!string.IsNullOrEmpty(err)) return null;
            var resolved = ResolvePath(basePath, relativePath, false, mode, out err);
            if (resolved == null || FileBrowserHelpers.IsPathChildOfAnother(resolved, basePath) || resolved == basePath)
                return resolved;
            err = "Error: Can't access files outside of base directory";
            return null;
        }

        /// <summary>
        ///     SAF-friendly way of resolving write path on device
        ///     <br />
        ///     Note, that calling this WILL create an empty file/folder on a device at the requested path
        /// </summary>
        /// <param name="basePath">base directory, system will make sure no writes or reads will be performed outside of it</param>
        /// <param name="relativePath">path relative to base directory</param>
        /// <param name="isDirectory">set to true if you want to write a directory, or false for files</param>
        /// <param name="err">error output</param>
        /// <returns>resolved path, or null if resolving failed</returns>
        public static string GetWritePath(string basePath, string relativePath, bool isDirectory, out string err)
        {
            relativePath = CollapsePath(relativePath, out err);
            if (!string.IsNullOrEmpty(err)) return null;
            var resolved = ResolvePath(basePath, relativePath, true, isDirectory ? Mode.Directory : Mode.File,
                out err);
            if (resolved == null || FileBrowserHelpers.IsPathChildOfAnother(resolved, basePath) || resolved == basePath)
                return resolved;
            err = "Error: Can't access files outside of base directory";
            return null;
        }

        /// <summary>
        ///     Given a path, expand it to a full path from our current working
        ///     directory, and resolve any . and .. entries in it to get a proper
        ///     full path. If the path is invalid, return null and set error.
        /// </summary>
        public static string CollapsePath(string path, out string error)
        {
            error = null;
            // Simplify and then validate our full path.
            var parts = new List<string>(path.Split(new[] { '/' }));
            for (var i = 1; i < parts.Count; i++)
                switch (parts[i])
                {
                    case ".":
                        // indicates current directory -- skip this
                        parts.RemoveAt(i);
                        i--;
                        break;
                    // go up one level (error if we're at the root)
                    case ".." when i == 1:
                        error = "Invalid path";
                        return null;
                    case "..":
                        parts.RemoveAt(i);
                        parts.RemoveAt(i - 1);
                        i -= 2;
                        break;
                }

            path = string.Join("/", parts.ToArray());
            return path;
        }

        /// <summary>
        ///     Resolves given path to a directory in a SAF-friendly manner
        /// </summary>
        [CanBeNull]
        private static string ResolvePath(string curdir, string path, bool write, Mode mode, out string err)
        {
            err = null;
            if (path.StartsWith("/")) path = path[1..];
            if (path.EndsWith("/")) path = path[..^1];
            if (path == "")
            {
                if (mode != Mode.File) return curdir;
                err = "Tried to read root folder as a file";
                return null;
            }

            var parts = path.Split(new[] { '/' });
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part is "" or "." or ".." || string.IsNullOrWhiteSpace(part))
                {
                    err = "Path is not collapsed";
                    return null;
                }

                // We have to iterate trough all children to find our file
                var entries = FileBrowserHelpers.GetEntriesInDirectory(curdir, false);
                var found = false;
                foreach (var entry in entries)
                {
                    if (!string.Equals(entry.Name, part, StringComparison.InvariantCultureIgnoreCase)) continue;

                    // If file exists and isn't a directory, and we aren't at the end of our path, or required type is a directory
                    if (!entry.IsDirectory && (mode == Mode.Directory || i != parts.Length - 1))
                    {
                        // In write mode, throw an error. Otherwise return null
                        if (write) err = $"Can't resolve path {path} on {curdir}";
                        return null;
                    }

                    curdir = entry.Path;
                    found = true;
                    break;
                }

                if (found) continue;
                // If file/directory is missing and we aren't in write mode, then path i not found
                if (!write) return null;
                if (i == parts.Length - 1 && mode == Mode.File)
                    curdir = FileBrowserHelpers.CreateFileInDirectory(curdir, part);
                else if (mode == Mode.Directory)
                    curdir = FileBrowserHelpers.CreateFolderInDirectory(curdir, part);
                else throw new Exception("Tried to write file in unspecified mode");
            }

            return curdir;
        }
    }
}