namespace ThingsEditor.IO.Filesystem
{
    public class FileInfo
    {
        public string comment; // file comment
        public string date; // file timestamp, in SQL format
        public bool isDirectory; // true if it's a directory
        public long size; // size in bytes
    }
}