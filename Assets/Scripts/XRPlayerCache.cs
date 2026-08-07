using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// High-performance static cache for VR player components.
/// Completely eliminates expensive Camera.main and FindObjectsByType queries in Update loops.
/// </summary>
public static class XRPlayerCache
{
    private static XROrigin cachedOrigin;
    private static Camera cachedCamera;
    private static Transform cachedCameraTransform;

    public static XROrigin Origin
    {
        get
        {
            if (cachedOrigin == null)
            {
                cachedOrigin = Object.FindAnyObjectByType<XROrigin>();
            }
            return cachedOrigin;
        }
    }

    public static Camera HeadCamera
    {
        get
        {
            if (cachedCamera == null)
            {
                if (Origin != null && Origin.Camera != null)
                {
                    cachedCamera = Origin.Camera;
                }
                else
                {
                    cachedCamera = Camera.main;
                }
            }
            return cachedCamera;
        }
    }

    public static Transform HeadTransform
    {
        get
        {
            if (cachedCameraTransform == null && HeadCamera != null)
            {
                cachedCameraTransform = HeadCamera.transform;
            }
            return cachedCameraTransform;
        }
    }

    public static void ResetCache()
    {
        cachedOrigin = null;
        cachedCamera = null;
        cachedCameraTransform = null;
    }
}
