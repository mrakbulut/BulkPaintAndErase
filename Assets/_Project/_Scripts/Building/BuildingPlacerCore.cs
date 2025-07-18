using System.Collections;
using UnityEngine;

public class BuildingPlacerCore : MonoBehaviour
{
    [Header("Placement Settings")]
    [SerializeField] private float _dragUpdateInterval = 0.2f;

    // Components
    private BuildingTouchHandler _touchHandler;
    private BuildingHologramManager _hologramManager;
    private BuildingValidator _validator;
    private BuildingGridManager _gridManager;
    private BuildingFeedbackManager _feedbackManager;

    // State
    private BuildingData _currentBuildingData;
    private bool _isPlacing = false;

    // Timing
    private float _dragUpdateTimer;

    private void Awake()
    {
        // Get or add required components
        _touchHandler = GetComponent<BuildingTouchHandler>();
        _hologramManager = GetComponent<BuildingHologramManager>();
        _validator = GetComponent<BuildingValidator>();
        _gridManager = GetComponent<BuildingGridManager>();
        _feedbackManager = GetComponent<BuildingFeedbackManager>();
    }

    private void Start()
    {
        // Subscribe to touch events
        _touchHandler.OnTapToPlace += TryPlaceBuilding;
        _touchHandler.OnLongPressToCancel += CancelPlacement;
        _touchHandler.OnDragStarted += StartDragging;
        _touchHandler.OnRemoveBuildingAt += TryRemoveBuilding;
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (_touchHandler != null)
        {
            _touchHandler.OnTapToPlace -= TryPlaceBuilding;
            _touchHandler.OnLongPressToCancel -= CancelPlacement;
            _touchHandler.OnDragStarted -= StartDragging;
            _touchHandler.OnRemoveBuildingAt -= TryRemoveBuilding;
        }
    }

    private void Update()
    {
        if (_isPlacing)
        {
            // Debug.Log("UPDATING PLACEMENT Is Dragging : " + _touchHandler.IsDragging);
            if (_touchHandler.IsDragging)
            {
                UpdateHologramForDrag();
            }
            else
            {
                _touchHandler.HandleTouchInputForPlacement();
            }

            if (_hologramManager.CurrentHologram != null && _hologramManager.IsHologramVisible)
            {
                UpdateHologramPosition();
                UpdateHologramAppearance();
            }
        }
        else
        {
            _touchHandler.HandleTouchInputForSelection();
        }
    }

    public void StartPlacement(BuildingData buildingData)
    {
        _currentBuildingData = buildingData;
        _isPlacing = true;

        _hologramManager.CreateHologram(buildingData);
        _feedbackManager.PlayStartPlacementFeedback();
    }

    public void EndPlacement()
    {
        if (_hologramManager.CurrentHologram != null && _hologramManager.IsHologramVisible)
        {
            var worldPos = _hologramManager.GetHologramWorldPosition();

            if (_validator.CanPlaceBuildingAt(worldPos, _currentBuildingData.Width, _currentBuildingData.Height))
            {
                TryPlaceBuilding();
            }
            else
            {
                CancelPlacement();
            }
        }
        else
        {
            CancelPlacement();
        }
    }

    private void UpdateHologramForDrag()
    {
        // Timer kontrolü - belirlenen interval'de bir güncelle
        _dragUpdateTimer += Time.deltaTime;

        if (_dragUpdateTimer < _dragUpdateInterval)
        {
            return;
        }

        _dragUpdateTimer = 0f;
        var screenPosition = _touchHandler.GetCurrentScreenPosition();

        if (BuildingRaycastHelper.Instance.IsGroundHit(screenPosition))
        {
            if (!_hologramManager.IsHologramVisible)
            {
                _hologramManager.ShowHologram();
            }
        }
        else
        {
            if (_hologramManager.IsHologramVisible)
            {
                _hologramManager.HideHologram();
            }
        }
    }

    private void UpdateHologramPosition()
    {
        var screenPosition = _touchHandler.GetCurrentScreenPosition();
        _hologramManager.UpdateHologramPosition(screenPosition);
    }

    private void UpdateHologramAppearance()
    {
        var worldPos = _hologramManager.GetHologramWorldPosition();
        bool canPlace = _validator.CanPlaceBuildingAt(worldPos, _currentBuildingData.Width, _currentBuildingData.Height);
        _hologramManager.UpdateHologramAppearance(canPlace);
    }

    private void TryPlaceBuilding()
    {
        if (_hologramManager.CurrentHologram == null || !_hologramManager.IsHologramVisible) return;

        var worldPos = _hologramManager.GetHologramWorldPosition();

        if (_validator.CanPlaceBuildingAt(worldPos, _currentBuildingData.Width, _currentBuildingData.Height))
        {
            PlaceBuilding(worldPos);
            _feedbackManager.PlaySuccessPlacementFeedback();
        }
        else
        {
            _feedbackManager.PlayInvalidPlacementFeedback();
        }

        CancelPlacement();
    }

    private void PlaceBuilding(Vector3 worldPos)
    {
        var assignedTeam = DetermineTeamForBuilding(worldPos);
        var teamRotation = assignedTeam?.GetTeamRotation() ?? Quaternion.identity;
        
        var building = Instantiate(_currentBuildingData.BuildingPrefab, worldPos, teamRotation);

        // _gridManager.UpdateGridGraph(gridPos, _currentBuildingData.Width, _currentBuildingData.Height, false);

        var buildingController = building.GetComponent<BuildingController>();
        var enemyTeam = DetermineEnemyTeamForBuilding(assignedTeam);
        buildingController.Initialize(_currentBuildingData, assignedTeam, enemyTeam);
    }

    private BattleTeam DetermineTeamForBuilding(Vector3 worldPos)
    {
        var gameManager = GameManager.Instance;
        if (gameManager == null) return null;

        float mapCenterZ = (gameManager.GetUpTeam().GetRandomPositionBetweenBounds().z +
            gameManager.GetDownTeam().GetRandomPositionBetweenBounds().z) / 2f;

        return worldPos.z > mapCenterZ ? gameManager.GetUpTeam() : gameManager.GetDownTeam();
    }

    private BattleTeam DetermineEnemyTeamForBuilding(BattleTeam battleTeam)
    {
        var gameManager = GameManager.Instance;
        if (gameManager == null) return null;

        return gameManager.GetUpTeam() == battleTeam ? gameManager.GetDownTeam() : gameManager.GetUpTeam();
    }

    private void TryRemoveBuilding(Vector2 screenPosition)
    {
        var building = BuildingRaycastHelper.Instance.GetBuildingAtScreenPosition(screenPosition);
        if (building != null)
        {
            // _gridManager.UpdateGridGraph(building.GridPosition, building.BuildingData.Width, building.BuildingData.Height, true);
            DestroyImmediate(building.gameObject);
            _feedbackManager.PlayRemoveBuildingFeedback();
        }
    }

    private void StartDragging()
    {
        // Dragging başladı, herhangi bir özel işlem varsa burada yapılabilir
    }

    private void CancelPlacement()
    {
        _isPlacing = false;
        _currentBuildingData = null;
        _hologramManager.DestroyCurrentHologram();
    }

    public bool IsPlacing => _isPlacing;
}
