using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ThingsEditor.IO.Filesystem
{
    internal class DiskController
    {
        public static readonly DiskController Instance = new();
        private static readonly Disk SystemDisk = new ResourcesDisk();
        private readonly Dictionary<string, Disk> disks = new();

        private long playerID;

        private static string GetDiskName(string path)
        {
            if (path.Length < 1 || path[0] != '/') return null;
            var diskName = path[1..];
            var slashPos = diskName.IndexOf('/');
            if (slashPos >= 0) diskName = diskName[..slashPos];
            // ModEntry.instance.Monitor.Log($"Returning diskName: {diskName}");
            return diskName;
        }

        public static DiskController GetCurrentDiskController()
        {
            return Instance;
        }

        public static string[] GetCurrentDiskNames()
        {
            return GetCurrentDiskController().GetDiskNames();
        }

        public static Disk GetCurrentDisk(ref string path)
        {
            return GetCurrentDiskController().GetDisk(ref path);
        }

        /// <summary>
        ///     Find the disk indicated by the given full path, and then strip that
        ///     off, returning the rest of the path.  If the disk is not found,
        ///     return null.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public Disk GetDisk(ref string path)
        {
            // ModEntry.instance.Monitor.Log($"Getting disk for: {path}");
            var diskName = GetDiskName(path);

            if (path.Length <= diskName.Length + 2) path = "";
            else path = path[(diskName.Length + 2)..];

            if (diskName == "sys") return SystemDisk;

            foreach (var volName in
                     disks.Keys) //ModEntry.instance.Monitor.Log("Checking " + volName + " -> " + disks[volName] + " against " + diskName);
                if (diskName ==
                    volName) //ModEntry.instance.Monitor.Log("Matches " + disks[volName] + " with remainder " + path);
                    return disks[volName];
            return null;
        }

        public string[] GetDiskNames()
        {
            var diskNames = disks.Keys.ToList();
            diskNames.Add("sys");
            return diskNames.ToArray();
        }

        public void AddDisk(string diskName, Disk disk)
        {
            disks[diskName] = disk;
        }

        public bool Exists(string path)
        {
            var disk = GetDisk(ref path);
            return disk != null && disk.Exists(path);
        }

        public FileInfo GetInfo(string path)
        {
            var disk = GetDisk(ref path);
            // ModEntry.instance.Monitor.Log($"Getting info for: {path}");
            return disk?.GetFileInfo(path);
        }

        /// <summary>
        ///     Delete the given file.
        /// </summary>
        /// <param name="path">file to delete</param>
        /// <returns>null if successful, error message otherwise</returns>
        public string Delete(string path)
        {
            var disk = GetDisk(ref path);
            if (disk == null) return "Error: disk not found";
            if (!disk.IsWriteable()) return "Error: disk not writeable";
            disk.Delete(path, out var err);
            return err;
        }

        /// <summary>
        ///     Move or copy a file from one place to another.
        /// </summary>
        /// <param name="oldPath">path of source file</param>
        /// <param name="newPath">destination path or directory</param>
        /// <param name="deleteSource">whether to delete source after copy (i.e. move) if possible</param>
        /// <param name="overwriteDest">whether to overwrite an existing file at the destination</param>
        /// <returns>null if successful, or error string if an error occurred</returns>
        public string MoveOrCopy(string oldPath, string newPath, bool deleteSource, bool overwriteDest)
        {
            if (newPath == oldPath) return null; // nothing to do
            var oldDisk = GetDisk(ref oldPath);
            if (oldDisk == null) return "Error: source disk not found";
            var newDisk = GetDisk(ref newPath);
            if (newDisk == null) return "Error: target disk not found";
            if (!newDisk.IsWriteable()) return "Error: target disk is not writeable";
            if (string.Equals(newPath, oldPath, StringComparison.InvariantCultureIgnoreCase))
            {
                // The file names (or paths) differ only in case.  This is tricky to handle
                // correctly.  On a case-insensitive file system (like most Mac and Windows
                // machines), we may be just changing the case of an existing file.  But
                // on a case-insensitive system, it may be making a new file.  Unfortunately
                // there is no easy way to tell what sort of file system we are even on.
            }
            else
            {
                if (newDisk.Exists(newPath, out var isDir))
                {
                    if (isDir) newPath = PathUtils.Combine(newPath, Path.GetFileName(oldPath));
                    else if (!overwriteDest) return "Error: target file al ready exists";
                }
            }

            try
            {
                var data = oldDisk.ReadBinary(oldPath);
                if (deleteSource)
                    oldDisk.Delete(oldPath, out _); // (it's actually OK if we can't delete the original)
                newDisk.WriteBinary(newPath, data);
                return null; // Success!
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public string Mount(string fromPath, string diskName)
        {
            if (diskName.Contains("/")) return "Error: Disk name can't contain forward slashes";

            if (diskName is "." or "..") return "Error: Disc name can't be . or ..";

            var fullPath = fromPath;
            var disc = GetDisk(ref fromPath);
            if (disc == null) return $"Error: Path {fullPath} points to missing disc";

            var newDisk = disc.Mount(fromPath, diskName, out var errMsg);
            if (newDisk == null) return errMsg;
            AddDisk(diskName, newDisk);
            return null;
        }
    }
}
