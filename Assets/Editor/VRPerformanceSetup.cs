#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds the VR Performance Detector to the active scene for real-time playtesting diagnostics.
/// </summary>
public static class VRPerformanceSetup
{
    [InitializeOnLoadMethod]
    private static void AutoInitOnLoad()
    {
        EditorApplication.delayCall += EnsureDetectorInScene;
    }

    [MenuItem("Tools/Setup Real-Time VR Performance Detector", false, 1)]
    [MenuItem("Window/VR/Setup Real-Time Performance Detector", false, 1)]
    public static void SetupDetector()
    {
        EnsureDetectorInScene();
    }

    public static void EnsureDetectorInScene()
    {
        if (Application.isPlaying) return;

        var existing = Object.FindAnyObjectByType<VRPerformanceDetector>();
        if (existing == null)
        {
            var go = new GameObject("VR_Performance_Detector");
            Undo.RegisterCreatedObjectUndo(go, "Create VR Performance Detector");
            existing = go.AddComponent<VRPerformanceDetector>();
            Debug.Log("✅ [VRPerformanceDetector] Auto-added Real-Time VR Performance Detector to scene.");
        }

        existing.targetFramerate = 72f;
        existing.spikeThresholdMs = 15.0f;
        existing.showOverlay = true;
        existing.logToFile = true;
        EditorUtility.SetDirty(existing);
    }
}
#endif
