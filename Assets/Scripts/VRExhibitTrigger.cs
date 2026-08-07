using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Triggers an interactive exhibit presentation when the pedestal crystal is grabbed in VR.
/// Features:
/// - 100% 3D spatialized continuous looping audio anchored to the exhibit stand.
/// - Opens the TMP story card in world space with page navigation.
/// - Respawns crystal and particle VFX when the story card is closed.
/// </summary>
[RequireComponent(typeof(VRGrabbable))]
public class VRExhibitTrigger : MonoBehaviour
{
    [Header("Exhibit Content")]
    [Tooltip("The audio clip to loop continuously while the card is open.")]
    public AudioSource audioSource;
    public AudioClip narrationClip;

    [Tooltip("The UI Panel component to display.")]
    public VRExhibitPanelUI exhibitPanel;

    [Tooltip("Optional ExhibitStory ScriptableObject.")]
    public ExhibitStory exhibitStory;

    [Tooltip("Legacy inline exhibit data.")]
    public VRExhibitPanelUI.AnimalExhibitData animalData = new VRExhibitPanelUI.AnimalExhibitData();

    [Header("Visual Effects")]
    [Tooltip("Particle system on the crystal that stops during story presentation.")]
    public ParticleSystem interactionParticles;

    [Header("Interaction Settings")]
    [Tooltip("Whether the controller immediately drops the crystal upon trigger so the player can aim at the card.")]
    public bool releaseGrabberOnTrigger = true;

    [Header("Events")]
    public UnityEvent onTrigger;

    [Header("Auto-Reset (Distance)")]
    public bool autoResetOnLeave = false;
    public float resetDistance = 10f;

    private VRGrabbable grabbable;
    private bool hasBeenTriggered = false;

    private void Awake()
    {
        grabbable = GetComponent<VRGrabbable>();

        GameObject audioHost = gameObject;
        if (exhibitPanel != null)
        {
            audioHost = exhibitPanel.gameObject;
        }
        else if (transform.parent != null)
        {
            audioHost = transform.parent.gameObject;
        }

        if (audioSource == null)
        {
            audioSource = audioHost.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = audioHost.AddComponent<AudioSource>();
            }
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f; // 100% 3D spatial audio in world space
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 2.0f;  // Clear, crisp sound when standing near exhibit
            audioSource.maxDistance = 25.0f; // Smoothly fades as player explores museum
            audioSource.dopplerLevel = 0.0f;
            audioSource.loop = true;          // Loop continuously while card is displayed
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

        // Stop any active museum docent voiceover so it doesn't clash with animal audio
        if (MuseumTourGuideManager.Instance != null)
        {
            MuseumTourGuideManager.Instance.StopGuideVoiceover();
        }

        Debug.Log($"[VRExhibitTrigger] Triggered exhibit for: {animalData.animalName}");

        // 1. Hide crystal object mesh & colliders immediately
        try
        {
            SetCrystalVisible(false);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VRExhibitTrigger] Exception hiding crystal: {ex.Message}");
        }

        // 2. Stop crystal particle systems
        try
        {
            StopAllParticles();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VRExhibitTrigger] Exception stopping particles: {ex.Message}");
        }

        // 3. Disable any legacy 3D canvas texts
        try
        {
            DisableLegacyExhibitCanvases();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VRExhibitTrigger] Exception disabling legacy canvases: {ex.Message}");
        }

        // 4. Play looping 3D spatial audio emanating from the stationary exhibit
        try
        {
            if (audioSource != null && narrationClip != null)
            {
                audioSource.Stop();
                audioSource.clip = narrationClip;
                audioSource.loop = true;
                audioSource.spatialBlend = 1.0f; // 100% 3D spatial audio
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = 2.0f;
                audioSource.maxDistance = 25.0f;
                audioSource.dopplerLevel = 0.0f;
                audioSource.volume = 1.0f;
                audioSource.time = 0f;
                audioSource.Play();
                Debug.Log($"[VRExhibitTrigger] Playing looping 3D audio for: {animalData.animalName} ({narrationClip.name})");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[VRExhibitTrigger] Exception playing audio: {ex.Message}");
        }

        // 5. Open and show the exhibit story card
        try
        {
            if (exhibitPanel != null)
            {
                if (exhibitStory != null)
                {
                    exhibitPanel.ShowStory(exhibitStory);
                    Debug.Log($"[VRExhibitTrigger] ShowStory called with ScriptableObject: '{exhibitStory.name}'");
                }
                else
                {
                    exhibitPanel.ShowExhibit(animalData);
                    Debug.Log($"[VRExhibitTrigger] ShowExhibit called for '{animalData.animalName}'");
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
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

        var oldPanels = FindObjectsByType<VRExhibitPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
        // Maintain continuous looping audio as long as the exhibit story card is displayed
        if (hasBeenTriggered && exhibitPanel != null && exhibitPanel.gameObject.activeSelf)
        {
            if (audioSource != null && narrationClip != null && !audioSource.isPlaying)
            {
                audioSource.clip = narrationClip;
                audioSource.loop = true;
                audioSource.time = 0f;
                audioSource.Play();
            }
        }

        if (autoResetOnLeave && hasBeenTriggered)
        {
            Transform playerTransform = XRPlayerCache.HeadTransform;
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

        var allSceneParticles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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

        var allSceneParticles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
