using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ThingsEditor.Editor
{
    [ScriptedImporter(1, "ms")]
    public class MsImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var subAsset = new TextAsset(File.ReadAllText(ctx.assetPath));
            ctx.AddObjectToAsset("text", subAsset);
            ctx.SetMainObject(subAsset);
        }
    }
}