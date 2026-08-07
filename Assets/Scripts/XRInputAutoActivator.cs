using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Management;

/// <summary>
/// Optimized XR Performance & Tracking Auto-Activator.
/// Fixes startup hourglass loading delay and frame jittering by optimizing VR frame pacing
/// and ensuring clean, non-blocking XR initialization.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class XRInputAutoActivator : MonoBehaviour
{
    [Header("Eye Height Override")]
    [Tooltip("Target standing eye height for the camera offset.")]
    public float targetEyeHeight = 1.1176f;

    private static bool hasInitialized = false;

    private void Awake()
    {
        if (hasInitialized) return;
        hasInitialized = true;

        OptimizeVRPerformance();
        InitializeXRSubsystems();
        ConfigureXROriginAndCamera();
        EnableInputActions();
    }

    private void OptimizeVRPerformance()
    {
        // VR Pacing Optimization: Disable Unity VSync so the Oculus runtime manages frame timing smoothly
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
    }

    private void InitializeXRSubsystems()
    {
        if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
        {
            var manager = XRGeneralSettings.Instance.Manager;
            if (!manager.isInitializationComplete)
            {
                manager.InitializeLoaderSync();
            }

            if (manager.isInitializationComplete)
            {
                manager.StartSubsystems();
            }
        }
    }

    private void ConfigureXROriginAndCamera()
    {
        var origin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null)
        {
            origin.transform.localScale = Vector3.one;

            if (origin.CameraFloorOffsetObject != null)
            {
                Vector3 offsetPos = origin.CameraFloorOffsetObject.transform.localPosition;
                if (offsetPos.y <= 0.1f)
                {
                    offsetPos.y = targetEyeHeight;
                    origin.CameraFloorOffsetObject.transform.localPosition = offsetPos;
                }
            }

            if (origin.Camera != null)
            {
                origin.Camera.transform.localPosition = Vector3.zero;
                origin.Camera.transform.localScale = Vector3.one;
            }
        }

        // Ensure both Left and Right VR Controllers are centered at local (0,0,0) under Camera Offset
        var grabbers = FindObjectsByType<VRControllerGrab>(FindObjectsInactive.Include);
        foreach (var grabber in grabbers)
        {
            if (grabber != null)
            {
                grabber.transform.localPosition = Vector3.zero;
                grabber.transform.localScale = Vector3.one;
            }
        }
    }

    private void EnableInputActions()
    {
        // Enable scene input action assets without obsolete FindObjectsSortMode warnings
        var activeAssets = FindObjectsByType<InputActionAsset>(FindObjectsInactive.Exclude);
        foreach (var asset in activeAssets)
        {
            if (asset != null && !asset.enabled)
            {
                asset.Enable();
            }
        }

        var drivers = FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Exclude);
        foreach (var driver in drivers)
        {
            if (driver != null)
            {
                driver.enabled = true;
                if (driver.positionInput.action != null) driver.positionInput.action.Enable();
                if (driver.rotationInput.action != null) driver.rotationInput.action.Enable();
            }
        }
    }
}
