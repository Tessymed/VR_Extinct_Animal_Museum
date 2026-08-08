using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

/// <summary>
/// Bulletproof 6DoF Head Tracking & Floor Elevation Enforcer for Meta Quest.
/// Solves:
/// 1. 3DoF world-rotation lock (world dragging when head turns) by directly binding to XRNode.CenterEye.
/// 2. Camera sinking into the ground by ensuring proper floor elevation and standing height.
/// </summary>
[DefaultExecutionOrder(-500)]
public class VRCameraPoseEnforcer : MonoBehaviour
{
    [Header("Elevation Settings")]
    [Tooltip("Target standing eye height in meters if Guardian floor is at zero.")]
    public float targetStandingHeight = 1.65f;

    [Tooltip("Minimum head elevation allowed to prevent sinking into the floor.")]
    public float minFloorElevation = 1.2f;

    private Transform cameraOffsetTransform;
    private XROrigin xrOrigin;
    private InputDevice centerEyeDevice;

    private void Awake()
    {
        xrOrigin = GetComponentInParent<XROrigin>();
        if (xrOrigin != null && xrOrigin.CameraFloorOffsetObject != null)
        {
            cameraOffsetTransform = xrOrigin.CameraFloorOffsetObject.transform;
        }
        else if (transform.parent != null)
        {
            cameraOffsetTransform = transform.parent;
        }

        // Register for early pre-render pose updates
        Application.onBeforeRender += OnBeforeRenderPoseUpdate;
    }

    private void OnDestroy()
    {
        Application.onBeforeRender -= OnBeforeRenderPoseUpdate;
    }

    private void Start()
    {
        CalibrateOriginAndFloor();
    }

    private void CalibrateOriginAndFloor()
    {
        if (xrOrigin != null)
        {
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
        }

        if (cameraOffsetTransform != null)
        {
            Vector3 offset = cameraOffsetTransform.localPosition;
            // If floor height is at zero or sunk into ground, set to standing eye height
            if (offset.y < 0.2f)
            {
                offset.y = targetStandingHeight;
                cameraOffsetTransform.localPosition = offset;
            }
        }
    }

    private void Update()
    {
        UpdateHeadPose();
    }

    private void OnBeforeRenderPoseUpdate()
    {
        UpdateHeadPose();
    }

    private void UpdateHeadPose()
    {
        if (!centerEyeDevice.isValid)
        {
            centerEyeDevice = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
        }

        if (centerEyeDevice.isValid)
        {
            // 1. Direct 6DoF Head Rotation (eliminates world-drag and rotation lock)
            if (centerEyeDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion headRotation))
            {
                transform.localRotation = headRotation;
            }
            else if (centerEyeDevice.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion centerRot))
            {
                transform.localRotation = centerRot;
            }

            // 2. Direct 6DoF Head Position
            if (centerEyeDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 headPosition))
            {
                transform.localPosition = headPosition;

                // Check if head position is sunk into ground
                if (cameraOffsetTransform != null && cameraOffsetTransform.localPosition.y < 0.2f && headPosition.y < minFloorElevation)
                {
                    Vector3 offset = cameraOffsetTransform.localPosition;
                    offset.y = targetStandingHeight;
                    cameraOffsetTransform.localPosition = offset;
                }
            }
            else if (centerEyeDevice.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 centerPos))
            {
                transform.localPosition = centerPos;
            }
        }
    }
}
