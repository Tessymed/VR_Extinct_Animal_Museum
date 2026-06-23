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

    [Header("Debug")]
    [Tooltip("Enable console debug logs to troubleshoot grabbing triggers and inputs.")]
    public bool debugLogging = true;

    // State tracking
    private List<VRGrabbable> objectsInRange = new List<VRGrabbable>();
    private VRGrabbable heldObject;
    private FixedJoint joint;
    private bool wasGrabPressed;

    // Velocity tracking history
    private const int BUFFER_SIZE = 10;
    private Vector3[] positionBuffer = new Vector3[BUFFER_SIZE];
    private Quaternion[] rotationBuffer = new Quaternion[BUFFER_SIZE];
    private float[] timeBuffer = new float[BUFFER_SIZE];
    private int bufferIndex = 0;
    private int currentBufferCount = 0;

    public VRGrabbable HeldObject => heldObject;

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
            GrabClosest();
        }
        else if (!isGrabPressed && wasGrabPressed)
        {
            ReleaseHeld();
        }

        wasGrabPressed = isGrabPressed;
    }

    private bool ReadGrabInput()
    {
        // Try reading from the Input Action
        if (grabAction.action != null && grabAction.action.bindings.Count > 0)
        {
            // If the action resolves to a float (analog grip trigger), read value
            if (grabAction.action.activeControl is InputControl<float> floatControl)
            {
                return floatControl.ReadValue() > grabThreshold;
            }
            // Otherwise check if it's a binary button press
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

        // Clean up joint if it exists
        if (joint != null)
        {
            Destroy(joint);
            joint = null;
        }

        // Calculate throwing velocities from history buffer
        Vector3 throwVelocity = GetTrackedVelocity();
        Vector3 throwAngularVelocity = GetTrackedAngularVelocity();

        // Release the object
        heldObject.Release(throwVelocity, throwAngularVelocity);
        heldObject = null;

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
            Destroy(joint);
            joint = null;
        }

        heldObject.Release(Vector3.zero, Vector3.zero);
        heldObject = null;

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

    private void TriggerHaptics(float amplitude, float duration)
    {
        var device = InputDevices.GetDeviceAtXRNode(controllerNode);
        if (device.isValid)
        {
            device.SendHapticImpulse(0, amplitude, duration);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        VRGrabbable grabbable = other.GetComponentInParent<VRGrabbable>();
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
        VRGrabbable grabbable = other.GetComponentInParent<VRGrabbable>();
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
}
