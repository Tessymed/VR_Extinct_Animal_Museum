#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// Automatically ensures the Main Camera has VRCameraPoseEnforcer attached and configured.
/// </summary>
public static class VRHeadSetup
{
    [InitializeOnLoadMethod]
    private static void AutoSetupCamera()
    {
        EditorApplication.delayCall += EnsureCameraPoseEnforcer;
    }

    [MenuItem("Tools/Setup VR 6DoF Camera & Height", false, 5)]
    public static void EnsureCameraPoseEnforcer()
    {
        if (Application.isPlaying) return;

        var mainCam = Camera.main;
        if (mainCam == null)
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin != null && origin.Camera != null)
            {
                mainCam = origin.Camera;
            }
        }

        if (mainCam != null)
        {
            var enforcer = mainCam.GetComponent<VRCameraPoseEnforcer>();
            if (enforcer == null)
            {
                enforcer = Undo.AddComponent<VRCameraPoseEnforcer>(mainCam.gameObject);
                Debug.Log("✅ [VRHeadSetup] Added VRCameraPoseEnforcer to Main Camera for 100% 6DoF head rotation & natural eye level.");
            }
            EditorUtility.SetDirty(mainCam.gameObject);
        }
    }
}
#endif
