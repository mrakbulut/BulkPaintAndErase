using System.Collections.Generic;
using UnityEngine;
using XDPaint;
using XDPaint.Controllers;
using XDPaint.Controllers.InputData;
using XDPaint.Core;
using XDPaint.Tools.Image.Base;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Utils;

public class Painter : IDisposable
{
    private readonly PaintManager _paintManager;
    private readonly Transform _transform;
    private readonly PainterConfig _config;
    private readonly int _fingerId;
    private readonly Dictionary<PaintManager, FrameDataBuffer<PaintState>> _paintStates;

    private IPaintTool _previousTool;
    private Color _previousBrushColor;
    private float _previousBrushSize;

    public Painter(PaintManager paintManager, Transform transform, PainterConfig config, int fingerId)
    {
        _paintManager = paintManager;
        _transform = transform;
        _config = config;
        _fingerId = fingerId;
        _paintStates = new Dictionary<PaintManager, FrameDataBuffer<PaintState>>();
    }

    public void PaintAtPosition(Vector3 worldPosition)
    {
        var ray = new Ray(worldPosition + Vector3.up * 10f, Vector3.down);
        var inputData = new InputData(ray, worldPosition, _config.Pressure, InputSource.World, _fingerId);

        var previousInputData = default(InputData);
        if (_paintStates.ContainsKey(_paintManager) && _paintStates[_paintManager].Count > 0)
        {
            var previousState = _paintStates[_paintManager].GetFrameData(0);
            previousInputData = previousState.InputData;
        }

        RaycastController.Instance.RequestRaycast(_paintManager, inputData, previousInputData, container =>
        {
            var raycastData = RaycastController.Instance.TryGetRaycast(container, inputData.FingerId, inputData.Ray.origin);
            if (raycastData != null)
            {
                ProcessPaint(inputData, previousInputData, raycastData);
            }
        });
    }

    private void ProcessPaint(InputData inputData, InputData previousInputData, RaycastData raycastData)
    {
        if (!_paintStates.ContainsKey(_paintManager))
        {
            _paintStates.Add(_paintManager, new FrameDataBuffer<PaintState>(2));
        }

        _paintStates[_paintManager].AddFrameData(new PaintState
        {
            InputData = inputData,
            RaycastData = raycastData,
            CollisionTransform = _transform
        });

        StorePreviousSettings();

        ConfigurePaintSettings();


        if (_paintStates[_paintManager].Count > 1)
        {
            var previous = _paintStates[_paintManager].GetFrameData(1);
            _paintManager.PaintObject.DrawLine(inputData, previousInputData, raycastData, previous.RaycastData);
        }
        else
        {
            _paintManager.PaintObject.DrawPoint(inputData, raycastData);
        }

        RestorePreviousSettings();
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

    private void ConfigurePaintSettings()
    {
        if (_paintManager == null) return;

        var targetTool = _config.IsErasing ? PaintTool.Erase : PaintTool.Brush;
        _paintManager.ToolsManager.SetTool(targetTool);

        _paintManager.Brush.Size = _config.BrushSize;

        if (!_config.IsErasing)
        {
            _paintManager.Brush.SetColor(_config.Color, true, false);
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

    public void ClearPaintStates()
    {
        foreach (var paintState in _paintStates.Values)
        {
            paintState.DoDispose();
        }
        _paintStates.Clear();
    }
    private class PaintState
    {
        public InputData InputData;
        public RaycastData RaycastData;
        public Transform CollisionTransform;
    }
    public void DoDispose()
    {
        ClearPaintStates();
    }
}

[System.Serializable]
public class PainterConfig
{
    public bool IsErasing;
    public Color Color;
    public float Pressure;
    public float BrushSize;
}
