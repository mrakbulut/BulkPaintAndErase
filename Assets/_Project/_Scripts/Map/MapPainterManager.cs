using System;
using System.Collections.Generic;
using Pathfinding.Drawing;
using Sirenix.OdinInspector;
using UnityEngine;
using XDPaint;
using XDPaint.Core;
using XDPaint.Tools.Image.Base;
using XDPaint.Controllers;
using XDPaint.Controllers.InputData;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Utils;


public class MapPainterManager : MonoBehaviour
{
    [TitleGroup("Map Settings")]
    [SerializeField] private GameObject _objectForPainting;
    [SerializeField] private PaintManager _paintManager;

    [TitleGroup("Brush Settings")]
    [SerializeField] private Color _color = Color.blue;
    [SerializeField] private float _brushSize = 2f;
    [SerializeField] private float _eraseSize = 2f;
    [SerializeField] private float _heightOffset = .1f;

    [TitleGroup("Debug")]
    [Button("Toggle Debug Lines")]
    private void ToggleDebugLines()
    {
        _showDebugLines = !_showDebugLines;
    }

    [Button("Clear Debug Lines")]
    private void ClearDebugLines()
    {
        _debugLines.Clear();
        _showDebugLines = false;
    }

    private IPaintTool _previousTool;
    private Color _previousBrushColor;
    private float _previousBrushSize;

    private Vector3 _minBound;
    private Vector3 _maxBound;
    private Vector3 _centerBound;

    public Vector3 BottomLeftBound => new Vector3(_bottomX, _heightOffset, _bottomZ);
    public Vector3 TopRightBound => new Vector3(_topX, _heightOffset, _topZ);

    private float _bottomX;
    private float _bottomZ;
    private float _topX;
    private float _topZ;
    private float _xOffset = .5f;

    private MovementDirection _movementDirection;

    private const float _pressure = 1f;
    private int _fingerId = 0;

    private List<(Vector3 start, Vector3 end)> _debugLines = new List<(Vector3, Vector3)>();
    private bool _showDebugLines = false;

    #region Testing

    [TitleGroup("Painting The Whole Map")]
    [Button("PaintTheMapFromBottomToTop")]
    private void PaintTheMapFromBottomToTop()
    {
        _movementDirection = MovementDirection.BottomToTop;
        PaintTheMap(_movementDirection);
    }
    [Button("PaintTheMapFromTopToBottom")]
    private void PaintTheMapFromTopToBottom()
    {
        _movementDirection = MovementDirection.TopToBottom;
        PaintTheMap(_movementDirection);
    }
    [TitleGroup("Erasing The Whole Map")]
    [Button("EraseTheMapFromBottomToTop")]
    private void EraseTheMapFromBottomToTop()
    {
        _movementDirection = MovementDirection.BottomToTop;
        EraseTheMap(_movementDirection);
    }
    [Button("EraseTheMapFromTopToBottom")]
    private void EraseTheMapFromTopToBottom()
    {
        _movementDirection = MovementDirection.TopToBottom;
        EraseTheMap(_movementDirection);
    }
    [TitleGroup("Painting The Half Of The Map")]
    [Button("PaintTheBottomHalf")]
    private void PaintTheBottomHalf()
    {
        _movementDirection = MovementDirection.BottomToTop;
        PaintTheHalf(_movementDirection);
    }
    [Button("PaintTheUpperHalf")]
    private void PaintTheUpperHalf()
    {
        _movementDirection = MovementDirection.TopToBottom;
        PaintTheHalf(_movementDirection);
    }
    [TitleGroup("Erasing The Half Of The Map")]
    [Button("EraseTheBottomHalf")]
    private void EraseTheBottomHalf()
    {
        _movementDirection = MovementDirection.BottomToTop;
        EraseTheHalf(_movementDirection);
    }
    [Button("EraseTheUpperHalf")]
    private void EraseTheUpperHalf()
    {
        _movementDirection = MovementDirection.TopToBottom;
        EraseTheHalf(_movementDirection);
    }

    #endregion

    private void Awake()
    {
        CacheBounds();
    }

    private void Start()
    {
        PaintingLineManager.Instance.SetPaintManager(_paintManager);
        ErasingLineManager.Instance.SetPaintManager(_paintManager);
    }

    public void PaintTheMap(MovementDirection movementDirection)
    {
        _movementDirection = movementDirection;
        StorePreviousSettings();
        SetupPaintingSettings();

        DrawLinesAcrossMap(false, false);

        RestorePreviousSettings();
    }

    public void PaintTheHalf(MovementDirection movementDirection)
    {
        _movementDirection = movementDirection;
        StorePreviousSettings();
        SetupPaintingSettings();

        DrawLinesAcrossMap(true, false);

        RestorePreviousSettings();
    }

    public void EraseTheMap(MovementDirection movementDirection)
    {
        _movementDirection = movementDirection;
        StorePreviousSettings();
        SetupErasingSettings();

        DrawLinesAcrossMap(false, true);

        RestorePreviousSettings();
    }

    public void EraseTheHalf(MovementDirection movementDirection)
    {
        _movementDirection = movementDirection;
        StorePreviousSettings();
        SetupErasingSettings();

        DrawLinesAcrossMap(true, true);

        RestorePreviousSettings();
    }


    private void CacheBounds()
    {
        var bounds = GetObjectBounds(_objectForPainting);
        _bottomX = bounds.min.x;
        _bottomZ = bounds.min.z;

        _minBound = bounds.min;
        _maxBound = bounds.max;
        _centerBound = bounds.center;

        _topX = bounds.max.x;
        _topZ = bounds.max.z;
    }

    private void DrawLinesAcrossMap(bool halfOnly, bool erasing)
    {
        var xPositions = CalculateSpawnPositions();
        (float startZ, float endZ) = CalculateStartEndZ(halfOnly, erasing);

        // Clear previous debug lines
        _debugLines.Clear();

        foreach (float xPos in xPositions)
        {
            var startPosition = new Vector3(xPos, _heightOffset, startZ);
            var endPosition = new Vector3(xPos, _heightOffset, endZ);

            DrawLineBetweenPoints(startPosition, endPosition, 0, 1);

            // Store line for debug drawing
            _debugLines.Add((startPosition, endPosition));
        }

        // Enable debug line drawing
        _showDebugLines = true;
    }

    private List<float> CalculateSpawnPositions()
    {
        var positions = new List<float>();
        _xOffset = 1f;
        float startX = _bottomX + _xOffset;
        float endX = _topX - _xOffset;
        float distance = endX - startX;

        float spacing = _brushSize / 5f;
        int spawnCount = Mathf.FloorToInt(distance / spacing) + 1;

        for (int i = 0; i < spawnCount; i++)
        {
            float t = i / (float)(spawnCount - 1);
            float currentX = Mathf.Lerp(startX, endX, t);

            positions.Add(currentX);
        }

        return positions;
    }

    private void Update()
    {
        if (_showDebugLines)
        {
            foreach (var line in _debugLines)
            {
                Draw.Line(line.start, line.end, Color.red);
            }
        }
    }

    private void DrawLineBetweenPoints(Vector3 startPos, Vector3 endPos, int startFingerId, int endFingerId)
    {
        var startRay = new Ray(startPos, Vector3.down);
        var endRay = new Ray(endPos, Vector3.down);

        // Create request data (finger IDs will be assigned during processing)
        var requestData = new DrawLineRequestData(startRay, startPos, endRay, endPos, _pressure, InputSource.World, startFingerId, endFingerId, _color, _brushSize);

        PaintingLineManager.Instance.RequestDrawLine(requestData);
    }


    private void SetupPaintingSettings()
    {
        if (_paintManager != null)
        {
            _paintManager.ToolsManager.SetTool(PaintTool.Brush);
            _paintManager.Brush.SetColor(_color, true, false);
            _paintManager.Brush.Size = _brushSize;
        }
    }

    private void SetupErasingSettings()
    {
        if (_paintManager != null)
        {
            _paintManager.ToolsManager.SetTool(PaintTool.Erase);
            _paintManager.Brush.Size = _eraseSize;
        }
    }

    private void RestorePreviousSettings()
    {
        if (_paintManager != null && _previousTool != null)
        {
            _paintManager.ToolsManager.SetTool(_previousTool.Type);
            _paintManager.Brush.SetColor(_previousBrushColor, true, false);
            _paintManager.Brush.Size = _previousBrushSize;
        }
    }

    private void StorePreviousSettings()
    {
        if (_paintManager != null)
        {
            _previousTool = _paintManager.ToolsManager.CurrentTool;
            _previousBrushColor = _paintManager.Brush.Color;
            _previousBrushSize = _paintManager.Brush.Size;
        }
    }


    private (float, float) CalculateStartEndZ(bool goingToHalf, bool erasing)
    {
        if (_objectForPainting == null)
        {
            Debug.LogWarning("MapPainterManager: Object for painting atanmamış!");
            return (0f, 0f);
        }

        float startZ, endZ;

        switch (_movementDirection)
        {
            case MovementDirection.BottomToTop:
                startZ = _bottomZ;
                endZ = _topZ;
                break;

            case MovementDirection.TopToBottom:
                startZ = _topZ;
                endZ = _bottomZ;
                break;

            default:
                startZ = _bottomZ;
                endZ = _topZ;
                break;
        }
        //Debug.Log("START Z : " + startZ + ", END Z : " + endZ);

        if (goingToHalf)
        {
            float centerZ = (startZ + endZ) / 2f;
            endZ = centerZ;
        }

        if (erasing)
        {
            startZ -= _eraseSize;
            endZ += _eraseSize;
        }

        return (startZ, endZ);
    }

    private Bounds GetObjectBounds(GameObject obj)
    {
        // Önce Renderer bileşenini kontrol et
        var renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds;
        }

        // Sonra Collider bileşenini kontrol et
        var collider = obj.GetComponent<Collider>();
        if (collider != null)
        {
            return collider.bounds;
        }

        // 2D objeler için Collider2D kontrol et
        var collider2D = obj.GetComponent<Collider2D>();
        if (collider2D != null)
        {
            return collider2D.bounds;
        }

        // Fallback: transform pozisyonuna göre varsayılan bounds
        Debug.LogWarning("MapManager: Object bounds bulunamadı, varsayılan bounds kullanılıyor.");
        return new Bounds(obj.transform.position, Vector3.one);
    }

    public enum MovementDirection
    {
        BottomToTop,
        TopToBottom
    }
}
