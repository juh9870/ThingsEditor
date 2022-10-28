using System;
using System.Collections.Generic;
using SimpleFileBrowser;

namespace ThingsEditor.IO.Filesystem
{
    internal class RealFileDisk : Disk
    {
        private readonly string basePath; // our real (native) base path
        private bool readOnly; // if true, this disk is write protected

        /// <summary>
        ///     Constructs real dile disc using path provided by FileBrowser
        /// </summary>
        /// <param name="basePath">FileBrowser path to the folder</param>
        public RealFileDisk(string basePath)
        {
            this.basePath = basePath;
            //ModEntry.instance.Monitor.Log("Set base path to: " + this.basePath);
            if (!FileBrowserHelpers.DirectoryExists(this.basePath))
                throw new ArgumentException("Can't create disc based on missing folder");
        }

        // private string NativePath(string path)
        // {
        //     if (!path.StartsWith(basePath)) path = Path.Combine(basePath, path);
        //
        //     path = Path.GetFullPath(path);
        //     if (!path.StartsWith(basePath)) throw new ArgumentException();
        //
        //     return path;
        // }

        /// <summary>
        ///     Get a list of files in the given directory (which must end in "/").
        ///     Returns just the names (not paths) of files immediately within the
        ///     given directory.
        /// </summary>
        public override List<string> GetFileNames(string dirPath)
        {
            var path = PathUtils.GetReadPath(basePath, dirPath, PathUtils.Mode.Directory, out _);
            if (path == null) return null;
            var items = FileBrowserHelpers.GetEntriesInDirectory(path, true);
            var names = new List<string>(items.Length);
            for (var index = 0; index < items.Length; index++) names.Add(items[index].Name);

            return names;
        }

        public override FileInfo GetFileInfo(string filePath)
        {
            filePath = PathUtils.GetReadPath(basePath, filePath, PathUtils.Mode.Any, out _);
            if (filePath == null) return null;

            if (FileBrowserHelpers.DirectoryExists(filePath))
            {
                var date = FileBrowserHelpers.GetLastModifiedDate(filePath);
                return new FileInfo
                {
                    date = date.ToString("yyyy-MM-dd HH:mm:ss"),
                    isDirectory = true
                };
            }

            if (FileBrowserHelpers.FileExists(filePath))
            {
                var date = FileBrowserHelpers.GetLastModifiedDate(filePath);
                var size = FileBrowserHelpers.GetFilesize(filePath);
                return new FileInfo
                {
                    date = date.ToString("yyyy-MM-dd HH:mm:ss"),
                    size = size,
                    isDirectory = false
                };
            }

            return null;
        }

        /// <summary>
        ///     Read the given text file as a string.
        /// </summary>
        /// <param name="filePath"></param>
        public override string ReadText(string filePath)
        {
            try
            {
                var path = PathUtils.GetReadPath(basePath, filePath, PathUtils.Mode.File, out _);
                if (path == null) return null;
                return FileBrowserHelpers.ReadTextFromFile(path);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Read the given file as a binary data.
        /// </summary>
        /// <param name="filePath"></param>
        public override byte[] ReadBinary(string filePath)
        {
            var path = PathUtils.GetReadPath(basePath, filePath, PathUtils.Mode.File, out _);
            if (path == null) return null;
            return FileBrowserHelpers.ReadBytesFromFile(path);
        }

        public override Disk Mount(string path, string diskName, out string errMsg)
        {
            var newPath = PathUtils.GetWritePath(basePath, path, true, out errMsg);
            var disk = new RealFileDisk(newPath);
            if (readOnly) disk.readOnly = true;
            return disk;
        }

        /// <summary>
        ///     Return whether this disk can be written to.
        /// </summary>
        public override bool IsWriteable()
        {
            return !readOnly;
        }

        /// <summary>
        ///     Write the given text to a file.
        /// </summary>
        public override void WriteText(string filePath, string text)
        {
            if (readOnly) return;
            var path = PathUtils.GetWritePath(basePath, filePath, false, out _);
            if (path == null) return;
            FileBrowserHelpers.WriteTextToFile(path, text);
        }

        /// <summary>
        ///     Write the given binary data to a file.
        /// </summary>
        public override void WriteBinary(string filePath, byte[] data)
        {
            if (readOnly) return;
            var path = PathUtils.GetWritePath(basePath, filePath, false, out _);
            if (path == null) return;
            FileBrowserHelpers.WriteBytesToFile(path, data);
        }

        /// <summary>
        ///     Delete the given file.
        /// </summary>
        public override bool MakeDir(string dirPath, out string errMsg)
        {
            if (readOnly)
            {
                errMsg = "Disk not writeable";
                return false;
            }

            dirPath = PathUtils.GetWritePath(basePath, dirPath, true, out errMsg);
            if (dirPath == null) return false;
            var parent = FileBrowserHelpers.GetDirectoryName(dirPath);
            var name = FileBrowserHelpers.GetFilename(dirPath);

            try
            {
                FileBrowserHelpers.CreateFolderInDirectory(parent, name);
                errMsg = null;
                return true;
            }
            catch (Exception e)
            {
                errMsg = e.Message;
                return false;
            }
        }

        /// <summary>
        ///     Delete the given file.
        /// </summary>
        public override bool Delete(string filePath, out string errMsg)
        {
            if (readOnly)
            {
                errMsg = "Disk not writeable";
                return false;
            }

            filePath = PathUtils.GetReadPath(basePath, filePath, PathUtils.Mode.Any, out errMsg);
            if (filePath == null) return false;
            var info = GetFileInfo(filePath);
            if (info == null)
            {
                errMsg = "File not found";
                return false;
            }

            try
            {
                if (info.isDirectory)
                    FileBrowserHelpers.DeleteDirectory(filePath);
                else
                    FileBrowserHelpers.DeleteFile(filePath);
                errMsg = null;
                return true;
            }
            catch (Exception e)
            {
                errMsg = e.Message;
                return false;
            }
        }
    }
}