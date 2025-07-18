using System.Collections;
using UnityEngine;
using PrimeTween;


public class BuildingController : MonoBehaviour
{
    [SerializeField] private BuildingPlacementManager _buildingPlacementManager;
    [SerializeField] private Transform _buildingDoor;
    [SerializeField] private Transform _initialPoint;

    private BuildingData _buildingData;
    private BattleTeam _battleTeam;
    private float _spawnTimer;
    private bool _isSpawning;

    public BuildingData BuildingData => _buildingData;
    public BattleTeam BattleTeam => _battleTeam;
    private BattleTeam _enemyTeam;

    public void Initialize(BuildingData data, BattleTeam battleTeam, BattleTeam enemyTeam)
    {
        _buildingData = data;
        _battleTeam = battleTeam;
        _enemyTeam = enemyTeam;

        _buildingPlacementManager.OnBuildingPlacedAt(this, data);

        if (_initialPoint == null)
        {
            _initialPoint = transform;
        }

        _spawnTimer = 0f;
    }

    private void Update()
    {
        if (_buildingData == null || _buildingData.UnitPrefab == null || _battleTeam == null || _isSpawning) return;

        _spawnTimer += Time.deltaTime;

        if (_spawnTimer >= _buildingData.SpawnInterval)
        {
            _spawnTimer = 0f;
            StartCoroutine(SpawnUnit());
        }
    }

    private IEnumerator SpawnUnit()
    {
        _isSpawning = true;

        var doorPosition = _buildingDoor.position;
        var initialPosition = _initialPoint.position;
        var randomDestination = GetRandomDestinationInEnemyTeamBounds();

        var unitObj = Instantiate(_buildingData.UnitPrefab, doorPosition, Quaternion.identity);
        var unit = unitObj.GetComponent<SimpleUnit>();

        if (unit != null)
        {
            SetupUnitWithTeamConfig(unit);
            yield return AnimateUnitSpawn(unit, doorPosition, initialPosition);
            unit.SetDestination(randomDestination);
        }

        _isSpawning = false;
    }

    private void SetupUnitWithTeamConfig(SimpleUnit unit)
    {
        _battleTeam.SetupUnit(unit);
    }

    private Tween AnimateUnitSpawn(SimpleUnit unit, Vector3 startPos, Vector3 endPos)
    {
        unit.transform.position = startPos;

        // Create a sequence that animates both position and scale simultaneously
        return Tween.Position(unit.transform, endPos, _buildingData.SpawnAnimationDuration, Ease.OutQuad);
    }

    private Vector3 GetRandomDestinationInEnemyTeamBounds()
    {
        return _enemyTeam.GetRandomPositionBetweenBounds();
    }
}
