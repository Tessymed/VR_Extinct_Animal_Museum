using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

/// <summary>
/// Lightweight XR Scene Configurator.
/// Ensures clean camera height, controller alignment, and smooth frame pacing
/// without interfering with Unity's native OpenXR/Oculus initialization.
/// </summary>
public class XRInputAutoActivator : MonoBehaviour
{
    [Header("Eye Height Override")]
    [Tooltip("Target standing eye height for the camera offset.")]
    public float targetEyeHeight = 1.1176f;

    private void Start()
    {
        // Allow Unity XR runtime to manage frame pacing smoothly
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        ConfigureXROriginAndCamera();
        EnableTrackedPoseDrivers();
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

        // Center Left and Right VR Controllers under Camera Offset
        var grabbers = FindObjectsByType<VRControllerGrab>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var grabber in grabbers)
        {
            if (grabber != null)
            {
                grabber.transform.localPosition = Vector3.zero;
                grabber.transform.localScale = Vector3.one;
            }
        }
    }

    private void EnableTrackedPoseDrivers()
    {
        var drivers = FindObjectsByType<TrackedPoseDriver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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
