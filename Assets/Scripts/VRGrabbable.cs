using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class VRGrabbable : MonoBehaviour
{
    public enum GrabType
    {
        Parenting,     // Kinematic attachment; follows hand exactly, passes through walls
        FixedJoint     // Physics-based attachment; collides with environment, doesn't pass through walls
    }

    [Header("Grab Settings")]
    [Tooltip("How the object attaches to the controller.")]
    public GrabType grabType = GrabType.Parenting;

    [Tooltip("Optional offset transform. If set, this point will align exactly with the controller's grab point. If null, the object's center will snap to the controller.")]
    public Transform attachPoint;

    [Header("Physics & Throwing")]
    [Tooltip("Should the object become kinematic (non-physics driven) while held? (Highly recommended for Parenting grab type)")]
    public bool isKinematicWhileHeld = true;

    [Tooltip("Multiplier applied to the controller's velocity when throwing.")]
    public float throwVelocityMultiplier = 1.2f;

    [Tooltip("Multiplier applied to the controller's rotational velocity when throwing.")]
    public float throwAngularVelocityMultiplier = 1.0f;

    [Header("Events")]
    public UnityEvent<VRControllerGrab> OnGrabbed;
    public UnityEvent<VRControllerGrab> OnReleased;

    // Cache the original physics state
    private Rigidbody rb;
    private Transform originalParent;
    private bool originalIsKinematic;
    private bool originalUseGravity;
    private float originalDrag;
    private float originalAngularDrag;

    // Keep track of the current grabber
    private VRControllerGrab currentGrabber;

    public Rigidbody Rigidbody => rb;
    public bool IsGrabbed => currentGrabber != null;
    public VRControllerGrab CurrentGrabber => currentGrabber;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        originalParent = transform.parent;
        CacheOriginalPhysicsState();
    }

    private void CacheOriginalPhysicsState()
    {
        if (rb != null)
        {
            originalIsKinematic = rb.isKinematic;
            originalUseGravity = rb.useGravity;
            originalDrag = rb.linearDamping;
            originalAngularDrag = rb.angularDamping;
        }
    }

    /// <summary>
    /// Called by the controller script when grabbing this object.
    /// </summary>
    public void Grab(VRControllerGrab grabber)
    {
        // If already grabbed by someone else, release it first (allows stealing)
        if (currentGrabber != null && currentGrabber != grabber)
        {
            currentGrabber.ForceRelease();
        }

        currentGrabber = grabber;

        // Cache physics state in case it changed since awake
        CacheOriginalPhysicsState();

        if (grabType == GrabType.Parenting)
        {
            if (isKinematicWhileHeld && rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            transform.SetParent(grabber.transform);
            AlignToGrabber(grabber.transform);
        }
        else if (grabType == GrabType.FixedJoint)
        {
            // Position object correctly before creating joint
            AlignToGrabber(grabber.transform);

            if (isKinematicWhileHeld && rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        OnGrabbed?.Invoke(grabber);
    }

    /// <summary>
    /// Called by the controller script when releasing this object.
    /// </summary>
    public void Release(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        if (currentGrabber == null) return;

        // Restore parenting
        if (grabType == GrabType.Parenting)
        {
            transform.SetParent(originalParent);
        }

        // Restore physics properties
        if (rb != null)
        {
            rb.isKinematic = originalIsKinematic;
            rb.useGravity = originalUseGravity;
            rb.linearDamping = originalDrag;
            rb.angularDamping = originalAngularDrag;

            // Apply throwing velocities
            rb.linearVelocity = linearVelocity * throwVelocityMultiplier;
            rb.angularVelocity = angularVelocity * throwAngularVelocityMultiplier;
        }

        var releasedGrabber = currentGrabber;
        currentGrabber = null;

        OnReleased?.Invoke(releasedGrabber);
    }

    /// <summary>
    /// Aligns the object (or its attachPoint) to the grabber's transform.
    /// </summary>
    private void AlignToGrabber(Transform grabberTransform)
    {
        if (attachPoint != null)
        {
            // Rotate the object so the attach point aligns with the grabber rotation
            transform.rotation = grabberTransform.rotation * Quaternion.Inverse(attachPoint.localRotation);
            // Translate the object so the attach point aligns with the grabber position
            transform.position = grabberTransform.position - (transform.rotation * attachPoint.localPosition);
        }
        else
        {
            // Default snap to center
            transform.position = grabberTransform.position;
            transform.rotation = grabberTransform.rotation;
        }
    }
}
