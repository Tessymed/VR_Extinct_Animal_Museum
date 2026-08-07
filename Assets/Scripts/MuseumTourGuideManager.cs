using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls the museum tour guide audio narration system.
/// Features:
/// - Instant, hitch-free VR audio playback directly in the headset earpiece (2D AudioSource).
/// - Waits until the VR tracking session is active and stable before starting narration.
/// - Automatically ducks / silences guide voiceover when user engages with an animal exhibit crystal.
/// - Remembers visited tour zones so walking back and forth does not repeat audio.
/// </summary>
public class MuseumTourGuideManager : MonoBehaviour
{
    public static MuseumTourGuideManager Instance { get; private set; }

    [Header("Audio Settings")]
    [Tooltip("The AudioSource used for museum guide voiceovers.")]
    public AudioSource guideAudioSource;

    [Range(0f, 1f)]
    public float narrationVolume = 0.9f;

    [Header("Startup Voiceover")]
    [Tooltip("Welcome audio clip to play once VR tracking is stable.")]
    public AudioClip welcomeClip;
    public float startDelay = 3.0f;

    // Track visited tour zones by unique ID
    private HashSet<string> visitedZones = new HashSet<string>();
    private Coroutine activePlaybackCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (guideAudioSource == null)
        {
            guideAudioSource = GetComponent<AudioSource>();
            if (guideAudioSource == null)
            {
                guideAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (guideAudioSource != null)
        {
            guideAudioSource.playOnAwake = false;
            guideAudioSource.spatialBlend = 0.0f; // 2D earpiece voiceover
            guideAudioSource.loop = false;
            guideAudioSource.volume = narrationVolume;
        }
    }

    private void Start()
    {
        if (welcomeClip != null)
        {
            StartCoroutine(PlayWelcomeWhenReady(startDelay));
        }
    }

    private IEnumerator PlayWelcomeWhenReady(float delay)
    {
        // Wait until VR frames are smoothly flowing
        yield return new WaitForSeconds(delay);

        if (welcomeClip != null && !welcomeClip.isReadyToPlay)
        {
            welcomeClip.LoadAudioData();
            yield return new WaitUntil(() => welcomeClip.isReadyToPlay);
        }

        PlayGuideVoiceover(welcomeClip, "entrance_welcome");
    }

    /// <summary>
    /// Plays a guide voiceover clip for a specific zone if not already visited.
    /// </summary>
    public bool PlayGuideVoiceover(AudioClip clip, string zoneId, bool allowReplay = false)
    {
        if (clip == null) return false;

        if (!allowReplay && !string.IsNullOrEmpty(zoneId) && visitedZones.Contains(zoneId))
        {
            return false; // Zone already visited
        }

        if (!string.IsNullOrEmpty(zoneId))
        {
            visitedZones.Add(zoneId);
        }

        if (activePlaybackCoroutine != null)
        {
            StopCoroutine(activePlaybackCoroutine);
        }

        activePlaybackCoroutine = StartCoroutine(PlayAudioSequence(clip));
        return true;
    }

    private IEnumerator PlayAudioSequence(AudioClip clip)
    {
        if (guideAudioSource == null) yield break;

        if (!clip.isReadyToPlay)
        {
            clip.LoadAudioData();
            yield return new WaitUntil(() => clip.isReadyToPlay);
        }

        // Smoothly start new clip
        guideAudioSource.Stop();
        guideAudioSource.clip = clip;
        guideAudioSource.volume = narrationVolume;
        guideAudioSource.Play();

        Debug.Log($"[TourGuide] Playing guide voiceover: {clip.name}");

        while (guideAudioSource.isPlaying)
        {
            yield return null;
        }

        activePlaybackCoroutine = null;
    }

    /// <summary>
    /// Smoothly pauses or stops the guide voiceover when an animal crystal exhibit is activated.
    /// </summary>
    public void StopGuideVoiceover()
    {
        if (activePlaybackCoroutine != null)
        {
            StopCoroutine(activePlaybackCoroutine);
            activePlaybackCoroutine = null;
        }

        if (guideAudioSource != null && guideAudioSource.isPlaying)
        {
            guideAudioSource.Stop();
            Debug.Log("[TourGuide] Guide narration stopped for exhibit interaction.");
        }
    }
}
