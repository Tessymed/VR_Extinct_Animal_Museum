using UnityEngine;

/// <summary>
/// Spatial trigger volume placed in doorways or gallery corridors.
/// When the VR player walks into this zone, it tells MuseumTourGuideManager
/// to play the appropriate guide narration clip once.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MuseumTourZone : MonoBehaviour
{
    [Header("Tour Waypoint Settings")]
    [Tooltip("Unique identifier for this tour zone (e.g. 'zone_dodo', 'zone_mammoth').")]
    public string zoneId = "zone_main";

    [Tooltip("The guide voiceover audio clip to play when entering this area.")]
    public AudioClip guideClip;

    [Tooltip("Whether this audio should replay if the user walks back into the area.")]
    public bool allowReplay = false;

    private Collider zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true; // Must be a trigger volume
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the collider belongs to the VR player (Main Camera or XR Origin)
        if (IsPlayerCollider(other))
        {
            TriggerGuideAudio();
        }
    }

    public void TriggerGuideAudio()
    {
        if (guideClip == null) return;

        if (MuseumTourGuideManager.Instance != null)
        {
            MuseumTourGuideManager.Instance.PlayGuideVoiceover(guideClip, zoneId, allowReplay);
        }
    }

    private bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;

        // Check for Camera, XR Origin, or Character controller tags
        if (col.CompareTag("MainCamera") || col.CompareTag("Player"))
        {
            return true;
        }

        if (col.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null || col.GetComponentInParent<Camera>() != null)
        {
            return true;
        }

        return false;
    }
}
