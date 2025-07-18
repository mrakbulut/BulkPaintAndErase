using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public class UnitSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField]
    private Image unitIcon;

    [SerializeField] [ReadOnly]
    private BuildingData _buildingData;

    private Canvas canvas;
    private RectTransform rectTransform;
    private CanvasGroup _canvasGroup;
    private BuildingPlacer buildingPlacer;
    private Vector2 originalPosition;

    private bool _isEmpty;
    public bool IsEmpty => _buildingData == null;
    public BuildingData BuildingData => _buildingData;

    private void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        rectTransform = GetComponent<RectTransform>();
        buildingPlacer = FindObjectOfType<BuildingPlacer>();
        // Debug.Log("BUILDING PLACER FOUND : " + buildingPlacer, buildingPlacer);
        originalPosition = rectTransform.anchoredPosition;

        if (_buildingData != null)
        {
            unitIcon.sprite = _buildingData.UnitIcon;
        }
    }

    public void Initialize(CanvasGroup canvasGroup)
    {
        _canvasGroup = canvasGroup;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _canvasGroup.alpha = 0.6f;
        _canvasGroup.blocksRaycasts = false;

        if (buildingPlacer != null)
        {
            buildingPlacer.StartPlacement(_buildingData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;

        if (buildingPlacer != null)
        {
            buildingPlacer.StartDragging();
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        if (buildingPlacer != null)
        {
            buildingPlacer.EndPlacement();
        }

        rectTransform.anchoredPosition = originalPosition;
    }

    public void SetBuildingData(BuildingData data)
    {
        _buildingData = data;
        if (unitIcon != null)
        {
            unitIcon.sprite = data.UnitIcon;
        }
    }
}
