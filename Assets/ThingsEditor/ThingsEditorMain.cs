using RosettaUI;
using RosettaUI.UIToolkit;
using SimpleFileBrowser;
using ThingsEditor.IO.Filesystem;
using ThingsEditor.Scripting;
using UnityEngine;

[RequireComponent(typeof(RosettaUIRootUIToolkit))]
public class ThingsEditorMain : MonoBehaviour
{
    [SerializeField] public UISkin Skin;
    private string knownPath;

    // Start is called before the first frame update
    private void Start()
    {
        FileBrowser.CheckWriteAccessToDestinationDirectory = true;
        FileBrowser.Skin = Skin;
        FileBrowser.ShowLoadDialog(data =>
        {
            var dir = data[0];
            var drive = new RealFileDisk(dir);
            DiskController.Instance.AddDisk("home", drive);
            RunTests();
            Show();
        }, () => { }, FileBrowser.PickMode.Folders);
    }

    private void RunTests()
    {
        // ScriptingManager.Reset();
        var interpreter = ScriptingManager.NewInterpreter("import.fresh from(\"/sys/test\").into({});");
        interpreter.Run();
    }

    private void Show()
    {
        GetComponent<RosettaUIRoot>().Build(UI.Window(UI.Button("Run tests", RunTests)));
    }
}