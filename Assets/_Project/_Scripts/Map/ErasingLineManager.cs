using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XDPaint;
using XDPaint.Controllers.InputData;
using XDPaint.Core;

public class ErasingLineManager : MonoBehaviour
{
    private static ErasingLineManager _instance;
    public static ErasingLineManager Instance => _instance;

    private readonly Queue<DrawLineRequestData> _drawLineQueue = new Queue<DrawLineRequestData>();
    private bool _isProcessing = false;
    private PaintManager _cachedPaintManager;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    public void RequestDrawLine(DrawLineRequestData requestData)
    {
        _drawLineQueue.Enqueue(requestData);
        ProcessQueue();
    }

    private void CreateErasingLineRequest(DrawLineRequestData requestData)
    {
        // Use finger IDs from request data
        int startFingerId = requestData.StartFingerId;
        int endFingerId = requestData.EndFingerId;

        // Create InputData objects with assigned finger IDs
        var startInputData = new InputData(requestData.StartRay, requestData.StartPosition, requestData.Pressure, requestData.InputSource, startFingerId);
        var endInputData = new InputData(requestData.EndRay, requestData.EndPosition, requestData.Pressure, requestData.InputSource, endFingerId);

        new ErasingLineRequest(_cachedPaintManager, startInputData, endInputData, requestData.BrushSize, OnDrawLineCompleted);
    }

    private void ProcessQueue()
    {
        if (_isProcessing || _drawLineQueue.Count == 0)
        {
            return;
        }

        _isProcessing = true;
        var nextRequest = _drawLineQueue.Dequeue();
        CreateErasingLineRequest(nextRequest);
    }

    private void OnDrawLineCompleted(int startFingerId, int endFingerId)
    {
        // FingerIndexManager.Instance.ReleaseFingerIndexes(startFingerId, endFingerId);

        // Debug.Log("ERASING - RELEASING FINGER INDEXES : " + startFingerId + ", " + endFingerId);

        StartCoroutine(ProcessNextFrameCoroutine());
    }

    private IEnumerator ProcessNextFrameCoroutine()
    {
        yield return null; // Wait 1 frame
        _isProcessing = false;
        ProcessQueue();
    }

    public void ClearQueue()
    {
        _drawLineQueue.Clear();
        _isProcessing = false;
    }

    public void SetPaintManager(PaintManager paintManager)
    {
        _cachedPaintManager = paintManager;
    }

    public int GetQueueCount()
    {
        return _drawLineQueue.Count;
    }
}
