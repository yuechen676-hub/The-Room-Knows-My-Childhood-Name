using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// The offline demo plays recorded replies instead of calling the dialogue service.
public static class RoomOfflineDemoTools
{
    public const string EditorKey = "Room.OfflineDemoInEditor";
    private const string Toggle = "Tools/Room/Play Offline Demo in Editor";
    private static readonly string Root = Directory.GetParent(Application.dataPath).FullName;

    [MenuItem(Toggle)]
    private static void ToggleInEditor() { EditorPrefs.SetBool(EditorKey, !EditorPrefs.GetBool(EditorKey, false)); }

    [MenuItem(Toggle, true)]
    private static bool ToggleInEditorState()
    {
        Menu.SetChecked(Toggle, EditorPrefs.GetBool(EditorKey, false));
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    // Built into its own folder with no connection settings beside the player, which is
    // what switches the player to the demo.
    [MenuItem("Tools/Room/Build Windows Offline Demo")]
    public static void BuildWindowsDemo()
    {
        var folder = Path.Combine(Root, "Builds", "WindowsDemo");
        try
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveOpenScenes())
                throw new Exception("Could not save the open scenes.");
            if (Resources.Load<TextAsset>("Demo/OfflineDialogue") == null)
                throw new Exception("Record the demo replies first: Deployment/record_offline_dialogue.py.");
            BuildDesktopRelease.ApplyDisplaySettings();
            var scenePaths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenePaths.Length != 1 || !File.Exists(scenePaths[0]))
                throw new Exception("Configure exactly one verified room scene in Build Settings before building.");
            Directory.CreateDirectory(folder);
            File.Delete(Path.Combine(folder, "room-service.json"));
            PlayerSettings.runInBackground = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = Path.Combine(folder, "TheRoomKnowsMyChildhoodName.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4
            });
            if (report.summary.result != BuildResult.Succeeded) { Debug.LogError("Offline demo build failed."); return; }
            CopyLiveDialogueKit(Path.Combine(folder, "LiveDialogue"));
            // Burst's debug symbols are for the developer only, as the folder name says.
            foreach (var debug in Directory.GetDirectories(folder, "*_DoNotShip")) Directory.Delete(debug, true);
            Debug.Log("Offline demo built in Builds/WindowsDemo (" + report.summary.totalSize + " bytes).");
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    // The optional live dialogue: start script, instructions and the dialogue service without
    // its tests, live validation scripts, logs or local environment.
    private static void CopyLiveDialogueKit(string kit)
    {
        if (Directory.Exists(kit)) Directory.Delete(kit, true);
        var server = Path.Combine(kit, "server");
        Directory.CreateDirectory(server);
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "Deployment", "LiveDialogueKit")))
            File.Copy(file, Path.Combine(kit, Path.GetFileName(file)));
        string[] skipped = { "test_", "validate_", "diagnose_", "measure_", "poem_", "editor_service", "cloud" };
        foreach (var file in Directory.GetFiles(Path.Combine(Root, "Deployment", "server")))
        {
            var name = Path.GetFileName(file);
            if ((name.EndsWith(".py") || name == "clues.json" || name == "requirements.txt") && !skipped.Any(name.StartsWith))
                File.Copy(file, Path.Combine(server, name));
        }
    }
}
