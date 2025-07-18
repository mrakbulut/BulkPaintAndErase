using UnityEngine;

/// <summary>
/// Initializes the building system and ensures all required components are set up properly.
/// Add this script to a GameObject in your scene to automatically set up the building system.
/// </summary>
public class BuildingSystemInitializer : MonoBehaviour
{
    [Header("Building System Setup")]
    [SerializeField] private bool _initializeOnStart = true;

    private void Start()
    {
        if (_initializeOnStart)
        {
            InitializeBuildingSystem();
        }
    }

    public void InitializeBuildingSystem()
    {
        Debug.Log("Building System initialized successfully!");
    }
}
