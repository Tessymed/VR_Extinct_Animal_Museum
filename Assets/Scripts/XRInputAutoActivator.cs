using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// Lightweight XR Scene Configurator.
/// Ensures clean camera height, controller alignment, and smooth frame pacing.
/// </summary>
[DefaultExecutionOrder(-100)]
public class XRInputAutoActivator : MonoBehaviour
{
    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;

        var origin = Object.FindAnyObjectByType<XROrigin>();
        if (origin != null)
        {
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 0f;
            if (origin.CameraFloorOffsetObject != null)
            {
                origin.CameraFloorOffsetObject.transform.localPosition = Vector3.zero;
            }
        }
    }
}
