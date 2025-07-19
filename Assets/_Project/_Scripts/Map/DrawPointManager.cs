using System.Collections.Generic;
using UnityEngine;
using XDPaint;
using XDPaint.Core;
public class DrawPointManager : MonoBehaviour
{
    public static DrawPointManager Instance;

    [SerializeField] private Renderer _objectForPaintRenderer;

    [SerializeField] private int _textureWidth = 256;
    [SerializeField] private int _textureHeight = 256;

    [SerializeField] private int _frameInterval = 3;
    [SerializeField] private int _maxBatchSize = 10;
    [SerializeField] private bool _enableDebugLogs = true;

    // Painting UV lines
    private readonly List<Vector2> _paintingTexturePositions = new List<Vector2>();
    private readonly List<float> _paintingPressures = new List<float>();
    private PaintManager _paintManager;
    private PainterConfig _paintingUVPainterConfig;

    // Erasing UV lines
    private readonly List<Vector2> _erasingTexturePositions = new List<Vector2>();
    private readonly List<float> _erasingPressures = new List<float>();
    private PainterConfig _erasingUVPainterConfig;

    // Pre-configured materials for dual mesh rendering
    private Material _paintMaterial;
    private Material _eraseMaterial;
    private int _frameCounter = 0;
    private bool _isProcessing = false;

    private Bounds _bounds;
    private bool _painting;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        var meshFilter = _objectForPaintRenderer.GetComponent<MeshFilter>();
        _bounds = meshFilter.sharedMesh.bounds;
    }

    private void Update()
    {
        _frameCounter++;

        if (_frameCounter >= _frameInterval && !_isProcessing)
        {
            ProcessUVLines();

            _frameCounter = 0;
        }
    }

    public void SetPaintManager(PaintManager paintManager, PainterConfig paintingConfig, PainterConfig erasingConfig)
    {
        _paintManager = paintManager;
        _paintingUVPainterConfig = paintingConfig;
        _erasingUVPainterConfig = erasingConfig;

        var renderTexture = _paintManager.GetResultRenderTexture();
        _textureWidth = renderTexture.width;
        _textureHeight = renderTexture.height;

        // Create separate material instances for paint and erase
        CreateMaterialInstances();
    }

    private void CreateMaterialInstances()
    {
        // Store original tool to restore later
        var originalTool = _paintManager.ToolsManager.CurrentTool;

        // Setup paint material with full configuration
        _paintManager.ToolsManager.SetTool(PaintTool.Brush);
        _paintManager.Brush.Size = _paintingUVPainterConfig.BrushSize;
        _paintManager.Brush.SetColor(_paintingUVPainterConfig.Color, true, false);
        // Create a deep copy of the brush material with all its properties
        _paintMaterial = new Material(_paintManager.Brush.Material);
        _paintMaterial.name = "DrawPointManager_Paint_Material";

        // Setup erase material with full configuration
        _paintManager.ToolsManager.SetTool(PaintTool.Erase);
        _paintManager.Brush.Size = _erasingUVPainterConfig.BrushSize;
        // Create a deep copy of the erase material with all its properties
        _eraseMaterial = new Material(_paintManager.Brush.Material);
        _eraseMaterial.name = "DrawPointManager_Erase_Material";

        // Restore original tool
        if (originalTool != null)
        {
            _paintManager.ToolsManager.SetTool(originalTool.Type);
        }

        if (_enableDebugLogs)
        {
            Debug.Log($"DrawPointManager: Material instances created");
            Debug.Log($"Paint Material: {_paintMaterial.name} - Shader: {_paintMaterial.shader.name}");
            Debug.Log($"Erase Material: {_eraseMaterial.name} - Shader: {_eraseMaterial.shader.name}");

            // Debug blend modes
            if (_paintMaterial.HasProperty("_SrcBlend"))
            {
                Debug.Log($"Paint SrcBlend: {_paintMaterial.GetInt("_SrcBlend")}, DstBlend: {_paintMaterial.GetInt("_DstBlend")}");
            }
            if (_eraseMaterial.HasProperty("_SrcBlend"))
            {
                Debug.Log($"Erase SrcBlend: {_eraseMaterial.GetInt("_SrcBlend")}, DstBlend: {_eraseMaterial.GetInt("_DstBlend")}");
            }
        }
    }
    public void RequestDrawPoint(Vector3 worldPosition, float pressure, bool isErasing)
    {
        var uvPosition = WorldToUV(worldPosition);

        if (uvPosition.HasValue)
        {
            var texturePosition = UvToTexturePosition(uvPosition.Value);
            if (isErasing)
            {
                // Add UV position and pressure to the erasing line
                _erasingTexturePositions.Add(texturePosition);
                _erasingPressures.Add(pressure);
            }
            else
            {
                // Add UV position and pressure to the painting line
                _paintingTexturePositions.Add(texturePosition);
                _paintingPressures.Add(pressure);
            }
        }
    }

    private void ProcessUVLines()
    {
        // Process both painting and erasing in single CommandBuffer to eliminate flickering
        ProcessDualMeshLines();
    }

    private void ProcessDualMeshLines()
    {
        bool hasPaintData = _paintingTexturePositions.Count > 0;
        bool hasEraseData = _erasingTexturePositions.Count > 0;

        if (!hasPaintData && !hasEraseData) return;

        // Set a tool temporarily for the render operation (required by XDPaint)
        var originalTool = _paintManager.ToolsManager.CurrentTool;
        _paintManager.ToolsManager.SetTool(PaintTool.Brush);

        // Use optimized dual mesh rendering with pre-configured materials
        _paintManager.PaintObject.DrawDualMeshFromUV(
            hasPaintData ? _paintingTexturePositions.ToArray() : null,
            hasPaintData ? _paintingPressures.ToArray() : null,
            hasPaintData ? _paintMaterial : null,
            Color.white, // Use white for proper texture blending - color comes from material
            hasEraseData ? _erasingTexturePositions.ToArray() : null,
            hasEraseData ? _erasingPressures.ToArray() : null,
            hasEraseData ? _eraseMaterial : null
            );

        // Restore original tool if it was different
        if (originalTool != null && originalTool.Type != PaintTool.Brush)
        {
            _paintManager.ToolsManager.SetTool(originalTool.Type);
        }

        // Clear processed data
        if (hasPaintData) ClearPaintingUVLineData();
        if (hasEraseData) ClearErasingUVLineData();

        if (_enableDebugLogs)
        {
            Debug.Log($"DrawPointManager: Processed dual mesh - Paint:{(hasPaintData ? _paintingTexturePositions.Count : 0)} Erase:{(hasEraseData ? _erasingTexturePositions.Count : 0)}");
        }
    }

    private void ClearPaintingUVLineData()
    {
        _paintingTexturePositions.Clear();
        _paintingPressures.Clear();
    }

    private void ClearErasingUVLineData()
    {
        _erasingTexturePositions.Clear();
        _erasingPressures.Clear();
    }

    private Vector2? WorldToUV(Vector3 worldPosition)
    {
        var localPosition = _objectForPaintRenderer.transform.InverseTransformPoint(worldPosition);

        float uvX = (localPosition.x - _bounds.min.x) / _bounds.size.x;
        float uvY = (localPosition.z - _bounds.min.z) / _bounds.size.z;

        uvX = Mathf.Clamp01(uvX);
        uvY = Mathf.Clamp01(uvY);

        return new Vector2(1f - uvX, 1f - uvY);
    }

    private Vector3 UvToTexturePosition(Vector2 uvPosition)
    {
        return new Vector2(uvPosition.x * _textureWidth, uvPosition.y * _textureHeight);
    }

    private void OnDestroy()
    {
        // Clean up material instances to prevent memory leaks
        if (_paintMaterial != null)
        {
            DestroyImmediate(_paintMaterial);
            _paintMaterial = null;
        }

        if (_eraseMaterial != null)
        {
            DestroyImmediate(_eraseMaterial);
            _eraseMaterial = null;
        }
    }

}
