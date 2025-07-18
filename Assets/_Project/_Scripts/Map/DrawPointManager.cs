using System.Collections.Generic;
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

    [SerializeField] private int _frameInterval = 3;
    [SerializeField] private int _maxBatchSize = 10;
    [SerializeField] private bool _enableDebugLogs = false;

    private readonly List<DrawPointRequest> _drawList = new List<DrawPointRequest>();
    private readonly Dictionary<PaintManager, List<DrawPointRequest>> _batchedRequests = new Dictionary<PaintManager, List<DrawPointRequest>>();
    private int _frameCounter = 0;
    private bool _isProcessing = false;

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

    private void Update()
    {
        _frameCounter++;

        if (_frameCounter >= _frameInterval && !_isProcessing && _drawList.Count > 0)
        {
            // Debug.Log("PROCESSING BATCHED DRAW REQUESTS : " + _drawList.Count);
            ProcessBatchedDrawRequests();
            _frameCounter = 0;
        }
    }

    public void RequestDrawPoint(PaintManager paintManager, InputData inputData, PainterConfig painterConfig)
    {
        // Perform raycast immediately and store the result
        RaycastController.Instance.RequestRaycast(
            paintManager,
            inputData,
            default(InputData),
            container => OnRaycastCompleteForQueue(container, paintManager, inputData, painterConfig)
            );
    }

    private void OnRaycastCompleteForQueue(RaycastRequestContainer container, PaintManager paintManager, InputData inputData, PainterConfig painterConfig)
    {
        var raycastData = RaycastController.Instance.TryGetRaycast(container, inputData.FingerId, inputData.Ray.origin);

        if (raycastData != null)
        {
            var request = new DrawPointRequest
            {
                PaintManager = paintManager,
                InputData = inputData,
                PainterConfig = painterConfig,
                RaycastData = raycastData
            };

            _drawList.Add(request);

            if (_enableDebugLogs)
            {
                Debug.Log($"DrawPointManager: Added draw request with raycast data. List size: {_drawList.Count}");
            }
        }
        else if (_enableDebugLogs)
        {
            Debug.LogWarning("DrawPointManager: Raycast failed, request not queued");
        }
    }

    private void ProcessBatchedDrawRequests()
    {
        if (_drawList.Count == 0 || _isProcessing)
        {
            return;
        }

        _isProcessing = true;
        _batchedRequests.Clear();

        // Determine the operation type from the first request
        var firstRequest = _drawList[0];
        bool isErasingBatch = firstRequest.PainterConfig.IsErasing;

        // Group requests by PaintManager, but only for the same operation type (FIFO order)
        int processedCount = 0;
        var requestsToRemove = new List<DrawPointRequest>();

        for (int i = 0; i < _drawList.Count && processedCount < _maxBatchSize; i++)
        {
            var request = _drawList[i];

            // Only process requests with the same operation type (drawing or erasing)
            if (request.PainterConfig.IsErasing != isErasingBatch)
            {
                continue; // Stop processing if operation type changes
            }

            if (!_batchedRequests.ContainsKey(request.PaintManager))
            {
                _batchedRequests[request.PaintManager] = new List<DrawPointRequest>();
            }

            _batchedRequests[request.PaintManager].Add(request);
            requestsToRemove.Add(request);
            processedCount++;
        }

        // Remove processed requests from the list (maintaining FIFO order)
        foreach (var request in requestsToRemove)
        {
            _drawList.Remove(request);
        }

        if (_enableDebugLogs)
        {
            string operationType = isErasingBatch ? "ERASING" : "DRAWING";
            Debug.Log($"DrawPointManager: Processing {processedCount} {operationType} requests in {_batchedRequests.Count} batches. Remaining list size: {_drawList.Count}");
        }

        // Process each batch
        foreach (var kvp in _batchedRequests)
        {
            var paintManager = kvp.Key;
            var requests = kvp.Value;

            ProcessBatchForPaintManager(paintManager, requests);
        }

        _isProcessing = false;
    }

    private void ProcessBatchForPaintManager(PaintManager paintManager, List<DrawPointRequest> requests)
    {
        if (requests.Count == 0) return;

        // All requests in this batch have the same operation type (drawing or erasing)
        // Now group by other config parameters (color, brush size, pressure) for drawing operations
        var firstRequest = requests[0];

        ProcessConfigGroup(paintManager, firstRequest.PainterConfig, requests);
    }

    private void ProcessConfigGroup(PaintManager paintManager, PainterConfig painterConfig, List<DrawPointRequest> requests)
    {
        // Store previous settings
        /*var previousTool = paintManager.ToolsManager.CurrentTool;
        var previousColor = paintManager.Brush.Color;
        float previousSize = paintManager.Brush.Size;*/

        // Apply current settings once for the entire batch
        var targetTool = painterConfig.IsErasing ? PaintTool.Erase : PaintTool.Brush;
        paintManager.ToolsManager.SetTool(targetTool);
        paintManager.Brush.Size = painterConfig.BrushSize;

        if (!painterConfig.IsErasing)
        {
            paintManager.Brush.SetColor(painterConfig.Color, true, false);
        }

        if (_enableDebugLogs)
        {
            string operationType = painterConfig.IsErasing ? "ERASING" : "DRAWING";
            Debug.Log($"DrawPointManager: {operationType} - Processing {requests.Count} requests with Tool={targetTool}, Color={painterConfig.Color}, BrushSize={painterConfig.BrushSize}");
        }

        // Process all requests using bulk DrawPoints operation
        if (requests.Count == 1)
        {
            // Single point - use original DrawPoint for compatibility
            ExecuteSingleDrawPoint(requests[0]);
        }
        else
        {
            // Multiple points - use new DrawPoints bulk method
            ExecuteBulkDrawPoints(requests);
        }

        /*// Restore previous settings
        if (previousTool != null)
        {
            paintManager.ToolsManager.SetTool(previousTool.Type);
            paintManager.Brush.SetColor(previousColor, true, false);
            paintManager.Brush.Size = previousSize;
        }*/
    }


    private void ExecuteSingleDrawPoint(DrawPointRequest request)
    {
        // Raycast data is already available in the request
        if (request.RaycastData != null)
        {
            request.PaintManager.PaintObject.DrawPoint(request.InputData, request.RaycastData);
        }
        else if (_enableDebugLogs)
        {
            Debug.LogWarning("DrawPointManager: RaycastData is null for draw request");
        }
    }

    private void ExecuteBulkDrawPoints(List<DrawPointRequest> requests)
    {
        if (requests.Count == 0) return;

        // All requests in this list have the same PaintManager and config
        var paintManager = requests[0].PaintManager;

        // Prepare arrays for bulk operation
        var inputDataArray = new InputData[requests.Count];
        var raycastDataArray = new RaycastData[requests.Count];
        int[] fingerIds = new int[requests.Count];

        int validCount = 0;
        for (int i = 0; i < requests.Count; i++)
        {
            var request = requests[i];
            if (request.RaycastData != null)
            {
                inputDataArray[validCount] = request.InputData;
                raycastDataArray[validCount] = request.RaycastData;
                fingerIds[validCount] = request.InputData.FingerId;
                validCount++;
            }
            else if (_enableDebugLogs)
            {
                Debug.LogWarning("DrawPointManager: RaycastData is null for bulk draw request");
            }
        }

        if (validCount > 0)
        {
            // Resize arrays to only include valid requests
            if (validCount < requests.Count)
            {
                System.Array.Resize(ref inputDataArray, validCount);
                System.Array.Resize(ref raycastDataArray, validCount);
                System.Array.Resize(ref fingerIds, validCount);
            }

            // Execute bulk draw operation
            paintManager.PaintObject.DrawPoints(inputDataArray, raycastDataArray, fingerIds);

            if (_enableDebugLogs)
            {
                Debug.Log($"DrawPointManager: Executed bulk draw operation with {validCount} points");
            }
        }
    }

    public void SetFrameInterval(int frameInterval)
    {
        _frameInterval = Mathf.Max(1, frameInterval);

        if (_enableDebugLogs)
        {
            Debug.Log($"DrawPointManager: Frame interval set to {_frameInterval}");
        }
    }

    public void SetMaxBatchSize(int maxBatchSize)
    {
        _maxBatchSize = Mathf.Max(1, maxBatchSize);

        if (_enableDebugLogs)
        {
            Debug.Log($"DrawPointManager: Max batch size set to {_maxBatchSize}");
        }
    }

    public int GetListSize()
    {
        return _drawList.Count;
    }

    public void ClearList()
    {
        _drawList.Clear();
        _isProcessing = false;

        if (_enableDebugLogs)
        {
            Debug.Log("DrawPointManager: List cleared");
        }
    }

    public void SetDebugMode(bool enabled)
    {
        _enableDebugLogs = enabled;
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
