using System;
using System.IO;
using System.Text;

namespace ThingsEditor.IO.Filesystem
{
    /// <summary>
    ///     OpenFile is a class that represents a file opened for reading/writing.
    ///     Because of the way our zip disk storage works, this has to actually do all
    ///     its work in memory until it is closed, at which point it is written to disk.
    /// </summary>
    internal class OpenFile
    {
        // where this data should be stored on disk:
        public Disk disk;
        public string error;

        private MemoryStream memStream;
        private bool needSave;
        public string path;

        public OpenFile(DiskController controller, string path, string mode)
        {
            disk = controller.GetDisk(ref path);
            this.path = path;
            error = null;
            readable = writeable = needSave = false;

            switch (mode)
            {
                case "r":
                {
                    // Read-only file stream; must exist.
                    byte[] data = null;
                    try
                    {
                        data = disk.ReadBinary(path);
                    }
                    catch (Exception) { }

                    if (data == null)
                    {
                        error = "Error: file not found";
                    }
                    else
                    {
                        memStream = new MemoryStream(data);
                        readable = true;
                    }
                }
                    break;
                case "r+":
                {
                    // Read/write file stream; must exist.
                    // ToDo: throw proper exceptions around these!
                    byte[] data = null;
                    try
                    {
                        data = disk.ReadBinary(path);
                    }
                    catch (Exception) { }

                    if (data == null)
                    {
                        error = "Error: file not found";
                    }
                    else
                    {
                        memStream = new MemoryStream();
                        memStream.Write(data, 0, data.Length);
                        memStream.Position = 0;
                        readable = writeable = true;
                    }
                }
                    break;
                case "w":
                {
                    // Write-only file stream; previous data (if any) is discarded.
                    memStream = new MemoryStream();
                    writeable = true;
                    needSave = true;
                }
                    break;
                case "w+":
                {
                    // Read/write file stream; previous data (if any) is discarded.
                    memStream = new MemoryStream();
                    readable = writeable = true;
                    needSave = true;
                }
                    break;
                case "rw+":
                {
                    // Read/write file stream; file created if it doesn't exist.
                    // (Same as "a+" but we start at the beginning rather than the end.)
                    memStream = new MemoryStream();
                    byte[] data = null;
                    try
                    {
                        data = disk.ReadBinary(path);
                    }
                    catch (Exception) { }

                    if (data != null) memStream.Write(data, 0, data.Length);
                    memStream.Position = 0;
                    readable = writeable = true;
                }
                    break;
                case "a":
                {
                    // Append: read existing data (if any), but then be ready to add more.
                    memStream = new MemoryStream();
                    byte[] data = null;
                    try
                    {
                        data = disk.ReadBinary(path);
                    }
                    catch (Exception) { }

                    if (data != null) memStream.Write(data, 0, data.Length);
                    writeable = true;
                }
                    break;
                case "a+":
                {
                    // Append: read existing data (if any), but then be ready to add more.
                    memStream = new MemoryStream();
                    byte[] data = null;
                    try
                    {
                        data = disk.ReadBinary(path);
                    }
                    catch (Exception) { }

                    if (data != null) memStream.Write(data, 0, data.Length);
                    readable = writeable = true;
                }
                    break;
                default:
                    error = "Error: invalid file mode (" + mode + ")";
                    break;
            }
        }

        public bool readable { get; }
        public bool writeable { get; }
        public bool isOpen => memStream != null;
        public bool isAtEnd => memStream == null || memStream.Position >= memStream.Length;

        public long position
        {
            get => memStream == null ? 0 : memStream.Position;
            set
            {
                if (memStream != null && memStream.CanSeek) memStream.Position = value;
            }
        }

        public void Write(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!writeable)
            {
                error = "Error: stream is not writeable";
                return;
            }

            if (!disk.IsWriteable())
            {
                error = "Error: disk is not writeable";
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(text);
            memStream.Write(bytes, 0, bytes.Length);
            needSave = true;
        }

        public string ReadToEnd()
        {
            if (!readable)
            {
                error = "Error: stream is not readable";
                return null;
            }

            if (memStream == null)
            {
                error = "Error: file is not open";
                return null;
            }

            var s = Encoding.UTF8.GetString(memStream.GetBuffer(), (int)memStream.Position,
                (int)(memStream.Length - memStream.Position));
            memStream.Position = memStream.Length;
            return s;
        }

        public string ReadLine()
        {
            if (!readable)
            {
                error = "Error: stream is not readable";
                return null;
            }

            if (memStream == null)
            {
                error = "Error: file is not open";
                return null;
            }

            // Find the line terminator.  We accept either 10; 13; or 13,10.
            // (So for the start of the line terminator, we need only search for 10 or 13.
            var memBuf = memStream.GetBuffer();
            var start = (int)memStream.Position;
            if (start >= memStream.Length) return null;
            var eolPos = start;
            while (eolPos < memStream.Length && memBuf[eolPos] != 10 && memBuf[eolPos] != 13) eolPos++;
            // Grab the string up to that point.
            var s = Encoding.UTF8.GetString(memBuf, start, eolPos - start);
            // Now advance past the line terminator.  (Watch out for 13,10.)
            if (eolPos + 1 < memStream.Length && memBuf[eolPos] == 13 && memBuf[eolPos + 1] == 10) eolPos++;
            if (eolPos + 1 > memStream.Length) eolPos--;
            memStream.Position = eolPos + 1;
            return s;
        }

        public string ReadChars(int charCount = 1)
        {
            if (!readable)
            {
                error = "Error: stream is not readable";
                return null;
            }

            if (memStream == null)
            {
                error = "Error: file is not open";
                return null;
            }

            // Advance, counting characters (technically, code points) until we have enough.
            // In UTF-8, a character always starts with either high bit clear, or first two bits set.
            // Additional bytes in a character always start with high 2 bits equal to 0b10.
            var memBuf = memStream.GetBuffer();
            var start = (int)memStream.Position;
            if (start >= memStream.Length) return null;
            var endPos = start;
            var count = 0;
            while (endPos < memStream.Length && count < charCount)
            {
                // Advance past the start of the current character, and count it.
                endPos++;
                count++;
                // Continue advancing while we are in subsequent bytes of that same character.
                while (endPos < memStream.Length && (memBuf[endPos] & 0xC0) == 0x80) endPos++;
            }

            // Grab the string up to that point.
            var s = Encoding.UTF8.GetString(memBuf, start, endPos - start);
            memStream.Position = endPos;
            return s;
        }

        public void Close()
        {
            if (memStream == null) return;
            if (writeable && needSave) disk.WriteBinary(path, memStream.ToArray());
            memStream.Close();
            memStream = null;
        }
    }
}
