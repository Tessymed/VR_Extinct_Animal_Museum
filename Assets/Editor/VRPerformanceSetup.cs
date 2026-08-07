#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds the VR Performance Detector to the active scene for real-time playtesting diagnostics.
/// </summary>
public static class VRPerformanceSetup
{
    [MenuItem("Tools/Setup Real-Time VR Performance Detector")]
    public static void SetupDetector()
    {
        var existing = Object.FindAnyObjectByType<VRPerformanceDetector>();
        if (existing == null)
        {
            var go = new GameObject("VR_Performance_Detector");
            Undo.RegisterCreatedObjectUndo(go, "Create VR Performance Detector");
            existing = go.AddComponent<VRPerformanceDetector>();
            Debug.Log("✅ [VRPerformanceSetup] VR Performance Detector added to scene. Save your scene!");
        }
        else
        {
            Debug.Log("ℹ️ [VRPerformanceSetup] VR Performance Detector already present in scene.");
        }

        existing.targetFramerate = 72f;
        existing.spikeThresholdMs = 15.0f;
        existing.showOverlay = true;
        existing.logToFile = true;
        EditorUtility.SetDirty(existing);
    }
}
#endif
