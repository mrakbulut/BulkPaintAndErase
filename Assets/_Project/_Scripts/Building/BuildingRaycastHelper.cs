using UnityEngine;

/// <summary>
/// Centralized raycast helper that performs raycast operations once per frame and caches results.
/// This prevents multiple raycast calls from different components in the same frame.
/// </summary>
public class BuildingRaycastHelper : MonoBehaviour
{
    [Header("Raycast Settings")]
    [SerializeField] private LayerMask _groundLayer = 1;
    [SerializeField] private LayerMask _obstacleForBuildingLayer = -1; // All layers for building detection

    private Camera _mainCamera;

    // Cached raycast results for current frame
    private struct RaycastCache
    {
        public bool hasGroundHit;
        public RaycastHit groundHit;
        public bool hasBuildingHit;
        public RaycastHit buildingHit;
        public Vector2 screenPosition;
        public int frameNumber;
    }

    private RaycastCache _cache;

    public static BuildingRaycastHelper Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        _mainCamera = Camera.main;
    }

    /// <summary>
    /// Gets ground raycast hit for the given screen position.
    /// Caches result for the current frame to avoid multiple raycasts.
    /// </summary>
    /// <param name="screenPosition">Screen position to raycast from</param>
    /// <param name="hit">The raycast hit result</param>
    /// <returns>True if ground was hit</returns>
    public bool GetGroundHit(Vector2 screenPosition, out RaycastHit hit)
    {
        UpdateCacheIfNeeded(screenPosition);
        hit = _cache.groundHit;
        return _cache.hasGroundHit;
    }

    /// <summary>
    /// Gets building raycast hit for the given screen position.
    /// Caches result for the current frame to avoid multiple raycasts.
    /// </summary>
    /// <param name="screenPosition">Screen position to raycast from</param>
    /// <param name="hit">The raycast hit result</param>
    /// <returns>True if building was hit</returns>
    public bool GetBuildingHit(Vector2 screenPosition, out RaycastHit hit)
    {
        UpdateCacheIfNeeded(screenPosition);
        hit = _cache.buildingHit;
        return _cache.hasBuildingHit;
    }

    /// <summary>
    /// Gets world position from screen position using ground raycast.
    /// </summary>
    /// <param name="screenPosition">Screen position</param>
    /// <returns>World position if hit, Vector3.zero if no hit</returns>
    public Vector3 GetWorldPositionFromScreen(Vector2 screenPosition)
    {
        if (GetGroundHit(screenPosition, out var hit))
        {
            return hit.point;
        }
        return Vector3.zero;
    }

    /// <summary>
    /// Checks if ground is hit at the given screen position.
    /// </summary>
    /// <param name="screenPosition">Screen position to check</param>
    /// <returns>True if ground is hit</returns>
    public bool IsGroundHit(Vector2 screenPosition)
    {
        return GetGroundHit(screenPosition, out _);
    }

    /// <summary>
    /// Gets building component at the given screen position.
    /// </summary>
    /// <param name="screenPosition">Screen position</param>
    /// <returns>BuildingController if found, null otherwise</returns>
    public BuildingController GetBuildingAtScreenPosition(Vector2 screenPosition)
    {
        if (GetBuildingHit(screenPosition, out var hit))
        {
            return hit.collider.GetComponent<BuildingController>();
        }
        return null;
    }

    private void UpdateCacheIfNeeded(Vector2 screenPosition)
    {
        // Check if we need to update cache (different frame or different screen position)
        bool needsUpdate = _cache.frameNumber != Time.frameCount ||
            Vector2.Distance(_cache.screenPosition, screenPosition) > 0.1f;

        if (!needsUpdate)
        {
            return;
        }

        // Perform raycasts and cache results
        var ray = _mainCamera.ScreenPointToRay(screenPosition);

        // Ground raycast
        _cache.hasGroundHit = Physics.Raycast(ray, out _cache.groundHit, Mathf.Infinity, _groundLayer);

        // Building raycast (for building detection/removal)
        _cache.hasBuildingHit = Physics.Raycast(ray, out _cache.buildingHit, Mathf.Infinity, _obstacleForBuildingLayer);

        // Update cache metadata
        _cache.screenPosition = screenPosition;
        _cache.frameNumber = Time.frameCount;
    }

    /// <summary>
    /// Forces cache invalidation. Useful when you want to ensure fresh raycast results.
    /// </summary>
    public void InvalidateCache()
    {
        _cache.frameNumber = -1;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
