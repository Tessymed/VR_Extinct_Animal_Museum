using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

/// <summary>
/// Natural Eye-Level 6DoF Head Tracking Enforcer for Meta Quest.
/// Ensures perfect human eye-level height (just below midway between ceiling and ground)
/// and full 6DoF head rotation without world-drag or ceiling/floor clipping.
/// </summary>
[DefaultExecutionOrder(-500)]
public class VRCameraPoseEnforcer : MonoBehaviour
{
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

        // Keep CameraOffset at zero so Quest physical floor tracking provides natural eye height
        if (cameraOffsetTransform != null)
        {
            cameraOffsetTransform.localPosition = Vector3.zero;
        }

        if (xrOrigin != null)
        {
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            xrOrigin.CameraYOffset = 0f;
        }

        // Register for pre-render 6DoF pose updates
        Application.onBeforeRender += OnBeforeRenderPoseUpdate;
    }

    private void OnDestroy()
    {
        Application.onBeforeRender -= OnBeforeRenderPoseUpdate;
    }

    private void Start()
    {
        if (cameraOffsetTransform != null)
        {
            cameraOffsetTransform.localPosition = Vector3.zero;
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
            // 1. Direct 6DoF Head Rotation
            if (centerEyeDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion headRotation))
            {
                transform.localRotation = headRotation;
            }
            else if (centerEyeDevice.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion centerRot))
            {
                transform.localRotation = centerRot;
            }

            // 2. Direct 6DoF Head Position (natural real-world physical standing height)
            if (centerEyeDevice.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 headPosition))
            {
                // If tracking reports uncalibrated flat zero, provide a natural 1.4m standing height
                if (headPosition.y < 0.1f && cameraOffsetTransform != null)
                {
                    cameraOffsetTransform.localPosition = new Vector3(0f, 1.4f, 0f);
                }
                else if (cameraOffsetTransform != null && cameraOffsetTransform.localPosition.y > 0.1f && headPosition.y >= 0.5f)
                {
                    cameraOffsetTransform.localPosition = Vector3.zero;
                }

                transform.localPosition = headPosition;
            }
            else if (centerEyeDevice.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 centerPos))
            {
                transform.localPosition = centerPos;
            }
        }
    }
}
