#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

/// <summary>
/// 1-Click VR Headset Build Utility.
/// Automates building the VR Extinct Animal Museum for:
/// 1. Standalone Meta Quest Headset (Android APK)
/// 2. PC VR / Quest Link (Windows Standalone 64-bit EXE)
/// </summary>
public static class HeadsetBuildHelper
{
    private static readonly string[] Scenes = new[] { "Assets/Scenes/Museum_Main.unity" };

    [MenuItem("Tools/Build/Build Meta Quest Standalone APK", false, 10)]
    public static void BuildQuestAPK()
    {
        string buildFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/MetaQuest"));
        if (!Directory.Exists(buildFolder))
        {
            Directory.CreateDirectory(buildFolder);
        }

        string apkPath = Path.Combine(buildFolder, "VR_Extinct_Animal_Museum.apk");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        Debug.Log($"🚀 [Build Tool] Starting Meta Quest Standalone APK Build...\nDestination: {apkPath}");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        PrintBuildSummary(report, "Meta Quest Standalone APK");
    }

    [MenuItem("Tools/Build/Build Windows PC VR (Quest Link)", false, 11)]
    public static void BuildWindowsPCVR()
    {
        string buildFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/PCVR"));
        if (!Directory.Exists(buildFolder))
        {
            Directory.CreateDirectory(buildFolder);
        }

        string exePath = Path.Combine(buildFolder, "VR_Extinct_Animal_Museum.exe");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = exePath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        Debug.Log($"🚀 [Build Tool] Starting Windows PC VR (Quest Link) Build...\nDestination: {exePath}");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        PrintBuildSummary(report, "Windows PC VR");
    }

    private static void PrintBuildSummary(BuildReport report, string buildName)
    {
        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"✅ [Build Tool] {buildName} SUCCEEDED!\nSize: {summary.totalSize / (1024 * 1024):F1} MB\nTime: {summary.totalTime.TotalSeconds:F1}s\nOutput Path: {summary.outputPath}");
            EditorUtility.RevealInFinder(summary.outputPath);
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($"❌ [Build Tool] {buildName} FAILED with {summary.totalErrors} errors.");
        }
        else
        {
            Debug.LogWarning($"⚠️ [Build Tool] {buildName} ended with result: {summary.result}");
        }
    }
}
#endif
