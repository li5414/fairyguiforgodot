// FairyGUI for Godot - visual comparison tooling, Unity side (not part of the Godot build).
// Copied by tools/visual-compare/run.ps1 into the scratch Unity project (_ref/unity-capture/Assets/Editor/).
// Builds a Windows player of the FairyGUI-unity examples. Scene 0 is a blank Boot scene so that
// UnityCaptureRunner can load the requested example into a clean process.
//   Unity.exe -batchmode -quit -projectPath <p> -buildTarget Win64 -executeMethod CaptureBuild.Build -captureBuildOut <exe>
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CaptureBuild
{
    const string BootScene = "Assets/Examples/Scenes/Boot.unity";

    public static void Build()
    {
        string output = GetArg("-captureBuildOut") ?? "Build/Capture.exe";

        var boot = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(boot, BootScene);

        var scenes = Directory.GetFiles("Assets/Examples/Scenes", "Example *.unity")
            .Select(p => p.Replace('\\', '/'))
            .OrderBy(p => p, System.StringComparer.Ordinal)
            .ToList();
        scenes.Insert(0, BootScene);
        EditorBuildSettings.scenes = scenes.Select(p => new EditorBuildSettingsScene(p, true)).ToArray();

        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1136;
        PlayerSettings.defaultScreenHeight = 640;
        PlayerSettings.resizableWindow = false;
        PlayerSettings.runInBackground = true;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });
        Debug.Log("[CAPTURE-BUILD] " + report.summary.result + " -> " + output + " (" + report.summary.totalErrors + " errors)");
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    static string GetArg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];
        return null;
    }
}
