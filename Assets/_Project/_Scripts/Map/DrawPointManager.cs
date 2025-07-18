using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XDPaint;
using XDPaint.Controllers;
using XDPaint.Controllers.InputData;
using XDPaint.Core;
using XDPaint.Tools.Raycast;
using XDPaint.Tools.Raycast.Base;
using XDPaint.Tools.Raycast.Data;

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
    private readonly Dictionary<int, List<Vector2>> _paintingUVPositions = new Dictionary<int, List<Vector2>>();
    private readonly Dictionary<int, List<float>> _paintingUVPressures = new Dictionary<int, List<float>>();
    private readonly Dictionary<int, PaintManager> _paintingUVPaintManagers = new Dictionary<int, PaintManager>();
    private readonly Dictionary<int, PainterConfig> _paintingUVPainterConfigs = new Dictionary<int, PainterConfig>();
    
    // Erasing UV lines
    private readonly Dictionary<int, List<Vector2>> _erasingUVPositions = new Dictionary<int, List<Vector2>>();
    private readonly Dictionary<int, List<float>> _erasingUVPressures = new Dictionary<int, List<float>>();
    private readonly Dictionary<int, PaintManager> _erasingUVPaintManagers = new Dictionary<int, PaintManager>();
    private readonly Dictionary<int, PainterConfig> _erasingUVPainterConfigs = new Dictionary<int, PainterConfig>();
    private int _frameCounter = 0;
    private bool _isProcessing = false;

    private Bounds _bounds;

    private bool _painting ;
    
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

    public void RequestDrawPoint(PaintManager paintManager, InputData inputData, PainterConfig painterConfig)
    {
        var fingerId = inputData.FingerId;
        var worldPosition = inputData.Position;

        // Calculate UV position from world position using texture dimensions
        var uvPosition = WorldToUV(worldPosition, paintManager);

        if (uvPosition.HasValue)
        {
            // Determine if this is painting or erasing
            bool isErasing = painterConfig.IsErasing;
            
            if (isErasing)
            {
                // Initialize erasing UV line data for this finger ID if not exists
                if (!_erasingUVPositions.ContainsKey(fingerId))
                {
                    _erasingUVPositions[fingerId] = new List<Vector2>();
                    _erasingUVPressures[fingerId] = new List<float>();
                    _erasingUVPaintManagers[fingerId] = paintManager;
                    _erasingUVPainterConfigs[fingerId] = painterConfig;
                }

                // Add UV position and pressure to the erasing line
                _erasingUVPositions[fingerId].Add(uvPosition.Value);
                _erasingUVPressures[fingerId].Add(inputData.Pressure);
            }
            else
            {
                // Initialize painting UV line data for this finger ID if not exists
                if (!_paintingUVPositions.ContainsKey(fingerId))
                {
                    _paintingUVPositions[fingerId] = new List<Vector2>();
                    _paintingUVPressures[fingerId] = new List<float>();
                    _paintingUVPaintManagers[fingerId] = paintManager;
                    _paintingUVPainterConfigs[fingerId] = painterConfig;
                }

                // Add UV position and pressure to the painting line
                _paintingUVPositions[fingerId].Add(uvPosition.Value);
                _paintingUVPressures[fingerId].Add(inputData.Pressure);
            }
            
            if (_enableDebugLogs)
            {
                string toolType = isErasing ? "Erasing" : "Painting";
                Debug.Log($"DrawPointManager: Added {toolType} UV position {uvPosition.Value} for finger {fingerId}");
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
        if (_paintingUVPositions.Count == 0) return;
        
        // Set brush tool once at the beginning of the process phase
        PaintManager firstPaintManager = null;
        PainterConfig firstPainterConfig = null;
        
        // Get the first paint manager and config to set the tool
        foreach (var kvp in _paintingUVPositions)
        {
            var fingerId = kvp.Key;
            if (_paintingUVPaintManagers.ContainsKey(fingerId))
            {
                firstPaintManager = _paintingUVPaintManagers[fingerId];
                firstPainterConfig = _paintingUVPainterConfigs[fingerId];
                break;
            }
        }
        
        if (firstPaintManager != null && firstPainterConfig != null)
        {
            // Set brush tool once for all painting operations in this frame
            firstPaintManager.ToolsManager.SetTool(PaintTool.Brush);
            firstPaintManager.Brush.Size = firstPainterConfig.BrushSize;
            firstPaintManager.Brush.SetColor(firstPainterConfig.Color, true, false);
            
            if (_enableDebugLogs)
            {
                Debug.Log("DrawPointManager: Set BRUSH tool for painting lines");
            }
        }

        // Process all painting lines
        foreach (var kvp in _paintingUVPositions.ToList())
        {
            var fingerId = kvp.Key;
            var uvPositions = kvp.Value;
            
            // Only render lines with at least 2 positions
            if (uvPositions.Count >= 2)
            {
                var paintManager = _paintingUVPaintManagers[fingerId];
                var painterConfig = _paintingUVPainterConfigs[fingerId];
                var pressures = _paintingUVPressures[fingerId];
                
                RenderUVLineWithoutToolSetup(paintManager, uvPositions.ToArray(), pressures.ToArray(), fingerId);
                
                // Clear the line data after rendering
                ClearPaintingUVLineData(fingerId);
            }
        }
    }
    
    private void ProcessErasingLines()
    {
        if (_erasingUVPositions.Count == 0) return;
        
        
        // Set erase tool once at the beginning of the process phase
        PaintManager firstPaintManager = null;
        PainterConfig firstPainterConfig = null;
        
        // Get the first paint manager and config to set the tool
        foreach (var kvp in _erasingUVPositions)
        {
            var fingerId = kvp.Key;
            if (_erasingUVPaintManagers.ContainsKey(fingerId))
            {
                firstPaintManager = _erasingUVPaintManagers[fingerId];
                firstPainterConfig = _erasingUVPainterConfigs[fingerId];
                break;
            }
        }
        
        if (firstPaintManager != null && firstPainterConfig != null)
        {
            // Set erase tool once for all erasing operations in this frame
            firstPaintManager.ToolsManager.SetTool(PaintTool.Erase);
            firstPaintManager.Brush.Size = firstPainterConfig.BrushSize;
            
            if (_enableDebugLogs)
            {
                Debug.Log("DrawPointManager: Set ERASE tool for erasing lines");
            }
        }

        // Process all erasing lines
        foreach (var kvp in _erasingUVPositions.ToList())
        {
            var fingerId = kvp.Key;
            var uvPositions = kvp.Value;
            
            // Only render lines with at least 2 positions
            if (uvPositions.Count >= 2)
            {
                var paintManager = _erasingUVPaintManagers[fingerId];
                var painterConfig = _erasingUVPainterConfigs[fingerId];
                var pressures = _erasingUVPressures[fingerId];
                
                RenderUVLineWithoutToolSetup(paintManager, uvPositions.ToArray(), pressures.ToArray(), fingerId);
                
                // Clear the line data after rendering
                ClearErasingUVLineData(fingerId);
            }
        }
    }

    private void RenderUVLine(PaintManager paintManager, PainterConfig painterConfig, Vector2[] uvPositions, float[] pressures, int fingerId)
    {
        // Setup paint manager for line rendering
        var targetTool = painterConfig.IsErasing ? PaintTool.Erase : PaintTool.Brush;
        paintManager.ToolsManager.SetTool(targetTool);
        paintManager.Brush.Size = painterConfig.BrushSize;

        if (!painterConfig.IsErasing)
        {
            paintManager.Brush.SetColor(painterConfig.Color, true, false);
        }

        // Use the new DrawLineFromUV method in BasePaintObject
        paintManager.PaintObject.DrawLineFromUV(uvPositions, pressures, fingerId);

        if (_enableDebugLogs)
        {
            Debug.Log($"DrawPointManager: Rendered UV line with {uvPositions.Length} positions for finger {fingerId}");
        }
    }
    
    private void RenderUVLineWithoutToolSetup(PaintManager paintManager, Vector2[] uvPositions, float[] pressures, int fingerId)
    {
        // Render UV line without setting up the tool (tool is already set at the beginning of the process phase)
        paintManager.PaintObject.DrawLineFromUV(uvPositions, pressures, fingerId);

        if (_enableDebugLogs)
        {
            Debug.Log($"DrawPointManager: Rendered UV line with {uvPositions.Length} positions for finger {fingerId}");
        }
    }

    public void RenderUVLineForFinger(int fingerId)
    {
        // Check painting lines first
        if (_paintingUVPositions.ContainsKey(fingerId) && _paintingUVPositions[fingerId].Count >= 2)
        {
            var uvPositions = _paintingUVPositions[fingerId];
            var paintManager = _paintingUVPaintManagers[fingerId];
            var painterConfig = _paintingUVPainterConfigs[fingerId];
            var pressures = _paintingUVPressures[fingerId];
            
            RenderUVLine(paintManager, painterConfig, uvPositions.ToArray(), pressures.ToArray(), fingerId);
            ClearPaintingUVLineData(fingerId);
        }
        
        // Check erasing lines
        if (_erasingUVPositions.ContainsKey(fingerId) && _erasingUVPositions[fingerId].Count >= 2)
        {
            var uvPositions = _erasingUVPositions[fingerId];
            var paintManager = _erasingUVPaintManagers[fingerId];
            var painterConfig = _erasingUVPainterConfigs[fingerId];
            var pressures = _erasingUVPressures[fingerId];
            
            RenderUVLine(paintManager, painterConfig, uvPositions.ToArray(), pressures.ToArray(), fingerId);
            ClearErasingUVLineData(fingerId);
        }
    }

    private void ClearPaintingUVLineData(int fingerId)
    {
        if (_paintingUVPositions.ContainsKey(fingerId))
        {
            _paintingUVPositions.Remove(fingerId);
            _paintingUVPressures.Remove(fingerId);
            _paintingUVPaintManagers.Remove(fingerId);
            _paintingUVPainterConfigs.Remove(fingerId);
        }
    }
    
    private void ClearErasingUVLineData(int fingerId)
    {
        if (_erasingUVPositions.ContainsKey(fingerId))
        {
            _erasingUVPositions.Remove(fingerId);
            _erasingUVPressures.Remove(fingerId);
            _erasingUVPaintManagers.Remove(fingerId);
            _erasingUVPainterConfigs.Remove(fingerId);
        }
    }

    public int GetUVLinePositionCount(int fingerId)
    {
        int paintingCount = _paintingUVPositions.ContainsKey(fingerId) ? _paintingUVPositions[fingerId].Count : 0;
        int erasingCount = _erasingUVPositions.ContainsKey(fingerId) ? _erasingUVPositions[fingerId].Count : 0;
        return paintingCount + erasingCount;
    }

    /// <summary>
    /// Converts world position to UV coordinates using texture dimensions and object bounds
    /// </summary>
    private Vector2? WorldToUV(Vector3 worldPosition, PaintManager paintManager)
    {
        try
        {
            var localPosition = _objectForPaintRenderer.transform.InverseTransformPoint(worldPosition);
            
            var uvX = (localPosition.x - _bounds.min.x) / _bounds.size.x;
            var uvY = (localPosition.z - _bounds.min.z) / _bounds.size.z; // Using Z for Y in UV space
 
            // Clamp UV coordinates to valid range
            uvX = Mathf.Clamp01(uvX);
            uvY = Mathf.Clamp01(uvY);

            var uvPosition = new Vector2(1f-uvX, 1f-uvY);

            if (_enableDebugLogs)
            {
                Debug.Log($"DrawPointManager: World pos {worldPosition} -> Local pos {localPosition} -> UV {uvPosition}");
                Debug.Log($"DrawPointManager: Mesh bounds {_bounds}, Texture size {_textureWidth}x{_textureHeight}");
            }

            return uvPosition;
        }
        catch (System.Exception e)
        {
            if (_enableDebugLogs)
                Debug.LogError($"DrawPointManager: Error calculating UV position: {e.Message}");
            return null;
        }
    }

}

[System.Serializable]
public class DrawPointRequest
{
    public PaintManager PaintManager;
    public InputData InputData;
    public PainterConfig PainterConfig;
    public RaycastData RaycastData;
}
