using Pathfinding;
using UnityEngine;
using XDPaint;
using XDPaint.Controllers;
using XDPaint.Controllers.InputData;
using XDPaint.Core;


public class SimpleUnit : MonoBehaviour
{
    [SerializeField] private float _lifetime = 5f;
    [SerializeField] private FollowerEntity _followerEntity;
    [SerializeField] private float _unitSpeed = 5f;
    [SerializeField] private float _paintDistance = 0.5f; // Distance between paint points
    [SerializeField] private float _heightOffset = 0.1f;

    private PaintManager _paintManager;
    private PainterConfig _painterConfig;
    private bool _active;
    private float _timer;

    private Vector3 _lastPaintPosition;
    private bool _hasPreviousPaintPosition;

    private int _startFingerId;

    public void Setup(PaintManager paintManager, PainterConfig painterConfig)
    {
        _paintManager = paintManager;
        _painterConfig = painterConfig;
        _active = true;
        _lastPaintPosition = transform.position;
        _hasPreviousPaintPosition = false;

        // Request finger ID for this unit
        _startFingerId = FingerIndexManager.Instance.GetNextFingerIndex();
    }


    public void SetDestination(Vector3 destination)
    {
        _followerEntity.enabled = true;
        _followerEntity.destination = destination;
        _followerEntity.maxSpeed = _unitSpeed;
    }

    private void Update()
    {
        if (!_active) return;

        _timer += Time.deltaTime;

        // Check if we've moved enough distance to paint
        CheckAndPaint();

        if (_timer >= _lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void CheckAndPaint()
    {
        var currentPosition = transform.position;

        // If we don't have a previous position, set it and paint first point
        if (!_hasPreviousPaintPosition)
        {
            _lastPaintPosition = currentPosition;
            _hasPreviousPaintPosition = true;
            PaintAtPosition(currentPosition);
            return;
        }

        // Calculate distance from last paint position
        float distanceTraveled = Vector3.Distance(currentPosition, _lastPaintPosition);

        // If we've traveled enough distance, paint a point
        if (distanceTraveled >= _paintDistance)
        {
            PaintAtPosition(currentPosition);
            _lastPaintPosition = currentPosition;
        }
    }

    private void PaintAtPosition(Vector3 position)
    {
        // Create ray pointing down from position
        var ray = new Ray(position + Vector3.up * _heightOffset, Vector3.down);
        var inputData = new InputData(ray, position, _painterConfig.Pressure, InputSource.World, _startFingerId);

        // Request draw point through DrawPointManager
        DrawPointManager.Instance.RequestDrawPoint(_paintManager, inputData, _painterConfig);
    }

    private void ReleaseFingerIds()
    {
        if (_startFingerId != -1)
        {
            FingerIndexManager.Instance.ReleaseFingerIndex(_startFingerId);
        }
    }

    private void OnDestroy()
    {
        if (FingerIndexManager.Instance != null)
        {
            ReleaseFingerIds();
        }
    }
}
