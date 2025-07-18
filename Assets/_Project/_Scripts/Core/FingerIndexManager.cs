using System.Collections.Generic;
using UnityEngine;

public class FingerIndexManager : MonoBehaviour
{
    public static FingerIndexManager Instance { get; private set; }

    [Header("Finger Index Settings")]
    [SerializeField] private int _startingFingerIndex;
    [SerializeField] private int _maxFingerIndex = 999;

    private readonly Queue<int> _availableFingerIndexes = new Queue<int>();
    private readonly HashSet<int> _usedFingerIndexes = new HashSet<int>();
    private int _nextFingerIndex;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        _nextFingerIndex = _startingFingerIndex;
        _availableFingerIndexes.Clear();
        _usedFingerIndexes.Clear();
    }

    public int GetNextFingerIndex()
    {
        int fingerIndex;

        // Try to reuse a released finger index first
        if (_availableFingerIndexes.Count > 0)
        {
            fingerIndex = _availableFingerIndexes.Dequeue();
        }
        else
        {
            // Generate a new finger index
            fingerIndex = _nextFingerIndex;
            _nextFingerIndex++;

            // Wrap around if we exceed max index
            if (_nextFingerIndex > _maxFingerIndex)
            {
                _nextFingerIndex = _startingFingerIndex;
            }
        }

        _usedFingerIndexes.Add(fingerIndex);
        return fingerIndex;
    }

    public void ReleaseFingerIndexes(params int[] fingerIndexes)
    {
        for (int i = 0; i < fingerIndexes.Length; i++)
        {
            ReleaseFingerIndex(fingerIndexes[i]);
        }
    }

    public void ReleaseFingerIndex(int fingerIndex)
    {
        if (_usedFingerIndexes.Contains(fingerIndex))
        {
            _usedFingerIndexes.Remove(fingerIndex);
            _availableFingerIndexes.Enqueue(fingerIndex);
        }
    }

    public int GetUsedFingerIndexCount()
    {
        return _usedFingerIndexes.Count;
    }

    public int GetAvailableFingerIndexCount()
    {
        return _availableFingerIndexes.Count;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // Debug method to show current state
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogFingerIndexState()
    {
        Debug.Log($"FingerIndexManager State - Used: {_usedFingerIndexes.Count}, Available: {_availableFingerIndexes.Count}, Next: {_nextFingerIndex}");
    }
}
