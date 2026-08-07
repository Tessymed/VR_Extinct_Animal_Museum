using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

[RequireComponent(typeof(LineRenderer))]
public class Teleport_now : MonoBehaviour
{
    [Header("XR Setup")]
    [Tooltip("The Transform of the XR Origin (the player's play space root).")]
    public Transform xrOrigin;

    [Tooltip("The Main Camera of the VR headset.")]
    public Camera mainCamera;

    [Header("Input Setup")]
    [Tooltip("The XRNode representing this controller (for fallback input).")]
    public XRNode controllerNode = XRNode.LeftHand;

    [Tooltip("The Input System Action for teleporting. If unassigned, joystick forward will activate teleportation, and returning joystick to center will trigger the teleport.")]
    public InputActionProperty teleportAction;

    [Header("Teleport Settings")]
    [Tooltip("Maximum teleportation range.")]
    public float maxRange = 15f;

    [Tooltip("Layers representing walkable ground.")]
    public LayerMask groundLayers = ~0; // Default to all layers

    [Tooltip("Tag required for walkable surfaces. Leave empty if any collider on the groundLayers is walkable.")]
    public string walkableTag = "Walkable";

    [Tooltip("Maximum allowed slope angle (in degrees) to teleport onto.")]
    [Range(10f, 80f)]
    public float maxSlopeAngle = 45f;

    [Header("Visuals")]
    [Tooltip("Optional prefab/GameObject to position at the teleport destination (e.g., a circle on the ground).")]
    public GameObject teleportReticle;

    [Tooltip("Color of the laser when pointing at a valid teleport location.")]
    public Color validColor = Color.cyan;

    [Tooltip("Color of the laser when pointing at an invalid location.")]
    public Color invalidColor = Color.red;

    [Header("Parabolic Curve Settings")]
    [Tooltip("Should the laser pointer be a curved arc instead of a straight line?")]
    public bool useParabolicCurve = true;

    [Tooltip("The initial speed of the simulated teleport projectile.")]
    public float launchSpeed = 10f;

    [Tooltip("The simulation resolution (number of segments in the curve).")]
    [Range(10, 100)]
    public int curveResolution = 30;

    [Tooltip("Gravity acceleration scale for the curve simulation.")]
    public float gravityScale = 1.5f;

    // Component cache
    private LineRenderer lineRenderer;
    private bool wasTeleportActive;
    private Vector3 targetPosition;
    private bool isValidTargetHit;
    private Vector3[] pointsBuffer;
    private float nextLogTime;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        pointsBuffer = new Vector3[curveResolution];
        
        // Setup default LineRenderer settings if not configured
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;
        lineRenderer.enabled = false;

        if (teleportReticle != null)
        {
            teleportReticle.SetActive(false);
        }
    }

    private void Start()
    {
        // Try to automatically find the XR Origin if not assigned
        if (xrOrigin == null)
        {
            var originComponent = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (originComponent != null)
            {
                xrOrigin = originComponent.transform;
            }
        }

        // Try to automatically find the Main Camera under the XR Origin
        if (xrOrigin != null && mainCamera == null)
        {
            mainCamera = xrOrigin.GetComponentInChildren<Camera>();
        }

        // Setup diagnostics to scan scene colliders on start
        var colliders = FindObjectsByType<Collider>(FindObjectsInactive.Exclude);
        Debug.Log($"[Teleport Setup Diagnostic] Scanning scene... Found {colliders.Length} total colliders.");
        foreach (var col in colliders)
        {
            if (col.CompareTag(walkableTag) || col.name.ToLower().Contains("floor") || col.name.ToLower().Contains("museum"))
            {
                Debug.Log($"[Teleport Setup Diagnostic] Found Match: GameObject='{col.gameObject.name}' | Tag='{col.tag}' | Layer='{LayerMask.LayerToName(col.gameObject.layer)}' | Enabled={col.enabled} | IsTrigger={col.isTrigger} | Bounds={col.bounds}");
            }
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
            AimTeleport();
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
        // Debug fallback: Hold 'T' key on keyboard to teleport in the Unity Editor
        if (Keyboard.current != null && Keyboard.current.tKey.isPressed)
        {
            return true;
        }
        #endif

        // Try reading from the Input Action
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

        // Fallback: Read directly from the hardware joystick
        return ReadFallbackJoystick();
    }

    private bool ReadFallbackJoystick()
    {
        var device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (device.isValid)
        {
            // Read primary joystick/touchpad axis
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 axis))
            {
                return axis.y > 0.5f;
            }
        }
        return false;
    }

    private void AimTeleport()
    {
        if (useParabolicCurve)
        {
            AimTeleportParabolic();
        }
        else
        {
            AimTeleportStraight();
        }
    }

    private void AimTeleportStraight()
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

        // Start the ray slightly in front of the origin to prevent self-collision
        Vector3 rayStart = originPos + originForward * 0.15f;
        Ray ray = new Ray(rayStart, originForward);
        RaycastHit hit;

        // Perform raycast, ignoring triggers
        if (Physics.Raycast(ray, out hit, maxRange, groundLayers, QueryTriggerInteraction.Ignore))
        {
            // Verify if surface is flat enough (slope check)
            float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
            bool isSlopeValid = slopeAngle <= maxSlopeAngle;

            // Verify if tag matches (if specified)
            bool isTagValid = string.IsNullOrEmpty(walkableTag) || hit.collider.CompareTag(walkableTag);

            if (isSlopeValid && isTagValid)
            {
                isValidTargetHit = true;
                targetPosition = hit.point;

                // Configure visuals for valid hit
                SetLaserColor(validColor);
                
                if (teleportReticle != null)
                {
                    teleportReticle.SetActive(true);
                    teleportReticle.transform.position = hit.point + Vector3.up * 0.01f; // Avoid z-fighting
                    teleportReticle.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                }
            }
            else
            {
                // Hit something, but it's not a walkable target (e.g. wall, ceiling)
                SetLaserColor(invalidColor);
                if (teleportReticle != null)
                {
                    teleportReticle.SetActive(false);
                }

                if (Time.time > nextLogTime)
                {
                    Debug.Log($"[Teleport Debug] Hit non-walkable object: '{hit.collider.name}' | Tag: '{hit.collider.tag}' | Slope: {slopeAngle:F1}° (Max: {maxSlopeAngle}°)");
                    nextLogTime = Time.time + 1.0f;
                }
            }

            // Draw line to the hit point
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, originPos);
            lineRenderer.SetPosition(1, hit.point);
        }
        else
        {
            // Didn't hit anything
            SetLaserColor(invalidColor);
            if (teleportReticle != null)
            {
                teleportReticle.SetActive(false);
            }

            if (Time.time > nextLogTime)
            {
                Debug.Log($"[Teleport Debug] Straight raycast did not hit any collider. Ray started at {rayStart} pointing {originForward}");
                nextLogTime = Time.time + 1.0f;
            }

            // Draw line extending out to max range
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, originPos);
            lineRenderer.SetPosition(1, originPos + originForward * maxRange);
        }
    }

    private void AimTeleportParabolic()
    {
        lineRenderer.enabled = true;
        isValidTargetHit = false;

        // Ensure points buffer is valid and correct size
        if (pointsBuffer == null || pointsBuffer.Length != curveResolution)
        {
            pointsBuffer = new Vector3[curveResolution];
        }
        int pointsCount = 0;

        Vector3 originPos = transform.position;
        Vector3 originForward = transform.forward;

        #if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.tKey.isPressed && mainCamera != null)
        {
            originPos = mainCamera.transform.position;
            originForward = mainCamera.transform.forward;
        }
        #endif

        Vector3 startPosition = originPos + originForward * 0.15f;
        Vector3 velocity = originForward * launchSpeed;
        Vector3 gravity = Physics.gravity * gravityScale;

        pointsBuffer[0] = startPosition;
        pointsCount = 1;

        Vector3 currentPosition = startPosition;
        float timeStep = 0.04f; // Time interval between segments
        bool hasHit = false;
        RaycastHit hit;

        for (int i = 1; i < curveResolution; i++)
        {
            float t = i * timeStep;
            // Formula: position = p0 + v0*t + 0.5*g*t^2
            Vector3 nextPosition = startPosition + velocity * t + 0.5f * gravity * t * t;

            Vector3 segmentDirection = nextPosition - currentPosition;
            float segmentDistance = segmentDirection.magnitude;

            // Check collision for this segment
            if (Physics.Raycast(currentPosition, segmentDirection, out hit, segmentDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                hasHit = true;
                pointsBuffer[i] = hit.point;
                pointsCount = i + 1;

                // Validate hit point
                float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                bool isSlopeValid = slopeAngle <= maxSlopeAngle;
                bool isTagValid = string.IsNullOrEmpty(walkableTag) || hit.collider.CompareTag(walkableTag);

                if (isSlopeValid && isTagValid)
                {
                    isValidTargetHit = true;
                    targetPosition = hit.point;
                    SetLaserColor(validColor);

                    if (teleportReticle != null)
                    {
                        teleportReticle.SetActive(true);
                        teleportReticle.transform.position = hit.point + Vector3.up * 0.01f;
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

                    if (Time.time > nextLogTime)
                    {
                        Debug.Log($"[Teleport Debug] Parabolic hit non-walkable object: '{hit.collider.name}' | Tag: '{hit.collider.tag}' | Slope: {slopeAngle:F1}° (Max: {maxSlopeAngle}°)");
                        nextLogTime = Time.time + 1.0f;
                    }
                }
                break; // Stop simulating segment once we hit a surface
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

            if (Time.time > nextLogTime)
            {
                Debug.Log($"[Teleport Debug] Parabolic raycast did not hit any collider. Start: {startPosition}, LaunchSpeed: {launchSpeed}, GravityScale: {gravityScale}");
                nextLogTime = Time.time + 1.0f;
            }
        }

        // Apply generated points to LineRenderer
        lineRenderer.positionCount = pointsCount;
        for (int i = 0; i < pointsCount; i++)
        {
            lineRenderer.SetPosition(i, pointsBuffer[i]);
        }
    }

    private void TryExecuteTeleport()
    {
        // Turn off visuals
        lineRenderer.enabled = false;
        if (teleportReticle != null)
        {
            teleportReticle.SetActive(false);
        }

        // Teleport the player if targeting a valid position
        if (isValidTargetHit && xrOrigin != null && mainCamera != null)
        {
            // Calculate vector offset from the XR Origin center to the player's head (camera)
            Vector3 cameraOffset = mainCamera.transform.position - xrOrigin.position;
            cameraOffset.y = 0; // We only care about horizontal offset (keep origin floor height)

            // Shift the XR Origin so that the player's camera aligns with the target position
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
