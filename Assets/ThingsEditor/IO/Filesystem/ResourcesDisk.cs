using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace ThingsEditor.IO.Filesystem
{
    internal class ResourcesDisk : Disk
    {
        public const string InfoJsonPath = "sysdisk_map";
        private static readonly Directory GlobalRoot = new();
        private readonly Directory root;

        static ResourcesDisk()
        {
            var item = Resources.Load(InfoJsonPath);
            if (item is not TextAsset json) throw new Exception("File system info json is missing!");

            var files = JsonConvert.DeserializeObject<Dictionary<string, ResourcesEntry>>(json.text)!;
            foreach (var (path, value) in files) GlobalRoot.Entry(GetPath(path), () => new CachedEntry(value));
        }

        public ResourcesDisk() : this(GlobalRoot) { }

        private ResourcesDisk(Directory root)
        {
            this.root = root;
        }

        [CanBeNull]
        private Directory GetDirectory(string path)
        {
            var entry = root.Entry(GetPath(path));
            return entry as Directory;
        }

        [CanBeNull]
        private CachedEntry GetFile(string path)
        {
            var entry = root.Entry(GetPath(path));
            return entry as CachedEntry;
        }

        public override List<string> GetFileNames(string dirPath)
        {
            var dir = GetDirectory(dirPath);
            if (dir == null) return null;
            var files = new List<string>();
            files.AddRange(dir.Files.Keys);
            files.AddRange(dir.Subdirectories.Keys);
            return files;
        }

        public override FileInfo GetFileInfo(string filePath)
        {
            var entry = root.Entry(GetPath(filePath));
            return entry?.Info;
        }

        public override string ReadText(string filePath)
        {
            return GetFile(filePath)?.ReadString();
        }

        public override byte[] ReadBinary(string filePath)
        {
            return GetFile(filePath)?.ReadBytes();
        }

        public override Disk Mount(string path, string diskName, out string errMsg)
        {
            try
            {
                errMsg = null;
                var dir = GetDirectory(path);
                return new ResourcesDisk(dir);
            }
            catch (Exception e)
            {
                errMsg = e.Message;
                return null;
            }
        }

        private static List<string> GetPath(string path)
        {
            return path.ToLowerInvariant().Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private interface IFsEntry
        {
            public FileInfo Info { get; }
        }

        private class Directory : IFsEntry
        {
            public readonly Dictionary<string, CachedEntry> Files = new();
            public readonly Dictionary<string, Directory> Subdirectories = new();

            public FileInfo Info { get; } = new()
            {
                isDirectory = true,
                date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            public IFsEntry Entry(IList<string> path, Func<IFsEntry> createIfNotExists = null)
            {
                if (path.Count == 0) return this;
                var next = path[0];
                if (path.Count == 1)
                {
                    if (createIfNotExists != null)
                    {
                        if (Files.ContainsKey(next) || Subdirectories.ContainsKey(next))
                            throw new Exception($"Duplicate path found: {next}");

                        var data = createIfNotExists();
                        switch (data)
                        {
                            case Directory directory:
                                Subdirectories.Add(next, directory);
                                break;
                            case CachedEntry entry:
                                Files.Add(next, entry);
                                break;
                        }

                        return data;
                    }

                    {
                        if (Files.TryGetValue(next, out var file)) return file;
                        if (Subdirectories.TryGetValue(next, out var directory)) return directory;
                        return null;
                    }
                }

                path.RemoveAt(0);
                if (!Subdirectories.TryGetValue(next, out var dir))
                {
                    if (createIfNotExists == null) return null;

                    dir = Subdirectories[next] = new Directory();
                }

                return dir.Entry(path, createIfNotExists);
            }
        }

        private class CachedEntry : IFsEntry
        {
            private byte[] bytesCache;
            private FileInfo info;
            private bool read;
            private readonly ResourcesEntry resourcesPath;
            private string stringCache;

            public CachedEntry(ResourcesEntry resourcesPath)
            {
                this.resourcesPath = resourcesPath;
            }

            public FileInfo Info
            {
                get
                {
                    if (info != null) return info;
                    Read();
                    return info = new FileInfo
                    {
                        isDirectory = false,
                        size = bytesCache.Length,
                        date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                }
            }

            private void Read()
            {
                if (read) return;
                var data = Resources.Load<TextAsset>(resourcesPath.ResourcesPath);
                bytesCache = data.bytes;
                stringCache = data.text;
                read = true;
            }

            public string ReadString()
            {
                Read();
                return stringCache;
            }

            public byte[] ReadBytes()
            {
                Read();
                return bytesCache;
            }
        }
    }

    public struct ResourcesEntry
    {
        public string FullPath;
        public string ResourcesPath;
    }
}