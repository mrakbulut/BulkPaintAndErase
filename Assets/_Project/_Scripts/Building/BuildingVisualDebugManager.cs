using UnityEngine;

public class BuildingVisualDebugManager : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private float _dragUpdateInterval = 0.2f;
    [SerializeField] private float _gridSize = 1f;

    // Components
    private BuildingPlacerCore _placerCore;
    private BuildingHologramManager _hologramManager;
    private BuildingValidator _validator;

    // Gizmos optimization
    private float _lastGizmosUpdateTime;
    private bool _lastGizmosCanPlaceResult;
    private BuildingData _currentBuildingData;

    private void Start()
    {
        _placerCore = GetComponent<BuildingPlacerCore>();
        _hologramManager = GetComponent<BuildingHologramManager>();
        _validator = GetComponent<BuildingValidator>();
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        if (!_placerCore.IsPlacing || _hologramManager.CurrentHologram == null ||
            !_hologramManager.IsHologramVisible || _currentBuildingData == null)
        {
            return;
        }

        // Hologram'ın world position'ını al
        var worldPos = _hologramManager.GetHologramWorldPosition();

        // Yerleştirilebilirlik durumuna göre renk seç - timer kontrolü ile optimize edilmiş
        bool canPlace;
        if (ShouldUpdateGizmosCheck())
        {
            canPlace = _validator.CanPlaceBuildingAt(worldPos, _currentBuildingData.Width, _currentBuildingData.Height);
            _lastGizmosCanPlaceResult = canPlace;
        }
        else
        {
            canPlace = _lastGizmosCanPlaceResult;
        }

        // Building'in world bounds'ını hesapla ve çiz
        var boxSize = new Vector3(_currentBuildingData.Width * _gridSize, 0.2f, _currentBuildingData.Height * _gridSize);

        Gizmos.color = canPlace ? Color.green : Color.red;
        Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.3f); // Transparency ekle

        // Building alanını küp olarak çiz
        var gizmosPos = worldPos;
        gizmosPos.y = 0.1f;
        Gizmos.DrawCube(gizmosPos, boxSize);

        // Hologram'ın gerçek world position'ını sarı nokta ile göster
        Gizmos.color = Color.yellow;
        var hologramWorldPos = worldPos;
        hologramWorldPos.y = 0.3f;
        Gizmos.DrawSphere(hologramWorldPos, 0.2f);

        // Building bounds'ını wireframe olarak göster
        Gizmos.color = Color.cyan;
        var wireframePos = worldPos;
        wireframePos.y = 0.2f;
        Gizmos.DrawWireCube(wireframePos, boxSize);
    }

    public void SetCurrentBuildingData(BuildingData buildingData)
    {
        _currentBuildingData = buildingData;
    }

    private bool ShouldUpdateGizmosCheck()
    {
        if (Time.time - _lastGizmosUpdateTime >= _dragUpdateInterval)
        {
            _lastGizmosUpdateTime = Time.time;
            return true;
        }
        return false;
    }
}
