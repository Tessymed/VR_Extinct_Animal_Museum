using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(VRGrabbable))]
public class VRExhibitTrigger : MonoBehaviour
{
    [Header("Audio Narration")]
    [Tooltip("The AudioSource used to play the narration. If unassigned, one will be created or found on this object.")]
    public AudioSource audioSource;
    
    [Tooltip("The narration audio clip for this exhibit.")]
    public AudioClip narrationClip;

    [Header("UI Integration")]
    [Tooltip("The exhibit info panel that should be activated when this trigger is used.")]
    public VRExhibitPanel exhibitPanel;

    [Header("Visual Effects & Feedback")]
    [Tooltip("Optional particle system to trigger on contact.")]
    public ParticleSystem interactionParticles;

    [Tooltip("Should the controller force-release this object immediately upon trigger? (Recommended for stationary elements like dials/ice crystals)")]
    public bool releaseGrabberOnTrigger = false;

    [Header("Custom Visual/Story Events")]
    [Tooltip("Custom events triggered when the user grabs/triggers this object (e.g. fading in silhouettes, changing scene light, etc.).")]
    public UnityEvent onTrigger;

    [Header("Auto Reset Settings")]
    [Tooltip("Should the exhibit auto-reset when the player walks away?")]
    public bool autoResetOnLeave = true;

    [Tooltip("How far away (in meters) the player must walk to trigger the reset.")]
    public float resetDistance = 4.0f;

    private VRGrabbable grabbable;
    private bool hasBeenTriggered = false;

    private void Awake()
    {
        grabbable = GetComponent<VRGrabbable>();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }

    private void OnEnable()
    {
        if (grabbable != null)
        {
            grabbable.OnGrabbed.AddListener(OnGrabbed);
        }
    }

    private void OnDisable()
    {
        if (grabbable != null)
        {
            grabbable.OnGrabbed.RemoveListener(OnGrabbed);
        }
    }

    private void OnGrabbed(VRControllerGrab grabber)
    {
        if (hasBeenTriggered) return;
        hasBeenTriggered = true;

        // Play particle system if available
        if (interactionParticles != null)
        {
            interactionParticles.Play();
        }

        // Play audio narration
        if (audioSource != null && narrationClip != null)
        {
            audioSource.clip = narrationClip;
            audioSource.Play();
        }

        // Activate and display the info panel
        if (exhibitPanel != null)
        {
            exhibitPanel.ActivatePanel();
        }

        // Execute custom events
        onTrigger?.Invoke();

        // Release the controller immediately if configured
        if (grabber != null && releaseGrabberOnTrigger)
        {
            grabber.ForceRelease();
        }
    }

    private void Update()
    {
        if (autoResetOnLeave && hasBeenTriggered)
        {
            // Find player camera (VR Headset / Main Camera)
            Transform playerTransform = null;
            var cam = Camera.main;
            if (cam != null)
            {
                playerTransform = cam.transform;
            }
            else
            {
                var origin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (origin != null)
                {
                    playerTransform = origin.transform;
                }
            }

            if (playerTransform != null)
            {
                float distance = Vector3.Distance(transform.position, playerTransform.position);
                if (distance > resetDistance)
                {
                    ResetExhibit();
                }
            }
        }
    }

    private void ResetExhibit()
    {
        ResetTrigger();
        if (exhibitPanel != null)
        {
            exhibitPanel.ResetPanel();
        }
    }

    /// <summary>
    /// Resets the trigger state so it can be activated again (e.g., if re-entering the room).
    /// </summary>
    public void ResetTrigger()
    {
        hasBeenTriggered = false;
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
