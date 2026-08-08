#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

/// <summary>
/// 1-Click VR Headset Build Utility.
/// Automates building the VR Extinct Animal Museum for:
/// 1. Standalone Meta Quest Headset (Android APK) - High performance ARM64 with ASTC textures.
/// 2. PC VR / Meta Quest Link (Windows Standalone 64-bit EXE).
/// </summary>
public static class HeadsetBuildHelper
{
    private static readonly string[] Scenes = new[] { "Assets/Scenes/Museum_Main.unity" };

    [MenuItem("Tools/Build/Build Meta Quest Standalone APK", false, 10)]
    public static void BuildQuestAPK()
    {
        // 1. Ensure build destination directory exists
        string buildFolder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/MetaQuest"));
        if (!Directory.Exists(buildFolder))
        {
            Directory.CreateDirectory(buildFolder);
        }

        string apkPath = Path.Combine(buildFolder, "VR_Extinct_Animal_Museum.apk");

        // 2. Configure Android settings for Meta Quest
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;

        // Meta Quest target architecture: ARM64
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29; // Android 10+ for Quest 2/3/Pro

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = apkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };

        Debug.Log($"🚀 [Build Tool] Starting Meta Quest Standalone APK Build...\nDestination: {apkPath}");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        PrintBuildSummary(report, "Meta Quest Standalone APK", apkPath);
    }

    [MenuItem("Tools/Build/Build Windows PC VR (Quest Link)", false, 11)]
    public static void BuildWindowsPCVR()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

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
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        Debug.Log($"🚀 [Build Tool] Starting Windows PC VR (Quest Link) Build...\nDestination: {exePath}");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        PrintBuildSummary(report, "Windows PC VR", exePath);
    }

    [MenuItem("Tools/Build/Reset Build Target to Windows PC VR", false, 20)]
    public static void ResetToWindows()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
        Debug.Log("✅ [Build Tool] Build target reset to Windows Standalone (Quest Link).");
    }

    private static void PrintBuildSummary(BuildReport report, string buildName, string targetPath)
    {
        BuildSummary summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"✅ [Build Tool] {buildName} SUCCEEDED!\nSize: {summary.totalSize / (1024 * 1024):F1} MB\nTime: {summary.totalTime.TotalSeconds:F1}s\nOutput Path: {summary.outputPath}");
            
            string fileToReveal = File.Exists(summary.outputPath) ? summary.outputPath : targetPath;
            if (File.Exists(fileToReveal))
            {
                EditorUtility.RevealInFinder(fileToReveal);
            }
            else if (Directory.Exists(Path.GetDirectoryName(targetPath)))
            {
                EditorUtility.RevealInFinder(Path.GetDirectoryName(targetPath));
            }
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
