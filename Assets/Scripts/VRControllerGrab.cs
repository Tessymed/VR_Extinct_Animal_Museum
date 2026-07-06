using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class VRControllerGrab : MonoBehaviour
{
    [Header("Input Setup")]
    [Tooltip("The XRNode representing this hand (used for fallback input and haptics).")]
    public XRNode controllerNode = XRNode.RightHand;

    [Tooltip("The Input System Action for grabbing. If left unassigned, the script will fall back to reading the Grip button from the controllerNode directly.")]
    public InputActionProperty grabAction;

    [Tooltip("How far the grip must be pressed (0 to 1) to trigger a grab.")]
    [Range(0.1f, 0.9f)]
    public float grabThreshold = 0.5f;

    [Header("Visuals & Haptics")]
    [Tooltip("Optional reference to the controller visual mesh. Can be hidden when holding certain objects.")]
    public GameObject controllerMesh;

    [Tooltip("Haptic feedback amplitude when grabbing an object.")]
    [Range(0f, 1f)]
    public float grabHapticAmplitude = 0.5f;

    [Tooltip("Haptic feedback duration when grabbing an object.")]
    public float grabHapticDuration = 0.1f;

    [Header("Distance Grab Settings")]
    [Tooltip("Enable raycast-based distance grabbing.")]
    public bool enableDistanceGrab = true;

    [Tooltip("Maximum distance to grab objects from.")]
    public float distanceGrabRange = 15f;

    [Tooltip("Layer mask for grabbable objects.")]
    public LayerMask grabbableLayer = ~0;

    [Tooltip("How fast the object flies to the hand (lerp speed).")]
    public float flyToHandSpeed = 10f;

    [Tooltip("Create a visual laser pointer for targeting grabbables.")]
    public bool showLaserPointer = true;

    [Tooltip("Color of the laser when pointing at a grabbable object.")]
    public Color laserColor = Color.green;

    [Tooltip("Thickness of the laser pointer line.")]
    public float laserWidth = 0.005f;

    [Header("Debug")]
    [Tooltip("Enable console debug logs to troubleshoot grabbing triggers and inputs.")]
    public bool debugLogging = true;

    // State tracking
    private List<VRGrabbable> objectsInRange = new List<VRGrabbable>();
    private VRGrabbable heldObject;
    private FixedJoint joint;
    private bool wasGrabPressed;

    // Distance grab state tracking
    private bool isFlyingToHand = false;
    private float flyProgress = 0f;
    private Vector3 startWorldPos;
    private Quaternion startWorldRot;
    private LineRenderer grabLaser;

    // Velocity tracking history
    private const int BUFFER_SIZE = 10;
    private Vector3[] positionBuffer = new Vector3[BUFFER_SIZE];
    private Quaternion[] rotationBuffer = new Quaternion[BUFFER_SIZE];
    private float[] timeBuffer = new float[BUFFER_SIZE];
    private int bufferIndex = 0;
    private int currentBufferCount = 0;

    public VRGrabbable HeldObject => heldObject;

    private void Start()
    {
        // Check if there is a collider on this GameObject
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"[VR Grab] {gameObject.name} is missing a Collider component! Grabbing will not work. Please add a SphereCollider (with 'Is Trigger' checked) to the {gameObject.name} GameObject itself.", this);
            
            // Automatically add a SphereCollider to make grabbing work out-of-box
            SphereCollider sphereCol = gameObject.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 0.1f;
            col = sphereCol;
            if (debugLogging)
            {
                Debug.Log($"[VR Grab] Automatically added a SphereCollider (Is Trigger, radius = 0.1) to {gameObject.name} to enable close-range grabbing.");
            }
        }
        else if (!col.isTrigger)
        {
            Debug.LogWarning($"[VR Grab] {gameObject.name}'s Collider is not set to a Trigger! Please check 'Is Trigger' in the Inspector.", this);
        }

        // Automatically add a Kinematic Rigidbody to the controller to make trigger overlaps 100% reliable
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            if (debugLogging)
            {
                Debug.Log($"[VR Grab] Automatically added a Kinematic Rigidbody to {gameObject.name} to ensure trigger overlaps are detected.");
            }
        }

        // Setup the laser pointer for distance grabbing
        SetupGrabLaser();
    }

    private void OnEnable()
    {
        if (grabAction.action != null)
        {
            grabAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (grabAction.action != null)
        {
            grabAction.action.Disable();
        }
    }

    private void Update()
    {
        // Track position and rotation for throwing velocity
        TrackMovement();

        // Clean out any destroyed or disabled objects from trigger list
        CleanObjectsInRange();

        #if UNITY_EDITOR
        // Debug fallback: Press 'G' on the keyboard to grab whatever the controller or camera is targeting
        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
        {
            VRGrabbable target = null;

            // 1. Try to grab whatever the controller's distance grab is already targeting
            if (FindDistanceGrabbable(out RaycastHit laserHit, out VRGrabbable laserGrabbable))
            {
                target = laserGrabbable;
            }

            // 2. Fallback: Raycast from camera to find a grabbable
            if (target == null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                    RaycastHit camHit;
                    if (Physics.Raycast(ray, out camHit, distanceGrabRange))
                    {
                        target = GetGrabbableFromCollider(camHit.collider);
                    }
                }
            }

            if (target != null)
            {
                Debug.Log($"[Grab Debug] Keyboard 'G' triggered grab on: {target.name}");
                heldObject = target;
                target.Grab(this);
            }
        }
        #endif

        // Read input
        bool isGrabPressed = ReadGrabInput();

        // Debug input state changes
        if (debugLogging && isGrabPressed != wasGrabPressed)
        {
            Debug.Log($"[VR Grab] {gameObject.name} Grab button pressed state changed to: {isGrabPressed} (using {(grabAction.action != null && grabAction.action.bindings.Count > 0 ? "Input Action" : "Fallback hardware grip")})");
        }

        // Check for state changes (press / release)
        if (isGrabPressed && !wasGrabPressed)
        {
            // Attempt close-range grab first
            GrabClosest();

            // If we did not grab anything close, try distance grab
            if (heldObject == null && enableDistanceGrab)
            {
                TryDistanceGrab();
            }
        }
        else if (!isGrabPressed && wasGrabPressed)
        {
            ReleaseHeld();
        }

        wasGrabPressed = isGrabPressed;

        // Handle distance grab flight lerp
        if (isFlyingToHand && heldObject != null)
        {
            UpdateFlight();
        }

        // Update grab laser visuals
        if (enableDistanceGrab)
        {
            UpdateGrabLaser();
        }
    }

    private bool ReadGrabInput()
    {
        // Try reading from the Input Action
        if (grabAction.action != null && grabAction.action.bindings.Count > 0)
        {
            var value = grabAction.action.ReadValueAsObject();
            if (value is float floatVal)
            {
                return floatVal > grabThreshold;
            }
            if (value is Vector2 vectorVal)
            {
                return vectorVal.y > grabThreshold || vectorVal.magnitude > grabThreshold;
            }
            return grabAction.action.IsPressed();
        }

        // Fallback: Read directly from the hardware device using UnityEngine.XR
        return ReadFallbackGrip();
    }

    private bool ReadFallbackGrip()
    {
        var device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (device.isValid)
        {
            // Try analog grip value first
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float gripValue))
            {
                return gripValue > grabThreshold;
            }
            // Fallback to binary grip button
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gripPressed))
            {
                return gripPressed;
            }
        }
        return false;
    }

    private void TrackMovement()
    {
        bufferIndex = (bufferIndex + 1) % BUFFER_SIZE;
        positionBuffer[bufferIndex] = transform.position;
        rotationBuffer[bufferIndex] = transform.rotation;
        timeBuffer[bufferIndex] = Time.time;
        if (currentBufferCount < BUFFER_SIZE)
        {
            currentBufferCount++;
        }
    }

    private void GrabClosest()
    {
        if (debugLogging)
        {
            Debug.Log($"[VR Grab] {gameObject.name} GrabClosest() triggered. Objects in range: {objectsInRange.Count}");
        }

        if (heldObject != null) return;
        if (objectsInRange.Count == 0) return;

        VRGrabbable closest = null;
        float closestDistance = float.MaxValue;

        foreach (var grabbable in objectsInRange)
        {
            if (grabbable == null || grabbable.IsGrabbed && grabbable.grabType == VRGrabbable.GrabType.FixedJoint && grabbable.CurrentGrabber == this) 
                continue;

            float distance = Vector3.Distance(transform.position, grabbable.transform.position);
            if (distance < closestDistance)
            {
                closest = grabbable;
                closestDistance = distance;
            }
        }

        if (closest != null)
        {
            heldObject = closest;
            heldObject.Grab(this);

            // Create physics joint if configured for joint grab
            if (heldObject.grabType == VRGrabbable.GrabType.FixedJoint)
            {
                joint = gameObject.AddComponent<FixedJoint>();
                joint.breakForce = float.PositiveInfinity;
                joint.breakTorque = float.PositiveInfinity;
                joint.connectedBody = heldObject.Rigidbody;
            }

            // Optional: Hide controller visuals
            if (controllerMesh != null)
            {
                // In a production setup, we could have a check: if (heldObject.hideController)
                // For simplicity, we hide if controllerMesh is set.
                controllerMesh.SetActive(false);
            }

            // Trigger Haptics
            TriggerHaptics(grabHapticAmplitude, grabHapticDuration);
        }
    }

    private void ReleaseHeld()
    {
        if (heldObject == null) return;

        // Clean up joint if it exists instantly so it doesn't zero out velocity constraints
        if (joint != null)
        {
            DestroyImmediate(joint);
            joint = null;
        }

        // Calculate throwing velocities from hardware or history buffer
        Vector3 throwVelocity = GetControllerVelocity();
        Vector3 throwAngularVelocity = GetControllerAngularVelocity();

        // Release the object
        heldObject.Release(throwVelocity, throwAngularVelocity);
        heldObject = null;
        isFlyingToHand = false; // Reset flight state

        // Show controller visuals again
        if (controllerMesh != null)
        {
            controllerMesh.SetActive(true);
        }
    }

    /// <summary>
    /// Forces the release of the held object without applying throwing forces.
    /// Used when the other hand steals the object.
    /// </summary>
    public void ForceRelease()
    {
        if (heldObject == null) return;

        if (joint != null)
        {
            DestroyImmediate(joint);
            joint = null;
        }

        heldObject.Release(Vector3.zero, Vector3.zero);
        heldObject = null;
        isFlyingToHand = false; // Reset flight state

        if (controllerMesh != null)
        {
            controllerMesh.SetActive(true);
        }
    }

    private Vector3 GetTrackedVelocity()
    {
        if (currentBufferCount < 2) return Vector3.zero;

        int latest = bufferIndex;
        int oldest = (bufferIndex - currentBufferCount + 1 + BUFFER_SIZE) % BUFFER_SIZE;

        float timeDiff = timeBuffer[latest] - timeBuffer[oldest];
        if (timeDiff <= 0.0001f) return Vector3.zero;

        return (positionBuffer[latest] - positionBuffer[oldest]) / timeDiff;
    }

    private Vector3 GetTrackedAngularVelocity()
    {
        if (currentBufferCount < 2) return Vector3.zero;

        int latest = bufferIndex;
        int oldest = (bufferIndex - currentBufferCount + 1 + BUFFER_SIZE) % BUFFER_SIZE;

        float timeDiff = timeBuffer[latest] - timeBuffer[oldest];
        if (timeDiff <= 0.0001f) return Vector3.zero;

        Quaternion latestRot = rotationBuffer[latest];
        Quaternion oldestRot = rotationBuffer[oldest];
        Quaternion delta = latestRot * Quaternion.Inverse(oldestRot);

        delta.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f) angle -= 360f;

        // Convert angle change to radians per second
        return axis * (angle * Mathf.Deg2Rad / timeDiff);
    }

    private Vector3 GetControllerVelocity()
    {
        var device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (device.isValid)
        {
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceVelocity, out Vector3 velocity))
            {
                // Convert tracking space velocity to world space using XR Origin
                var originComponent = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
                Transform origin = originComponent != null ? originComponent.transform : transform.parent;
                if (origin != null)
                {
                    return origin.TransformDirection(velocity);
                }
                return velocity;
            }
        }
        return GetTrackedVelocity();
    }

    private Vector3 GetControllerAngularVelocity()
    {
        var device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (device.isValid)
        {
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceAngularVelocity, out Vector3 angularVelocity))
            {
                // Convert tracking space angular velocity to world space using XR Origin
                var originComponent = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
                Transform origin = originComponent != null ? originComponent.transform : transform.parent;
                if (origin != null)
                {
                    return origin.TransformDirection(angularVelocity);
                }
                return angularVelocity;
            }
        }
        return GetTrackedAngularVelocity();
    }

    private void TriggerHaptics(float amplitude, float duration)
    {
        var device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (device.isValid)
        {
            device.SendHapticImpulse(0, amplitude, duration);
        }
    }

    private VRGrabbable GetGrabbableFromCollider(Collider col)
    {
        if (col == null) return null;
        
        // Quick check on the collider's own GameObject
        VRGrabbable grabbable = col.GetComponent<VRGrabbable>();
        if (grabbable != null) return grabbable;

        // O(1) performance bypass for static geometry
        if (col.attachedRigidbody == null) return null;

        // Fallback for compound objects
        return col.GetComponentInParent<VRGrabbable>();
    }

    private void OnTriggerEnter(Collider other)
    {
        VRGrabbable grabbable = GetGrabbableFromCollider(other);
        if (grabbable != null)
        {
            if (debugLogging)
            {
                Debug.Log($"[VR Grab] {gameObject.name} entered trigger range of grabbable: {grabbable.name}", grabbable.gameObject);
            }

            if (!objectsInRange.Contains(grabbable))
            {
                objectsInRange.Add(grabbable);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        VRGrabbable grabbable = GetGrabbableFromCollider(other);
        if (grabbable != null)
        {
            if (debugLogging)
            {
                Debug.Log($"[VR Grab] {gameObject.name} exited trigger range of grabbable: {grabbable.name}", grabbable.gameObject);
            }

            if (objectsInRange.Contains(grabbable))
            {
                objectsInRange.Remove(grabbable);
            }
        }
    }

    private void CleanObjectsInRange()
    {
        for (int i = objectsInRange.Count - 1; i >= 0; i--)
        {
            if (objectsInRange[i] == null || !objectsInRange[i].gameObject.activeInHierarchy)
            {
                objectsInRange.RemoveAt(i);
            }
        }
    }

    private void SetupGrabLaser()
    {
        if (!showLaserPointer) return;

        GameObject laserObj = new GameObject("GrabLaserPointer");
        laserObj.transform.SetParent(transform);
        laserObj.transform.localPosition = Vector3.zero;
        laserObj.transform.localRotation = Quaternion.identity;

        grabLaser = laserObj.AddComponent<LineRenderer>();
        
        // Assign a default material to prevent the magenta 'missing shader' (pink) look
        grabLaser.material = new Material(Shader.Find("Sprites/Default"));
        
        grabLaser.startWidth = laserWidth;
        grabLaser.endWidth = laserWidth;
        grabLaser.startColor = laserColor;
        grabLaser.endColor = laserColor;
        grabLaser.positionCount = 2;
        grabLaser.enabled = false;
    }

    private bool FindDistanceGrabbable(out RaycastHit bestHit, out VRGrabbable bestGrabbable)
    {
        bestHit = new RaycastHit();
        bestGrabbable = null;

        Vector3 rayStart = transform.position + transform.forward * 0.1f;
        Ray ray = new Ray(rayStart, transform.forward);
        
        RaycastHit[] hits = Physics.RaycastAll(ray, distanceGrabRange, grabbableLayer, QueryTriggerInteraction.Ignore);
        
        // Sort hits by distance
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // Ignore colliders on the controller itself or its children
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.gameObject == gameObject)
                continue;

            VRGrabbable grabbable = GetGrabbableFromCollider(hit.collider);
            if (grabbable != null && grabbable.CurrentGrabber != this)
            {
                bestHit = hit;
                bestGrabbable = grabbable;
                return true;
            }
        }

        return false;
    }

    private void UpdateGrabLaser()
    {
        if (grabLaser == null) return;

        // If we are holding an object, hide the laser
        if (heldObject != null)
        {
            grabLaser.enabled = false;
            return;
        }

        if (FindDistanceGrabbable(out RaycastHit hit, out VRGrabbable grabbable))
        {
            grabLaser.enabled = true;
            grabLaser.SetPosition(0, transform.position);
            grabLaser.SetPosition(1, hit.point);
        }
        else
        {
            grabLaser.enabled = false;
        }
    }

    private void TryDistanceGrab()
    {
        if (debugLogging)
        {
            Debug.Log($"[VR Grab] {gameObject.name} TryDistanceGrab() triggered.");
        }

        if (FindDistanceGrabbable(out RaycastHit hit, out VRGrabbable grabbable))
        {
            if (grabbable.IsGrabbed && grabbable.CurrentGrabber == this)
            {
                return; // Already holding this
            }

            StartDistanceGrab(grabbable);
        }
    }

    private void StartDistanceGrab(VRGrabbable grabbable)
    {
        if (debugLogging)
        {
            Debug.Log($"[VR Grab] {gameObject.name} starting distance grab on: {grabbable.name}", grabbable.gameObject);
        }

        heldObject = grabbable;
        isFlyingToHand = true;
        flyProgress = 0f;

        // Capture starting position and rotation in world space
        startWorldPos = heldObject.transform.position;
        startWorldRot = heldObject.transform.rotation;

        // Call Grab on the object to set its state (e.g. kinematic, ownership)
        heldObject.Grab(this);

        // Override to kinematic during flight so manual lerp is smooth and doesn't fight physics
        if (heldObject.Rigidbody != null)
        {
            heldObject.Rigidbody.isKinematic = true;
            heldObject.Rigidbody.useGravity = false;
        }

        // Optional: Hide controller visuals
        if (controllerMesh != null)
        {
            controllerMesh.SetActive(false);
        }

        // Trigger initial grab haptics
        TriggerHaptics(grabHapticAmplitude, grabHapticDuration);
    }

    private void UpdateFlight()
    {
        flyProgress += Time.deltaTime * flyToHandSpeed;
        if (flyProgress >= 1f)
        {
            flyProgress = 1f;
            isFlyingToHand = false;
            FinishDistanceGrab();
        }
        else
        {
            // Calculate target world position and rotation based on controller's current transform
            Vector3 targetWorldPos;
            Quaternion targetWorldRot;

            if (heldObject.attachPoint != null)
            {
                // Align heldObject so its attachPoint matches the controller
                targetWorldRot = transform.rotation * Quaternion.Inverse(heldObject.attachPoint.localRotation);
                targetWorldPos = transform.position - (targetWorldRot * heldObject.attachPoint.localPosition);
            }
            else
            {
                targetWorldPos = transform.position;
                targetWorldRot = transform.rotation;
            }

            // Lerp in world space
            heldObject.transform.position = Vector3.Lerp(startWorldPos, targetWorldPos, flyProgress);
            heldObject.transform.rotation = Quaternion.Slerp(startWorldRot, targetWorldRot, flyProgress);
        }
    }

    private void FinishDistanceGrab()
    {
        if (heldObject == null) return;

        if (debugLogging)
        {
            Debug.Log($"[VR Grab] {gameObject.name} finished distance grab on: {heldObject.name}");
        }

        // Align to final position/rotation in world space
        Vector3 targetWorldPos;
        Quaternion targetWorldRot;

        if (heldObject.attachPoint != null)
        {
            targetWorldRot = transform.rotation * Quaternion.Inverse(heldObject.attachPoint.localRotation);
            targetWorldPos = transform.position - (targetWorldRot * heldObject.attachPoint.localPosition);
        }
        else
        {
            targetWorldPos = transform.position;
            targetWorldRot = transform.rotation;
        }

        heldObject.transform.position = targetWorldPos;
        heldObject.transform.rotation = targetWorldRot;

        // Apply grabbing attachment method
        if (heldObject.grabType == VRGrabbable.GrabType.Parenting)
        {
            heldObject.transform.SetParent(transform);
            
            // Re-align local transform for parenting
            if (heldObject.attachPoint != null)
            {
                heldObject.transform.localRotation = Quaternion.Inverse(heldObject.attachPoint.localRotation);
                heldObject.transform.localPosition = - (heldObject.transform.localRotation * heldObject.attachPoint.localPosition);
            }
            else
            {
                heldObject.transform.localPosition = Vector3.zero;
                heldObject.transform.localRotation = Quaternion.identity;
            }
        }
        else if (heldObject.grabType == VRGrabbable.GrabType.FixedJoint)
        {
            // Do NOT parent! Keep it in world space / original parent
            heldObject.transform.SetParent(heldObject.OriginalParent);

            // Make it non-kinematic so the FixedJoint works
            if (heldObject.Rigidbody != null)
            {
                heldObject.Rigidbody.isKinematic = false;
                heldObject.Rigidbody.useGravity = false;
            }

            // Create physics joint
            joint = gameObject.AddComponent<FixedJoint>();
            joint.breakForce = float.PositiveInfinity;
            joint.breakTorque = float.PositiveInfinity;
            joint.connectedBody = heldObject.Rigidbody;
        }

        // Trigger another subtle haptic pulse to confirm arrival
        TriggerHaptics(grabHapticAmplitude * 0.8f, grabHapticDuration);
    }
}
