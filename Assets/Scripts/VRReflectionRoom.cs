using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VRReflectionRoom : MonoBehaviour
{
    [System.Serializable]
    public struct PledgeOrbData
    {
        public VRGrabbable orbGrabbable;
        [Tooltip("The central constellation point this specific orb will float to when selected.")]
        public Transform targetPoint;
        [Tooltip("Custom visual asset (e.g. feather, paw print, ice crystal) that becomes active when this pledge is taken.")]
        public GameObject associatedMotif;
    }

    [Header("Pledge Configuration")]
    public List<PledgeOrbData> pledgeOrbs = new List<PledgeOrbData>();

    [Header("Visual Effects & Feedback")]
    [Tooltip("Global particle system representing the active constellation/globe in the center of the room.")]
    public ParticleSystem constellationParticles;

    [Tooltip("Global audio source to play a successful pledge sound.")]
    public AudioSource audioSource;
    public AudioClip pledgeSound;

    [Tooltip("Speed at which the selected orb floats to its constellation point.")]
    public float floatSpeed = 2f;

    private HashSet<VRGrabbable> activatedOrbs = new HashSet<VRGrabbable>();

    private void OnEnable()
    {
        foreach (var data in pledgeOrbs)
        {
            if (data.orbGrabbable != null)
            {
                data.orbGrabbable.OnGrabbed.AddListener((grabber) => OnPledgeGrabbed(data.orbGrabbable, grabber));
            }
        }
    }

    private void OnDisable()
    {
        foreach (var data in pledgeOrbs)
        {
            if (data.orbGrabbable != null)
            {
                data.orbGrabbable.OnGrabbed.RemoveAllListeners();
            }
        }
    }

    private void OnPledgeGrabbed(VRGrabbable orb, VRControllerGrab grabber)
    {
        if (activatedOrbs.Contains(orb)) return;
        activatedOrbs.Add(orb);

        // Find matching data for this orb
        PledgeOrbData matchingData = default;
        foreach (var data in pledgeOrbs)
        {
            if (data.orbGrabbable == orb)
            {
                matchingData = data;
                break;
            }
        }

        // Force release from the hand immediately so it floats away
        if (grabber != null)
        {
            grabber.ForceRelease();
        }

        // Disable VR grabbing on this orb so it cannot be grabbed again
        orb.enabled = false;

        // Disable physics so it doesn't fall or collide
        Rigidbody rb = orb.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Disable colliders so it passes smoothly through things
        Collider col = orb.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        // Trigger visual motif if assigned
        if (matchingData.associatedMotif != null)
        {
            matchingData.associatedMotif.SetActive(true);
        }

        // Play feedback audio
        if (audioSource != null && pledgeSound != null)
        {
            audioSource.PlayOneShot(pledgeSound);
        }

        // Start floating animation towards constellation
        if (matchingData.targetPoint != null)
        {
            StartCoroutine(FloatToConstellation(orb.transform, matchingData.targetPoint.position));
        }

        // Trigger constellation particles
        if (constellationParticles != null && !constellationParticles.isPlaying)
        {
            constellationParticles.Play();
        }
    }

    private IEnumerator FloatToConstellation(Transform orbTransform, Vector3 targetPos)
    {
        Vector3 startPos = orbTransform.position;
        float progress = 0f;

        while (progress < 1f)
        {
            progress += Time.deltaTime * floatSpeed;
            orbTransform.position = Vector3.Lerp(startPos, targetPos, progress);
            yield return null;
        }

        orbTransform.position = targetPos;
        
        // Add a gentle floating/bobbing effect at the target point
        float randomOffset = Random.Range(0f, 100f);
        while (true)
        {
            float bobbingOffset = Mathf.Sin(Time.time * 2f + randomOffset) * 0.05f;
            orbTransform.position = targetPos + Vector3.up * bobbingOffset;
            yield return null;
        }
    }
}
