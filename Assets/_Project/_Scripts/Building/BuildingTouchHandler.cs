using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class BuildingTouchHandler : MonoBehaviour
{
    [Header("Touch Settings")]
    [SerializeField] private float _touchSensitivity = 1f;
    [SerializeField] private float _longPressTime = 0.5f;

    // Events
    public event Action OnTapToPlace;
    public event Action OnLongPressToCancel;
    public event Action OnDragStarted;
    public event Action<Vector2> OnRemoveBuildingAt;

    // Touch input variables
    private Vector2 _touchStartPos;
    private float _touchStartTime;
    private int _primaryTouchId = -1;
    private bool _isDragging = false;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();

        #if UNITY_EDITOR
        TouchSimulation.Enable();
        #endif
    }

    private void OnDisable()
    {
        EnhancedTouchSupport.Disable();

        #if UNITY_EDITOR
        TouchSimulation.Disable();
        #endif
    }

    public void HandleTouchInputForPlacement()
    {
        if (Touch.activeTouches.Count > 0)
        {
            var primaryTouch = Touch.activeTouches[0];

            switch (primaryTouch.phase)
            {
                case TouchPhase.Began:
                    _touchStartPos = primaryTouch.screenPosition;
                    _touchStartTime = Time.time;
                    _primaryTouchId = primaryTouch.finger.index;
                    break;

                case TouchPhase.Moved:
                    if (primaryTouch.finger.index == _primaryTouchId)
                    {
                        float dragDistance = Vector2.Distance(primaryTouch.screenPosition, _touchStartPos);
                        if (dragDistance > _touchSensitivity && !_isDragging)
                        {
                            _isDragging = true;
                            OnDragStarted?.Invoke();
                        }
                    }
                    break;

                case TouchPhase.Ended:
                    if (primaryTouch.finger.index == _primaryTouchId)
                    {
                        float touchDuration = Time.time - _touchStartTime;

                        if (!_isDragging && touchDuration < _longPressTime)
                        {
                            OnTapToPlace?.Invoke();
                        }
                        else if (touchDuration >= _longPressTime)
                        {
                            OnLongPressToCancel?.Invoke();
                        }

                        ResetTouchInput();
                    }
                    break;

                case TouchPhase.Canceled:
                    if (primaryTouch.finger.index == _primaryTouchId)
                    {
                        OnLongPressToCancel?.Invoke();
                        ResetTouchInput();
                    }
                    break;
            }
        }
    }

    public void HandleTouchInputForSelection()
    {
        if (Touch.activeTouches.Count > 0)
        {
            var primaryTouch = Touch.activeTouches[0];

            if (primaryTouch.phase == TouchPhase.Began)
            {
                _touchStartPos = primaryTouch.screenPosition;
                _touchStartTime = Time.time;
                _primaryTouchId = primaryTouch.finger.index;
            }
            else if (primaryTouch.phase == TouchPhase.Ended && primaryTouch.finger.index == _primaryTouchId)
            {
                float touchDuration = Time.time - _touchStartTime;
                float dragDistance = Vector2.Distance(primaryTouch.screenPosition, _touchStartPos);

                if (touchDuration >= _longPressTime && dragDistance < _touchSensitivity)
                {
                    OnRemoveBuildingAt?.Invoke(primaryTouch.screenPosition);
                }

                ResetTouchInput();
            }
        }
    }

    public Vector2 GetCurrentScreenPosition()
    {
        if (Touch.activeTouches.Count > 0)
        {
            return Touch.activeTouches[0].screenPosition;
        }
        else
        {
            return Mouse.current?.position.ReadValue() ?? Vector2.zero;
        }
    }

    public bool IsDragging => _isDragging;

    private void ResetTouchInput()
    {
        _isDragging = false;
        _primaryTouchId = -1;
        _touchStartTime = 0f;
    }
}
