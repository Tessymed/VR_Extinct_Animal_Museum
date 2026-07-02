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
