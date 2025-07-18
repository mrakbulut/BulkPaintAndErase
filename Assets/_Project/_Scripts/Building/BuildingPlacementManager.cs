using UnityEngine;
using Pathfinding;

public class BuildingPlacementManager : MonoBehaviour
{
    [Header("Pathfinding Integration")]
    [SerializeField] private GraphUpdateScene _graphUpdateScene;

    public System.Action<BuildingController, BuildingData> OnBuildingPlaced;

    public void OnBuildingPlacedAt(BuildingController buildingController, BuildingData buildingData)
    {
        _graphUpdateScene.Apply();
        //Debug.Log("APPLYING GRAPH UPDATE SCENE");
        OnBuildingPlaced?.Invoke(buildingController, buildingData);

        //Debug.Log($"Building placed: {buildingData}.");
    }
}
