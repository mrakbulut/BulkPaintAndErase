using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;


public class UnitPanel : MonoBehaviour
{
    [TitleGroup("UI References")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private UnitSlot[] _unitSlots;

    [TitleGroup("Building Data")]
    [SerializeField] private List<BuildingData> _availableBuildings = new List<BuildingData>();

    private void Start()
    {
        for (int i = 0; i < _unitSlots.Length; i++)
        {
            _unitSlots[i].Initialize(_canvasGroup);

            var notActivelyUsingBuildingData = GetNotActivelyUsingBuildingData();
            FindAndSetEmptyUnitSlotBuildingData(notActivelyUsingBuildingData);
        }
    }

    private BuildingData GetNotActivelyUsingBuildingData()
    {
        var notUsingBuildings = new List<int>();

        for (int i = 0; i < _availableBuildings.Count; i++)
        {
            var buildingData = _availableBuildings[i];
            bool usingBySlot = false;

            for (int k = 0; k < _unitSlots.Length; k++)
            {
                if (_unitSlots[k].IsEmpty) continue;
                if (_unitSlots[k].BuildingData == buildingData)
                {
                    usingBySlot = true;
                    break;
                }
            }

            if (!usingBySlot)
            {
                notUsingBuildings.Add(i);
            }
        }

        int randomNotUsingBuildingDataIndex = Random.Range(0, notUsingBuildings.Count);
        int randomBuildingDataIndex = notUsingBuildings[randomNotUsingBuildingDataIndex];
        return _availableBuildings[randomBuildingDataIndex];
    }

    public void FindAndSetEmptyUnitSlotBuildingData(BuildingData buildingData)
    {
        var slot = GetEmptySlot();

        if (slot != null)
        {
            // Debug.Log("SETTING BUILDING DATA : " + buildingData, slot);
            slot.SetBuildingData(buildingData);
        }
    }

    private UnitSlot GetEmptySlot()
    {
        var emptySlotIndexes = new List<int>();

        for (int i = 0; i < _unitSlots.Length; i++)
        {
            if (_unitSlots[i].IsEmpty)
            {
                emptySlotIndexes.Add(i);
            }
        }

        int randomEmptySlotIndex = Random.Range(0, emptySlotIndexes.Count);
        int emptySlotIndex = emptySlotIndexes[randomEmptySlotIndex];
        return _unitSlots[emptySlotIndex];
    }
}
