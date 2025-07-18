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
    [SerializeField] private bool _checkDistanceForPaint = true;
    
    private PainterConfig _painterConfig;
    private bool _active;
    private float _timer;

    private Vector3 _lastPaintPosition;
    private bool _hasPreviousPaintPosition;

    public void Setup( PainterConfig painterConfig)
    {
        _painterConfig = painterConfig;
        _active = true;
        _lastPaintPosition = transform.position;
        _hasPreviousPaintPosition = false;
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
        
        if (!_checkDistanceForPaint)
        {
            if(_lastPaintPosition == currentPosition) return;
            
            _lastPaintPosition = currentPosition;
            PaintAtPosition(currentPosition);
            return;
        }
        
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
        // Request draw point through DrawPointManager
        DrawPointManager.Instance.RequestDrawPoint(position, _painterConfig.Pressure, _painterConfig.IsErasing);
    }
}
