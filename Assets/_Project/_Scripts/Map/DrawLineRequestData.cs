using UnityEngine;
using XDPaint.Controllers.InputData;
using XDPaint.Core;

public class DrawLineRequestData
{
    public Ray StartRay { get; }
    public Vector3 StartPosition { get; }
    public Ray EndRay { get; }
    public Vector3 EndPosition { get; }
    public float Pressure { get; }
    public InputSource InputSource { get; }
    public Color Color { get; }
    public float BrushSize { get; }
    public int StartFingerId { get; }
    public int EndFingerId { get; }

    public DrawLineRequestData(Ray startRay, Vector3 startPosition, Ray endRay, Vector3 endPosition, float pressure, InputSource inputSource, int startFingerId, int endFingerId, Color color = default, float brushSize = 1f)
    {
        StartRay = startRay;
        StartPosition = startPosition;
        EndRay = endRay;
        EndPosition = endPosition;
        Pressure = pressure;
        InputSource = inputSource;
        StartFingerId = startFingerId;
        EndFingerId = endFingerId;
        Color = color;
        BrushSize = brushSize;
    }
}
