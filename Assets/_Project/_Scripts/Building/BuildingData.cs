using UnityEngine;


[CreateAssetMenu(fileName = "BuildingData", menuName = "TinyClash/Building/BuildingData")]
public class BuildingData : ScriptableObject
{
    public string BuildingName;
    public GameObject BuildingPrefab;
    public GameObject HologramPrefab;
    public Sprite UnitIcon;
    public int Width = 1;
    public int Height = 1;

    [Header("Unit Spawning")]
    public GameObject UnitPrefab;
    public float SpawnInterval = 3f;
    public float SpawnAnimationDuration = 1f;
}
