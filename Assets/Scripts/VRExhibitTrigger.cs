using System.Collections;
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
    public VRExhibitPanelUI exhibitPanel;

    [Header("Exhibit Story (ScriptableObject)")]
    [Tooltip("Drag the DodoStory / MammothStory / ThylacineStory asset here.")]
    public ExhibitStory exhibitStory;

    [Header("Exhibit Content (Legacy — leave empty if using ExhibitStory above)")]
    [Tooltip("The structured animal data for this exhibit.")]
    public VRExhibitPanelUI.AnimalExhibitData animalData;

    [Header("Visual Effects & Feedback")]
    [Tooltip("Optional particle system to trigger on contact.")]
    public ParticleSystem interactionParticles;

    [Tooltip("Should the controller force-release this object immediately upon trigger? (Recommended for stationary elements like dials/ice crystals)")]
    public bool releaseGrabberOnTrigger = true;

    [Header("Custom Visual/Story Events")]
    [Tooltip("Custom events triggered when the user grabs/triggers this object (e.g. fading in silhouettes, changing scene light, etc.).")]
    public UnityEvent onTrigger;

    [Header("Auto Reset Settings")]
    [Tooltip("Should the exhibit auto-reset when the player walks far away? (Disabled by default so players can read at their own pace)")]
    public bool autoResetOnLeave = false;

    [Tooltip("How far away (in meters) the player must walk to trigger the auto-reset.")]
    public float resetDistance = 50.0f;

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
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.0f; // 2D stereo clear museum narration directly into headset
            audioSource.volume = 1.0f;
        }

        if (interactionParticles == null || !interactionParticles.gameObject.scene.isLoaded)
        {
            interactionParticles = GetComponentInChildren<ParticleSystem>();
        }
    }

    private void Start()
    {
        // Enforce 100% invisible UI card at scene startup
        if (exhibitPanel != null)
        {
            exhibitPanel.HidePanel();
        }
        else
        {
            Debug.LogWarning($"[VRExhibitTrigger] '{gameObject.name}' is missing an Exhibit Panel reference!", this);
        }

        // Disable old legacy 3D canvas texts at startup to prevent text overlay
        DisableLegacyExhibitCanvases();
    }

    private void OnEnable()
    {
        if (grabbable != null)
        {
            grabbable.OnGrabbed.AddListener(OnGrabbed);
        }
        if (exhibitPanel != null)
        {
            exhibitPanel.OnPanelClosed -= ResetExhibit;
            exhibitPanel.OnPanelClosed += ResetExhibit;
        }
    }

    private void OnDisable()
    {
        if (grabbable != null)
        {
            grabbable.OnGrabbed.RemoveListener(OnGrabbed);
        }
        if (exhibitPanel != null)
        {
            exhibitPanel.OnPanelClosed -= ResetExhibit;
        }
    }

    private void OnGrabbed(VRControllerGrab grabber)
    {
        // Release the controller immediately so the user does not hold/carry the crystal
        if (grabber != null && releaseGrabberOnTrigger)
        {
            grabber.ForceRelease();
        }

        if (hasBeenTriggered) return;
        hasBeenTriggered = true;

        Debug.Log($"[VRExhibitTrigger] Triggered exhibit for: {animalData.animalName}");

        // 1. Hide/destroy crystal object mesh & colliders immediately
        try
        {
            SetCrystalVisible(false);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VRExhibitTrigger] Exception hiding crystal: {ex.Message}");
        }

        // 2. Stop ALL particle systems on crystal & nearby in scene (e.g. Loading_free_blue)
        try
        {
            StopAllParticles();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VRExhibitTrigger] Exception stopping particles: {ex.Message}");
        }

        // 3. Disable any legacy 3D canvas texts to prevent overlay
        try
        {
            DisableLegacyExhibitCanvases();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VRExhibitTrigger] Exception disabling legacy canvases: {ex.Message}");
        }

        // 4. Play loud, clear audio narration directly in headset
        try
        {
            if (audioSource != null && narrationClip != null)
            {
                audioSource.spatialBlend = 0.0f; // Clear head-locked stereo narration
                audioSource.volume = 1.0f;
                audioSource.PlayOneShot(narrationClip);
                Debug.Log($"[VRExhibitTrigger] Playing narration audio: {narrationClip.name}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VRExhibitTrigger] Exception playing audio: {ex.Message}");
        }

        // 5. Pop up UI Info Panel maintaining the exact Editor position and rotation
        try
        {
            if (exhibitPanel != null)
            {
                exhibitPanel.gameObject.SetActive(true);

                // Prefer ScriptableObject story; fall back to legacy inline data
                if (exhibitStory != null)
                {
                    exhibitPanel.ShowStory(exhibitStory);
                    Debug.Log($"[VRExhibitTrigger] ShowStory called with '{exhibitStory.animalName}'");
                }
                else
                {
                    exhibitPanel.ShowExhibit(animalData);
                    Debug.Log($"[VRExhibitTrigger] ShowExhibit (legacy) called for '{animalData.animalName}'");
                }
            }
            else
            {
                Debug.LogError($"[VRExhibitTrigger] exhibitPanel reference is NULL on '{gameObject.name}'!");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VRExhibitTrigger] Exception showing UI exhibit panel: {ex.Message}");
        }

        onTrigger?.Invoke();
    }

    private void DisableLegacyExhibitCanvases()
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (var canvas in canvases)
        {
            if (canvas != null && canvas.gameObject.scene.isLoaded && Vector3.Distance(transform.position, canvas.transform.position) < 4.0f)
            {
                // Never disable our active TMP exhibit card canvas!
                if (canvas.GetComponentInParent<VRExhibitPanelUI>() != null || canvas.gameObject.name == "ExhibitCanvas")
                {
                    continue;
                }

                if (canvas.GetComponent<UnityEngine.UIElements.UIDocument>() == null)
                {
                    canvas.gameObject.SetActive(false);
                }
            }
        }

        var oldPanels = FindObjectsByType<VRExhibitPanel>(FindObjectsInactive.Include);
        foreach (var op in oldPanels)
        {
            if (op != null && op.gameObject.scene.isLoaded && Vector3.Distance(transform.position, op.transform.position) < 4.0f)
            {
                op.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (autoResetOnLeave && hasBeenTriggered)
        {
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
                    Debug.Log($"[VRExhibitTrigger] Player walked {distance:F1}m away (> {resetDistance}m). Auto-resetting exhibit for: {animalData.animalName}");
                    ResetExhibit();
                }
            }
        }
    }

    public void ResetExhibit()
    {
        ResetTrigger();
        if (exhibitPanel != null && exhibitPanel.gameObject.activeSelf)
        {
            exhibitPanel.HidePanel();
        }
    }

    /// <summary>
    /// Resets the trigger state and renews/respawns the crystal on its pedestal.
    /// </summary>
    public void ResetTrigger()
    {
        hasBeenTriggered = false;
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }

        // Renew / respawn crystal visuals and colliders, and resume particle VFX loop
        SetCrystalVisible(true);
        PlayAllParticles();
    }

    private void SetCrystalVisible(bool visible)
    {
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.enabled = visible;
        }

        var colliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in colliders)
        {
            c.enabled = visible;
        }
    }

    private void StopAllParticles()
    {
        if (interactionParticles != null && interactionParticles.gameObject.scene.isLoaded)
        {
            interactionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            interactionParticles.Clear(true);
        }

        var childParticles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in childParticles)
        {
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
            }
        }

        var allSceneParticles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include);
        foreach (var ps in allSceneParticles)
        {
            if (ps != null && ps.gameObject.scene.isLoaded && Vector3.Distance(transform.position, ps.transform.position) < 3.0f)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
                ps.gameObject.SetActive(false);
            }
        }
    }

    private void PlayAllParticles()
    {
        if (interactionParticles != null && interactionParticles.gameObject.scene.isLoaded)
        {
            interactionParticles.gameObject.SetActive(true);
            interactionParticles.Play(true);
        }

        var childParticles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in childParticles)
        {
            if (ps != null)
            {
                ps.gameObject.SetActive(true);
                ps.Play(true);
            }
        }

        var allSceneParticles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include);
        foreach (var ps in allSceneParticles)
        {
            if (ps != null && ps.gameObject.scene.isLoaded && Vector3.Distance(transform.position, ps.transform.position) < 3.0f)
            {
                ps.gameObject.SetActive(true);
                ps.Play(true);
            }
        }
    }
}
