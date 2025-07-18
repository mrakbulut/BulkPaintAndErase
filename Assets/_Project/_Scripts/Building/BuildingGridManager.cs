using Pathfinding;
using UnityEngine;

public class BuildingGridManager : MonoBehaviour
{
    private AstarPath _astarPath;
    private GridGraph _gridGraph;
    
    private void Start()
    {
        _astarPath = AstarPath.active;
        
        if (_astarPath != null)
        {
            _gridGraph = _astarPath.data.gridGraph;
        }
    }
    
    public void UpdateGridGraph(Vector2Int gridPos, int width, int height, bool walkable)
    {
        if (_gridGraph == null) return;
        
        // Building'in bound'larını merkez pozisyondan hesapla
        int halfWidth = width / 2;
        int halfHeight = height / 2;
        
        int startX = gridPos.x - halfWidth;
        int endX = gridPos.x + halfWidth;
        int startY = gridPos.y - halfHeight;
        int endY = gridPos.y + halfHeight;
        
        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                var node = _gridGraph.GetNode(x, y);
                if (node != null)
                {
                    node.Walkable = walkable;
                }
            }
        }
        
        // Pathfinding graph'ını güncelle
        AstarPath.active.Scan();
    }
    
    public Vector3 GridToWorldPosition(Vector2Int gridPos, float gridSize, Vector2 gridBounds)
    {
        float x = gridPos.x - gridBounds.x;
        float z = gridPos.y - gridBounds.y;
        return new Vector3(x, 0, z);
    }
}