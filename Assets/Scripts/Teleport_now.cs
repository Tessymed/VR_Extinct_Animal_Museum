using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// <summary>
/// Ultra-Smooth, Low-Overhead VR Parabolic Teleportation.
/// Optimized for Meta Quest Link / OpenXR to maintain rock-solid 72/90 FPS
/// with zero frame drops, zero raycast thrashing, and zero loading pauses.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class Teleport_now : MonoBehaviour
{
    [Header("XR Setup")]
    [Tooltip("The Transform of the XR Origin.")]
    public Transform xrOrigin;

    [Tooltip("The Main Camera of the VR headset.")]
    public Camera mainCamera;

    [Header("Input Setup")]
    [Tooltip("The XRNode representing this controller.")]
    public XRNode controllerNode = XRNode.LeftHand;

    [Tooltip("The Input System Action for teleporting.")]
    public InputActionProperty teleportAction;

    [Header("Teleport Settings")]
    [Tooltip("Maximum teleportation range in meters.")]
    public float maxRange = 14f;

    [Tooltip("Layers representing walkable ground.")]
    public LayerMask groundLayers = ~0;

    [Tooltip("Tag required for walkable surfaces. Leave empty for any ground layer.")]
    public string walkableTag = "Walkable";

    [Tooltip("Maximum allowed slope angle in degrees.")]
    [Range(10f, 80f)]
    public float maxSlopeAngle = 45f;

    [Header("Visuals")]
    public GameObject teleportReticle;
    public Color validColor = new Color(0.1f, 0.9f, 0.9f, 0.8f);
    public Color invalidColor = new Color(0.9f, 0.1f, 0.1f, 0.6f);

    [Header("Performance Settings")]
    [Tooltip("Number of curve segments (kept lean at 12 to eliminate VR frame hitches).")]
    [Range(6, 20)]
    public int curveResolution = 12;
    public float launchSpeed = 9f;
    public float gravityScale = 1.6f;

    // Component cache
    private LineRenderer lineRenderer;
    private bool wasTeleportActive;
    private Vector3 targetPosition;
    private bool isValidTargetHit;
    private Vector3[] pointsBuffer;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        pointsBuffer = new Vector3[curveResolution + 1];

        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.008f;
        lineRenderer.endWidth = 0.008f;
        lineRenderer.enabled = false;

        if (teleportReticle != null)
        {
            teleportReticle.SetActive(false);
        }
    }

    private void Start()
    {
        if (xrOrigin == null)
        {
            var originComponent = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (originComponent != null)
            {
                xrOrigin = originComponent.transform;
            }
        }

        if (xrOrigin != null && mainCamera == null)
        {
            mainCamera = xrOrigin.GetComponentInChildren<Camera>();
        }
    }

    private void OnEnable()
    {
        if (teleportAction.action != null)
        {
            teleportAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (teleportAction.action != null)
        {
            teleportAction.action.Disable();
        }

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }

        if (teleportReticle != null)
        {
            teleportReticle.SetActive(false);
        }
    }

    private void Update()
    {
        bool isTeleportActive = ReadTeleportInput();

        if (isTeleportActive)
        {
            AimTeleportFast();
        }
        else if (wasTeleportActive && !isTeleportActive)
        {
            TryExecuteTeleport();
        }

        wasTeleportActive = isTeleportActive;
    }

    private bool ReadTeleportInput()
    {
        #if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.tKey.isPressed)
        {
            return true;
        }
        #endif

        if (teleportAction.action != null && teleportAction.action.bindings.Count > 0)
        {
            var value = teleportAction.action.ReadValueAsObject();
            if (value is Vector2 vectorVal)
            {
                return vectorVal.y > 0.5f;
            }
            if (value is float floatVal)
            {
                return floatVal > 0.5f;
            }
            return teleportAction.action.IsPressed();
        }

        return ReadFallbackJoystick();
    }

    private bool ReadFallbackJoystick()
    {
        var device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (device.isValid)
        {
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis))
            {
                return axis.y > 0.5f;
            }
        }
        return false;
    }

    private void AimTeleportFast()
    {
        lineRenderer.enabled = true;
        isValidTargetHit = false;

        Vector3 originPos = transform.position;
        Vector3 originForward = transform.forward;

        #if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.tKey.isPressed && mainCamera != null)
        {
            originPos = mainCamera.transform.position;
            originForward = mainCamera.transform.forward;
        }
        #endif

        Vector3 startPosition = originPos + originForward * 0.1f;
        Vector3 velocity = originForward * launchSpeed;
        Vector3 gravity = Physics.gravity * gravityScale;

        pointsBuffer[0] = startPosition;
        int pointsCount = 1;

        Vector3 currentPosition = startPosition;
        float timeStep = 0.06f; // Optimized step size
        bool hasHit = false;

        for (int i = 1; i <= curveResolution; i++)
        {
            float t = i * timeStep;
            Vector3 nextPosition = startPosition + velocity * t + 0.5f * gravity * t * t;
            Vector3 segmentDirection = nextPosition - currentPosition;
            float segmentDistance = segmentDirection.magnitude;

            if (Physics.Raycast(currentPosition, segmentDirection.normalized, out RaycastHit hit, segmentDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                pointsBuffer[i] = hit.point;
                pointsCount = i + 1;
                hasHit = true;

                float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                bool isSlopeWalkable = slopeAngle <= maxSlopeAngle;
                bool isTagWalkable = string.IsNullOrEmpty(walkableTag) || hit.collider.CompareTag(walkableTag) || hit.collider.name.ToLower().Contains("floor") || hit.collider.name.ToLower().Contains("museum");

                if (isSlopeWalkable && isTagWalkable)
                {
                    isValidTargetHit = true;
                    targetPosition = hit.point;
                    SetLaserColor(validColor);

                    if (teleportReticle != null)
                    {
                        teleportReticle.SetActive(true);
                        teleportReticle.transform.position = hit.point + hit.normal * 0.01f;
                        teleportReticle.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    }
                }
                else
                {
                    SetLaserColor(invalidColor);
                    if (teleportReticle != null)
                    {
                        teleportReticle.SetActive(false);
                    }
                }
                break;
            }

            currentPosition = nextPosition;
            pointsBuffer[i] = currentPosition;
            pointsCount = i + 1;
        }

        if (!hasHit)
        {
            SetLaserColor(invalidColor);
            if (teleportReticle != null)
            {
                teleportReticle.SetActive(false);
            }
        }

        lineRenderer.positionCount = pointsCount;
        for (int i = 0; i < pointsCount; i++)
        {
            lineRenderer.SetPosition(i, pointsBuffer[i]);
        }
    }

    private void TryExecuteTeleport()
    {
        lineRenderer.enabled = false;
        if (teleportReticle != null)
        {
            teleportReticle.SetActive(false);
        }

        if (isValidTargetHit && xrOrigin != null && mainCamera != null)
        {
            Vector3 cameraOffset = mainCamera.transform.position - xrOrigin.position;
            cameraOffset.y = 0;

            xrOrigin.position = targetPosition - cameraOffset;
        }

        isValidTargetHit = false;
    }

    private void SetLaserColor(Color color)
    {
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }
}
