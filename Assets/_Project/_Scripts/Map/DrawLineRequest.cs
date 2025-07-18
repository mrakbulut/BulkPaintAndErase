using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XDPaint;
using XDPaint.Controllers;
using XDPaint.Controllers.InputData;
using XDPaint.Tools.Raycast.Data;

public class DrawLineRequest
{
    private readonly PaintManager _paintManager;
    private readonly InputData _startInputData;
    private readonly InputData _endInputData;
    private readonly System.Action<int, int> _onCompleted;

    private RaycastData _startRaycastData;
    private RaycastData _endRaycastData;
    private bool _startRaycastReceived;
    private bool _endRaycastReceived;

    public DrawLineRequest(PaintManager paintManager, InputData startInputData, InputData endInputData, System.Action<int, int> onCompleted = null)
    {
        _paintManager = paintManager;
        _startInputData = startInputData;
        _endInputData = endInputData;
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
            // Generate intermediate raycasts for continuous line
            var lineRaycasts = GenerateLineRaycasts(_startRaycastData, _endRaycastData);

            // Duplicate first element for better start coverage
            if (lineRaycasts.Length > 0)
            {
                var raycastList = new List<KeyValuePair<Ray, RaycastData>>();
                raycastList.AddRange(lineRaycasts); // Add all elements
                lineRaycasts = raycastList.ToArray();
            }

            _paintManager.PaintObject.DrawLine(_endInputData, _startInputData, _endRaycastData, _startRaycastData, lineRaycasts);
        }

        _onCompleted?.Invoke(_startInputData.FingerId, _endInputData.FingerId);
    }

    private KeyValuePair<Ray, RaycastData>[] GenerateLineRaycasts(RaycastData start, RaycastData end)
    {
        var lineRaycasts = new List<KeyValuePair<Ray, RaycastData>>();

        //Debug.Log("START WORLD HIT : " + start.WorldHit + ", END WORLD HIT : " + end.WorldHit);

        // Calculate distance and steps for intermediate raycasts
        float distance = Vector3.Distance(start.WorldHit, end.WorldHit);
        float brushSize = _paintManager.Brush.Size;
        float raycastInterval = 1f; // Interval for intermediate points
        int steps = Mathf.Max(2, Mathf.FloorToInt(distance / (brushSize * raycastInterval)));

        //Debug.Log($"GenerateLineRaycasts: Distance={distance}, Steps={steps}, BrushSize={brushSize}");

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
                //Debug.Log($"Added intermediate raycast at {interpolatedWorldPos}");
            }
            else
            {
                //Debug.LogWarning($"Failed to generate raycast at {interpolatedWorldPos}");
            }
        }

        //Debug.Log($"Generated {lineRaycasts.Count} intermediate raycasts");
        return lineRaycasts.ToArray();
    }
}
