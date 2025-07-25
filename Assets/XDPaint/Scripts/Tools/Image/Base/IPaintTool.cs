using UnityEngine;
using UnityEngine.Rendering;
using XDPaint.Core;
using XDPaint.Core.Materials;
using XDPaint.Core.PaintModes;
using XDPaint.Core.PaintObject.Data;
using IDisposable = XDPaint.Core.IDisposable;

namespace XDPaint.Tools.Image.Base
{
    public interface IPaintTool : IDisposable
    {
        PaintTool Type { get; }
        bool AllowRender { get; }
        bool CanDrawLines { get; }
        bool ConsiderPreviousPosition { get; }
        bool ShowPreview { get; }
        int Smoothing { get; }
        bool RandomizePointsQuadsAngle { get; }
        bool RandomizeLinesQuadsAngle { get; }
        bool RenderToLayer { get; }
        bool RenderToInput { get; }
        bool RenderToTextures { get; }
        bool DrawPreProcess { get; }
        bool DrawProcess { get; }
        bool DrawPostProcess { get; }
        bool BakeInputToLayer { get; }
        bool ProcessingFinished { get; }
        BasePaintToolSettings BaseSettings { get; }

        void FillWithColor(Color color);
        void SetPaintMode(IPaintMode mode);
        void OnBrushChanged(IBrush brush);
        void OnDrawPreProcess(RenderTargetIdentifier combined);
        void OnDrawProcess(RenderTargetIdentifier combined);
        void OnDrawPostProcess(RenderTargetIdentifier combined);
        void OnRenderPreview(RenderTargetIdentifier combined);
        void OnBakeInputToLayer(RenderTargetIdentifier activeLayer);
        void Enter();
        void Exit();
        void UpdateHover(PointerData pointerData);
        void UpdateDown(PointerData pointerData);
        void UpdatePress(PointerData pointerData);
        void UpdateUp(PointerUpData pointerUpData);
    }
}