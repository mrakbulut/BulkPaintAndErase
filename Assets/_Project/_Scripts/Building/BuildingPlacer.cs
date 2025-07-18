using UnityEngine;

/// <summary>
/// Main BuildingPlacer class that orchestrates building placement functionality.
/// This is a facade that coordinates multiple specialized components following Single Responsibility Principle.
/// </summary>
public class BuildingPlacer : MonoBehaviour
{
    private BuildingPlacerCore _placerCore;
    private BuildingVisualDebugManager _visualDebugManager;

    private void Awake()
    {
        // Debug.Log("CREATING BUILDING PLACER : " + gameObject, gameObject);

        _placerCore = GetComponent<BuildingPlacerCore>();
        _visualDebugManager = GetComponent<BuildingVisualDebugManager>();
    }

    /// <summary>
    /// Starts the building placement process for the given building data.
    /// </summary>
    /// <param name="buildingData">The building data to place</param>
    public void StartPlacement(BuildingData buildingData)
    {
        // Debug.Log("STARTING PLACEMENT");
        _placerCore.StartPlacement(buildingData);
        _visualDebugManager.SetCurrentBuildingData(buildingData);
    }

    /// <summary>
    /// Starts the dragging mode for building placement.
    /// </summary>
    public void StartDragging()
    {
        // This is now handled internally by the touch handler
    }

    /// <summary>
    /// Ends the building placement process.
    /// </summary>
    public void EndPlacement()
    {
        _placerCore.EndPlacement();
    }

    /// <summary>
    /// Gets whether the placer is currently in placement mode.
    /// </summary>
    public bool IsPlacing => _placerCore.IsPlacing;
}
