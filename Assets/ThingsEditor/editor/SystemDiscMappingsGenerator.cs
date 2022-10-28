using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ThingsEditor.IO.Filesystem;
using UnityEditor;
using UnityEngine;

namespace ThingsEditor.Editor
{
    public class SystemDiscMappingsGenerator : AssetPostprocessor
    {
        private const string Sysdisk = "sysdisk";
        private const string SysdiskRoot = "SysdiskRoot";
        private const string SysdiskJson = "SysdiskJson";

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            var deletedFiles = new HashSet<string>();
            var newFiles = new HashSet<string>();

            foreach (var asset in importedAssets.Concat(movedAssets))
            {
                if (!asset.ToLower().Contains(Sysdisk)) continue;
                newFiles.Add(asset);
            }

            foreach (var asset in deletedAssets.Concat(movedFromAssetPaths)) deletedFiles.Add(asset.ToLowerInvariant());

            if (deletedFiles.Count == 0 && newFiles.Count == 0) return;
            var json = GetJson(out _);
            var files = JsonConvert.DeserializeObject<Dictionary<string, ResourcesEntry>>(json.text);
            var folders = SysDisks();
            var changed = false;
            foreach (var file in newFiles) ProcessPath(files, folders, file, false, ref changed);

            foreach (var (path, entry) in new Dictionary<string, ResourcesEntry>(files!))
            {
                if (!deletedFiles.Contains(entry.FullPath.ToLowerInvariant())) continue;
                files.Remove(path);
                changed = true;
            }

            if (changed) WriteJson(files);
        }

        [MenuItem("Assets/Generate sysdisk")]
        private static void ProcessFiles()
        {
            var folders = SysDisks();
            var assets = AssetDatabase.FindAssets("", folders);
            var files = new Dictionary<string, ResourcesEntry>();
            var changed = false;
            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!File.Exists(path)) continue;
                ProcessPath(files, folders, path, true, ref changed);
            }

            WriteJson(files);
        }

        private static string[] SysDisks()
        {
            var folders = AssetDatabase.FindAssets($"l:{SysdiskRoot}").Select(e =>
            {
                var path = AssetDatabase.GUIDToAssetPath(e);
                if (path.ToLower().EndsWith(Sysdisk) || path.ToLower().EndsWith(Sysdisk + "/")) return path;
                throw new Exception($"All {SysdiskRoot} labeled folders must ba named {Sysdisk}\nAt {path}");
            }).ToArray();
            if (folders.Length == 0) throw new Exception($"{SysdiskRoot} labeled folder is not found");
            return folders;
        }

        private static void ProcessPath(IDictionary<string, ResourcesEntry> files, IEnumerable<string> folders,
            string path, bool overwriteConflicts, ref bool changed)
        {
            var fullPath = path;
            foreach (var folder in folders) path = path.Replace(folder, "");

            if (path == fullPath)
            {
                Debug.LogWarning($"File {fullPath} is inside {Sysdisk} folder, that is not labeled as {SysdiskRoot}");
                return;
            }

            if (path.StartsWith("/")) path = path[1..];
            if (files.ContainsKey(path))
            {
                var old = files[path];
                if (string.Equals(old.FullPath, fullPath, StringComparison.InvariantCultureIgnoreCase)) return;
                if (!overwriteConflicts)
                    throw new Exception(
                        $"Files {old.FullPath} and {fullPath} are pointing to the same sysdisk path {path}");
            }

            changed = true;
            files[path] = new ResourcesEntry
            {
                FullPath = fullPath,
                ResourcesPath = ExtractResourcesPath(fullPath)
            };
        }

        private static TextAsset GetJson(out string path)
        {
            var jsonAssets = AssetDatabase.FindAssets($"l:{SysdiskJson}");
            if (jsonAssets.Length == 0)
                throw new Exception(
                    $"Sysdisk JSON is not found. Please create a json file and label it with {SysdiskJson}");

            if (jsonAssets.Length > 1)
                throw new Exception($"Too many files labeled {SysdiskJson} are found. Only 1 may be present at a time");

            path = AssetDatabase.GUIDToAssetPath(jsonAssets[0]);
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            return asset;
        }

        private static void WriteJson(Dictionary<string, ResourcesEntry> data)
        {
            var asset = GetJson(out var path);

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(path, json);
            EditorUtility.SetDirty(asset);
        }

        private static string ExtractResourcesPath(string path)
        {
            var parts = path.Split("/").ToList();
            while (parts.Count > 0)
            {
                var name = parts[0];
                parts.RemoveAt(0);
                if (name == "Resources")
                {
                    path = string.Join('/', parts);
                    return Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path))
                        .Replace("\\", "/");
                }
            }

            throw new Exception($"path {path} is not inside Resources folder");
        }
    }
}