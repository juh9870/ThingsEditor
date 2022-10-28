using System;
using System.Collections.Generic;
using System.Text;

namespace ThingsEditor.IO.Filesystem
{
    public class MemoryDirectory
    {
        public FileInfo DirInfo { get; set; } = new()
        {
            isDirectory = true,
            date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        public Dictionary<string, MemoryDirectory> Subdirectories { get; set; } = new();
        public Dictionary<string, Tuple<FileInfo, byte[]>> Files { get; set; } = new();

        public bool IsEmpty()
        {
            return Files.Count == 0 && Subdirectories.Count == 0;
        }

        public MemoryDirectory GetDirectoryAt(List<string> dirPath, out string errMsg)
        {
            errMsg = null;
            if (dirPath.Count == 0) return this;

            var dirKey = dirPath[0];
            if (!Subdirectories.TryGetValue(dirKey, out var subDirectory))
            {
                errMsg = Files.ContainsKey(dirKey) ? "Error: requested path is not a directory" : "Error: Invalid path";
                return null;
            }

            if (dirPath.Count == 1) return subDirectory;
            dirPath.RemoveAt(0);
            return Subdirectories[dirKey].GetDirectoryAt(dirPath, out errMsg);
        }

        public List<string> ListFiles(List<string> dirPath)
        {
            if (dirPath.Count == 0)
            {
                List<string> fileList = new();
                fileList.AddRange(Files.Keys);
                fileList.AddRange(Subdirectories.Keys);

                return fileList;
            }

            var dirKey = dirPath[0];
            dirPath.RemoveAt(0);

            return Subdirectories[dirKey].ListFiles(dirPath);
        }

        public FileInfo GetFileInfo(List<string> filePath)
        {
            if (filePath.Count == 0) return DirInfo;

            if (filePath.Count == 1)
            {
                if (Files.ContainsKey(filePath[0]))
                    return Files[filePath[0]].Item1;
                if (Subdirectories.ContainsKey(filePath[0])) return Subdirectories[filePath[0]].DirInfo;

                return null;
            }

            var dirKey = filePath[0];
            filePath.RemoveAt(0);
            if (Subdirectories.ContainsKey(dirKey)) return Subdirectories[dirKey].GetFileInfo(filePath);

            return null;
        }

        public byte[] ReadBinaryFile(List<string> filePath)
        {
            if (filePath.Count == 0) return null;

            if (filePath.Count == 1)
            {
                if (Files.ContainsKey(filePath[0])) return Files[filePath[0]].Item2;

                return null;
            }

            var dirKey = filePath[0];
            filePath.RemoveAt(0);
            if (Subdirectories.ContainsKey(dirKey)) return Subdirectories[dirKey].ReadBinaryFile(filePath);

            return null;
        }

        public string ReadTextFile(List<string> filePath)
        {
            if (filePath.Count == 0) return null;

            if (filePath.Count == 1)
            {
                if (Files.ContainsKey(filePath[0])) return Encoding.Default.GetString(Files[filePath[0]].Item2);

                return null;
            }

            var dirKey = filePath[0];
            filePath.RemoveAt(0);
            if (Subdirectories.ContainsKey(dirKey))
                return Encoding.Default.GetString(Subdirectories[dirKey].ReadBinaryFile(filePath));

            return null;
        }

        public void WriteBinaryFile(List<string> filePath, byte[] bytes)
        {
            if (filePath.Count == 1)
            {
                Files[filePath[0]] = Tuple.Create(
                    new FileInfo
                    {
                        isDirectory = false,
                        date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        size = bytes.Length
                    },
                    bytes
                );
            }
            else if (filePath.Count > 1)
            {
                var dirKey = filePath[0];
                filePath.RemoveAt(0);
                if (Subdirectories.ContainsKey(dirKey)) Subdirectories[dirKey].WriteBinaryFile(filePath, bytes);
            }
        }

        public void WriteTextFile(List<string> filePath, string text)
        {
            WriteBinaryFile(filePath, Encoding.Default.GetBytes(text));
        }

        public bool MakeDir(List<string> dirPath, out string errMsg)
        {
            if (dirPath.Count == 1)
            {
                if (!Subdirectories.ContainsKey(dirPath[0]))
                {
                    Subdirectories[dirPath[0]] = new MemoryDirectory();
                    errMsg = null;

                    return true;
                }

                errMsg = "Directory exists";
            }
            else if (dirPath.Count > 1)
            {
                var dirKey = dirPath[0];
                dirPath.RemoveAt(0);
                Subdirectories[dirKey].MakeDir(dirPath, out errMsg);
            }
            else
            {
                errMsg = "Invalid path";
            }

            return false;
        }

        public bool Delete(List<string> filePath, out string errMsg)
        {
            if (filePath.Count == 1)
            {
                errMsg = null;
                if (Files.ContainsKey(filePath[0]))
                {
                    Files.Remove(filePath[0]);
                    return true;
                }

                if (Subdirectories.ContainsKey(filePath[0]))
                {
                    if (Subdirectories[filePath[0]].IsEmpty())
                    {
                        Subdirectories.Remove(filePath[0]);
                        return true;
                    }

                    errMsg = "Directory not empty";
                    return false;
                }

                errMsg = "No such file or directory";
                return false;
            }

            if (filePath.Count > 1)
            {
                var dirKey = filePath[0];
                filePath.RemoveAt(0);
                if (Subdirectories.ContainsKey(dirKey)) return Subdirectories[dirKey].Delete(filePath, out errMsg);
            }

            errMsg = "Invalid path";
            return false;
        }

        public override string ToString()
        {
            var dirString = "MemoryDirectory: {";
            foreach (var dir in Subdirectories) dirString += $"\n\tdir\t{dir.Key}";
            foreach (var file in Files) dirString += $"\n\tfile\t{file.Key}";
            dirString += "\n}";

            return dirString;
        }
    }
}