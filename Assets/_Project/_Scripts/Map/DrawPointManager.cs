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
    [SerializeField] private bool _enableDebugLogs = false;

    // Painting UV lines
    private readonly List<Vector2> _paintingTexturePositions = new List<Vector2>();
    private readonly List<float> _paintingPressures = new List<float>();
    private PaintManager _paintManager;
    private PainterConfig _paintingUVPainterConfig;

    // Erasing UV lines
    private readonly List<Vector2> _erasingTexturePositions = new List<Vector2>();
    private readonly List<float> _erasingPressures = new List<float>();
    private PainterConfig _erasingUVPainterConfig;
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

            // Process UV lines that have accumulated positions
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
        _painting = !_painting;

        if (_painting)
        {
            ProcessPaintingLines();
        }
        else
        {
            ProcessErasingLines();
        }
    }

    private void ProcessPaintingLines()
    {
        if (_paintingTexturePositions.Count == 0) return;

        _paintManager.ToolsManager.SetTool(PaintTool.Brush);
        _paintManager.Brush.Size = _paintingUVPainterConfig.BrushSize;
        _paintManager.Brush.SetColor(_paintingUVPainterConfig.Color, true, false);

        RenderUVLineWithoutToolSetup(_paintingTexturePositions.ToArray(), _paintingPressures.ToArray());

        ClearPaintingUVLineData();
    }

    private void ProcessErasingLines()
    {
        if (_erasingTexturePositions.Count == 0) return;

        _paintManager.ToolsManager.SetTool(PaintTool.Erase);
        _paintManager.Brush.Size = _erasingUVPainterConfig.BrushSize;

        RenderUVLineWithoutToolSetup(_erasingTexturePositions.ToArray(), _erasingPressures.ToArray());

        ClearErasingUVLineData();
    }

    private void RenderUVLineWithoutToolSetup(Vector2[] uvPositions, float[] pressures)
    {
        _paintManager.PaintObject.DrawLineFromUV(uvPositions, pressures);
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

}
