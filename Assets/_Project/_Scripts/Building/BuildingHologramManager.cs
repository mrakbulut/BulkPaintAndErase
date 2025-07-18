using UnityEngine;

public class BuildingHologramManager : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Material _validPlacementMaterial;
    [SerializeField] private Material _invalidPlacementMaterial;

    private GameObject _currentHologram;
    private bool _isHologramVisible = false;

    public GameObject CurrentHologram => _currentHologram;
    public bool IsHologramVisible => _isHologramVisible;

    public void CreateHologram(BuildingData buildingData)
    {
        DestroyCurrentHologram();

        // Hologramı oluştur ama başlangıçta gizle
        if (buildingData.HologramPrefab != null)
        {
            _currentHologram = Instantiate(buildingData.HologramPrefab);
        }
        else
        {
            // Hologram yoksa building prefab'ını kullan
            _currentHologram = Instantiate(buildingData.BuildingPrefab);
            MakeHologram(_currentHologram);
        }

        // Hologramı başlangıçta gizle
        _currentHologram.SetActive(false);
        _isHologramVisible = false;
    }

    public void ShowHologram()
    {
        if (_currentHologram != null)
        {
            _currentHologram.SetActive(true);
            _isHologramVisible = true;
        }
    }

    public void HideHologram()
    {
        if (_currentHologram != null)
        {
            _currentHologram.SetActive(false);
            _isHologramVisible = false;
        }
    }

    public void UpdateHologramPosition(Vector2 screenPosition)
    {
        if (_currentHologram == null) return;

        var worldPos = BuildingRaycastHelper.Instance.GetWorldPositionFromScreen(screenPosition);
        if (worldPos != Vector3.zero)
        {
            _currentHologram.transform.position = worldPos;
        }
    }

    public void UpdateHologramAppearance(bool canPlace)
    {
        if (_currentHologram == null) return;

        // Hologram materyalını güncelle
        var renderers = _currentHologram.GetComponentsInChildren<Renderer>();
        var materialToUse = canPlace ? _validPlacementMaterial : _invalidPlacementMaterial;

        foreach (var renderer in renderers)
        {
            var materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = materialToUse;
            }
            renderer.materials = materials;
        }
    }

    public Vector3 GetHologramWorldPosition()
    {
        return _currentHologram != null ? _currentHologram.transform.position : Vector3.zero;
    }

    public void DestroyCurrentHologram()
    {
        if (_currentHologram != null)
        {
            DestroyImmediate(_currentHologram);
            _currentHologram = null;
        }
        _isHologramVisible = false;
    }

    private void MakeHologram(GameObject obj)
    {
        // Hologram için transparent material ayarla
        var renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            var materials = renderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = _validPlacementMaterial;
            }
            renderer.materials = materials;
        }

        // Collider'ları devre dışı bırak
        var colliders = obj.GetComponentsInChildren<Collider>();
        foreach (var collider in colliders)
        {
            collider.enabled = false;
        }
    }
}
