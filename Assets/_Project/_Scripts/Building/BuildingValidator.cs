using Pathfinding;
using UnityEngine;

public class BuildingValidator : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private float _gridSize = 1f;

    [Header("Building Placement Settings")]
    [SerializeField] private LayerMask _obstacleForBuildingLayer = 1 << 6;

    private AstarPath _astarPath;
    private GridGraph _gridGraph;
    private Collider[] _buildingObstacles;

    private const int _maxBuildingObstacleCount = 5;

    private void Start()
    {
        _buildingObstacles = new Collider[_maxBuildingObstacleCount];
        _astarPath = AstarPath.active;

        if (_astarPath != null)
        {
            _gridGraph = _astarPath.data.gridGraph;
        }
    }

    public bool CanPlaceBuildingAt(Vector3 worldPos, int width, int height)
    {
        var boxSize = new Vector3(width * _gridSize, 2f, height * _gridSize);
        var worldBounds = new Bounds(Vector3.zero, new Vector3(_gridGraph.width * _gridSize, 10f, _gridGraph.depth * _gridSize));
        var buildingBounds = new Bounds(worldPos, boxSize);

        if (!worldBounds.Contains(buildingBounds.min) || !worldBounds.Contains(buildingBounds.max))
        {
            //Debug.Log("CANT PLACE BUILDING AT : " + worldPos + " - World bounds check failed");
            return false;
        }


        if (HasUnitsInArea(worldPos, width, height))
        {
            // Debug.Log("CANT PLACE BUILDING AT : " + worldPos + " - Units found in building area");
            return false;
        }

        return true;
    }

    private bool HasUnitsInArea(Vector3 centerWorldPos, int width, int height)
    {
        var boxSize = new Vector3(width * _gridSize, 4f, height * _gridSize);
        int overlappedObstacleCount = Physics.OverlapBoxNonAlloc(centerWorldPos, boxSize * .5f, _buildingObstacles, Quaternion.identity, _obstacleForBuildingLayer);

        // Debug.Log("OVERLAPPED OBSTACLE COUNT : " + overlappedObstacleCount);

        return overlappedObstacleCount > 0;
    }
}
