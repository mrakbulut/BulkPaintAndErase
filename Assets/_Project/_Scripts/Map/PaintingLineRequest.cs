using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XDPaint;
using XDPaint.Controllers;
using XDPaint.Controllers.InputData;
using XDPaint.Core;
using XDPaint.Tools.Image.Base;
using XDPaint.Tools.Raycast.Data;

public class PaintingLineRequest
{
    private readonly PaintManager _paintManager;
    private readonly InputData _startInputData;
    private readonly InputData _endInputData;
    private readonly Color _color;
    private readonly float _brushSize;
    private readonly System.Action<int, int> _onCompleted;

    private IPaintTool _previousTool;
    private Color _previousBrushColor;
    private float _previousBrushSize;

    private RaycastData _startRaycastData;
    private RaycastData _endRaycastData;
    private bool _startRaycastReceived;
    private bool _endRaycastReceived;

    public PaintingLineRequest(PaintManager paintManager, InputData startInputData, InputData endInputData, Color color, float brushSize, System.Action<int, int> onCompleted = null)
    {
        _paintManager = paintManager;
        _startInputData = startInputData;
        _endInputData = endInputData;
        _color = color;
        _brushSize = brushSize;
        _onCompleted = onCompleted;

        RequestRaycastData();
    }

    private void RequestRaycastData()
    {
        // Start raycast request
        RaycastController.Instance.RequestRaycast(_paintManager, _startInputData, default(InputData), container =>
        {
            _startRaycastData = RaycastController.Instance.TryGetRaycast(container, _startInputData.FingerId, _startInputData.Ray.origin);
            _startRaycastReceived = true;
            CheckIfReadyToExecute();
        });

        // End raycast request
        RaycastController.Instance.RequestRaycast(_paintManager, _endInputData, default(InputData), container =>
        {
            _endRaycastData = RaycastController.Instance.TryGetRaycast(container, _endInputData.FingerId, _endInputData.Ray.origin);
            _endRaycastReceived = true;
            CheckIfReadyToExecute();
        });
    }

    private void CheckIfReadyToExecute()
    {
        if (_startRaycastReceived && _endRaycastReceived)
        {
            ExecuteDrawLine();
        }
    }

    private void ExecuteDrawLine()
    {
        if (_startRaycastData != null && _endRaycastData != null)
        {
            // Store previous settings
            StorePreviousSettings();

            // Configure painting settings
            ConfigurePaintingSettings();

            // Generate intermediate raycasts for continuous line
            var lineRaycasts = GenerateLineRaycasts(_startRaycastData, _endRaycastData);

            // Duplicate first element for better start coverage
            if (lineRaycasts.Length > 0)
            {
                var raycastList = new List<KeyValuePair<Ray, RaycastData>>();
                raycastList.AddRange(lineRaycasts);
                lineRaycasts = raycastList.ToArray();
            }

            _paintManager.PaintObject.DrawLine(_endInputData, _startInputData, _endRaycastData, _startRaycastData, lineRaycasts);

            // Restore previous settings
            RestorePreviousSettings();
        }

        _onCompleted?.Invoke(_startInputData.FingerId, _endInputData.FingerId);
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

    private void ConfigurePaintingSettings()
    {
        if (_paintManager != null)
        {
            _paintManager.ToolsManager.SetTool(PaintTool.Brush);
            _paintManager.Brush.SetColor(_color, true, false);
            _paintManager.Brush.Size = _brushSize;
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

    private KeyValuePair<Ray, RaycastData>[] GenerateLineRaycasts(RaycastData start, RaycastData end)
    {
        var lineRaycasts = new List<KeyValuePair<Ray, RaycastData>>();

        // Calculate distance and steps for intermediate raycasts
        float distance = Vector3.Distance(start.WorldHit, end.WorldHit);
        float brushSize = _paintManager.Brush.Size;
        float raycastInterval = 1f; // Interval for intermediate points
        int steps = Mathf.Max(2, Mathf.FloorToInt(distance / (brushSize * raycastInterval)));

        // Generate intermediate raycasts
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            var interpolatedWorldPos = Vector3.Lerp(start.WorldHit, end.WorldHit, t);

            // Create ray from interpolated world position (pointing down)
            var ray = new Ray(interpolatedWorldPos + Vector3.up * 10f, Vector3.down);

            // Request raycast for this intermediate position with unique fingerId
            var raycastData = RaycastController.Instance.RaycastLocal(_paintManager, ray, interpolatedWorldPos, _startInputData.FingerId + i + 1000);

            if (raycastData != null)
            {
                lineRaycasts.Add(new KeyValuePair<Ray, RaycastData>(ray, raycastData));
            }
        }

        return lineRaycasts.ToArray();
    }
}
