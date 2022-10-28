using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ThingsEditor.IO.Filesystem
{
    internal class MemoryFileDisk : Disk
    {
        internal string DiskName;
        internal MemoryDirectory Root;

        // Request initial data sync
        public MemoryFileDisk(string diskName)
        {
            DiskName = diskName;
        }

        private void SendUpdateMessage(MemoryFileDiskAction action, string filePath, byte[] data = null) { }

        public override Disk Mount(string path, string diskName, out string errMsg)
        {
            errMsg = null;
            var folder = Root?.GetDirectoryAt(GetSegments(path), out errMsg);
            if (folder == null) return null;

            return new MemoryFileDisk(diskName)
            {
                Root = folder
            };
        }

        public override bool IsWriteable()
        {
            return true;
        }

        public override FileInfo GetFileInfo(string filePath)
        {
            return Root?.GetFileInfo(GetSegments(filePath));
        }

        // Relative to disk root
        public override List<string> GetFileNames(string dirPath)
        {
            return Root?.ListFiles(GetSegments(dirPath));
        }

        public override byte[] ReadBinary(string filePath)
        {
            return Root?.ReadBinaryFile(GetSegments(filePath));
        }

        public override string ReadText(string filePath)
        {
            return Root?.ReadTextFile(GetSegments(filePath));
        }

        public override void WriteText(string filePath, string text)
        {
            if (Root == null) return;

            Root.WriteTextFile(GetSegments(filePath), text);

            SendUpdateMessage(MemoryFileDiskAction.Write, filePath, Encoding.Default.GetBytes(text));
        }

        public override void WriteBinary(string filePath, byte[] data)
        {
            if (Root == null) return;

            Root.WriteBinaryFile(GetSegments(filePath), data);

            SendUpdateMessage(MemoryFileDiskAction.Write, filePath, data);
        }

        public override bool MakeDir(string dirPath, out string errMsg)
        {
            errMsg = "Root directory not found";
            if (Root == null) return false;

            var result = Root.MakeDir(GetSegments(dirPath), out errMsg);
            if (result) SendUpdateMessage(MemoryFileDiskAction.MakeDir, dirPath);
            return result;
        }

        public override bool Delete(string filePath, out string errMsg)
        {
            errMsg = "Root directory not found";
            if (Root == null) return false;

            var result = Root.Delete(GetSegments(filePath), out errMsg);
            if (result) SendUpdateMessage(MemoryFileDiskAction.Delete, filePath);
            return result;
        }

        public static List<string> GetSegments(string path)
        {
            return path.Split('/').ToList();
        }
    }
}