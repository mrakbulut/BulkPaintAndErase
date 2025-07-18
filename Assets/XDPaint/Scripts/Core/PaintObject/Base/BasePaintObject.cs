using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XDPaint.Controllers;
using XDPaint.Controllers.InputData;
using XDPaint.Core.Materials;
using XDPaint.Core.PaintObject.Data;
using XDPaint.Core.PaintObject.RaycastProcessor.Base;
using XDPaint.Core.PaintObject.LineProcessor;
using XDPaint.Core.PaintObject.LineProcessor.Base;
using XDPaint.Core.PaintObject.LineProcessor.Data;
using XDPaint.Tools.Image.Base;
using XDPaint.Tools.Raycast.Data;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace XDPaint.Core.PaintObject.Base
{
    [Serializable]
    public abstract class BasePaintObject : BasePaintObjectRenderer
    {
        #region Events

        /// <summary>
        /// Mouse hover event
        /// </summary>
        public event Action<PointerData> OnPointerHover;

        /// <summary>
        /// Mouse down event
        /// </summary>
        public event Action<PointerData> OnPointerDown;

        /// <summary>
        /// Mouse press event
        /// </summary>
        public event Action<PointerData> OnPointerPress;

        /// <summary>
        /// Mouse up event
        /// </summary>
        public event Action<PointerUpData> OnPointerUp;

        /// <summary>
        /// Draw point event, can be used by the developer to obtain data about painting
        /// </summary>
        public event Action<DrawPointData> OnDrawPoint;

        /// <summary>
        /// Draw line event, can be used by the developer to obtain data about painting
        /// </summary>
        public event Action<DrawLineData> OnDrawLine;

        #endregion

        #region Properties and variables

        public bool InBounds
        {
            get
            {
                foreach (var frameDataBuffer in frameContainer.Data)
                {
                    if (frameDataBuffer.Count == 0)
                    {
                        continue;
                    }

                    if (frameDataBuffer.GetFrameData(0).State.InBounds)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsPainting
        {
            get
            {
                foreach (var frameDataBuffer in frameContainer.Data)
                {
                    if (frameDataBuffer.Count == 0)
                    {
                        continue;
                    }

                    if (frameDataBuffer.GetFrameData(0).State.IsPainting)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsPainted { get; private set; }
        public bool ProcessInput = true;
        protected Transform ObjectTransform { get; private set; }
        protected IPaintManager PaintManager;

        private int HistoryLength => CanSmoothLines ? 4 : 2;

        private Vector3 RenderOffset
        {
            get
            {
                if (PaintData.Brush == null)
                {
                    return Vector3.zero;
                }

                var renderOffset = PaintData.Brush.RenderOffset;
                if (renderOffset.x > 0)
                {
                    renderOffset.x = Paint.SourceTexture.texelSize.x / 2f;
                }

                if (renderOffset.y > 0)
                {
                    renderOffset.y = Paint.SourceTexture.texelSize.y / 2f;
                }

                return renderOffset;
            }
        }

        private FrameDataContainer frameContainer;
        private PaintStateData[] statesData;
        private ILineProcessor lineProcessor;
        private IRaycastProcessor raycastProcessor;
        private BaseWorldData worldData;
        private bool clearTexture = true;
        private bool writeClear;

        #endregion

        #region Abstract methods

        public abstract bool CanSmoothLines { get; }
        public abstract Vector2 ConvertUVToTexturePosition(Vector2 uvPosition);
        public abstract Vector2 ConvertTextureToUVPosition(Vector2 texturePosition);
        protected abstract void Init();
        protected abstract bool IsInBounds(Ray ray);

        #endregion

        public void Init(IPaintManager paintManager, IPaintData paintData, Transform objectTransform, Paint paint)
        {
            PaintManager = paintManager;
            PaintData = paintData;
            ObjectTransform = objectTransform;
            Paint = paint;
            if (paintData.PaintSpace == PaintSpace.World)
            {
                worldData = new BaseWorldData();
            }

            InitRenderer(PaintManager, Paint);
            InitPaintStateData();
            InitStatesController();
            Init();
        }

        public override void DoDispose()
        {
            if (PaintData.StatesController != null)
            {
                PaintData.StatesController.OnRenderTextureAction -= OnExtraDraw;
                PaintData.StatesController.OnClearTextureAction -= OnClearTexture;
                PaintData.StatesController.OnResetState -= OnResetState;
            }

            frameContainer.DoDispose();
            statesData = null;
            worldData = null;
            base.DoDispose();
        }

        private void InitPaintStateData()
        {
            frameContainer = new FrameDataContainer(HistoryLength);
            statesData = new PaintStateData[InputController.Instance.MaxTouchesCount];
            for (int i = 0; i < statesData.Length; i++)
            {
                statesData[i] = new PaintStateData();
            }
        }

        private void InitStatesController()
        {
            if (PaintData.StatesController == null)
            {
                return;
            }

            PaintData.StatesController.OnRenderTextureAction += OnExtraDraw;
            PaintData.StatesController.OnClearTextureAction += OnClearTexture;
            PaintData.StatesController.OnResetState += OnResetState;
        }

        private void OnResetState()
        {
            clearTexture = true;
        }

        #region Input

        public void OnMouseHover(InputData inputData, RaycastData raycastData)
        {
            if (!IsPainting)
            {
                FrameData frameData;
                if (raycastData != null)
                {
                    frameData = new FrameData(inputData, raycastData, PaintData.Brush.Size);
                    frameContainer.Data[inputData.FingerId].AddFrameData(frameData);
                }
                else
                {
                    frameData = frameContainer.Data[inputData.FingerId].GetFrameData(0);
                }

                UpdatePaintData(frameData, true);
                if (OnPointerHover != null)
                {
                    var data = new PointerData(frameData.InputData, frameData.RaycastData, ConvertUVToTexturePosition(raycastData.UVHit));
                    OnPointerHover(data);
                }
            }
        }

        public void OnMouseHoverFailed(InputData inputData)
        {
            frameContainer.Data[inputData.FingerId].DoDispose();
        }

        public void OnMouseDown(InputData inputData, RaycastData raycastData)
        {
            OnMouse(inputData, raycastData, true);
        }

        public void OnMouseButton(InputData inputData, RaycastData raycastData)
        {
            OnMouse(inputData, raycastData, false);
        }

        private void OnMouse(InputData inputData, RaycastData raycastData, bool isDown)
        {
            if (isDown)
            {
                frameContainer.Data[inputData.FingerId].DoDispose();
            }

            var frameData = new FrameData(inputData, raycastData, PaintData.Brush.Size);
            frameContainer.Data[inputData.FingerId].AddFrameData(frameData);
            if (raycastData != null && raycastData.Triangle.Transform == ObjectTransform)
            {
                frameData.State.IsPainting = true;
                frameData.BrushSize = PaintData.Brush.Size;
                var paintState = statesData[frameData.InputData.FingerId];
                paintState.IsPainting = frameData.State.IsPainting;
                UpdatePaintData(frameData, false);
                if (frameData.RaycastData != null)
                {
                    frameData.State.IsPaintingPerformed = true;
                    paintState.IsPaintingPerformed = frameData.State.IsPaintingPerformed;
                    if (isDown)
                    {
                        if (OnPointerDown != null)
                        {
                            var data = new PointerData(frameData.InputData, frameData.RaycastData, ConvertUVToTexturePosition(raycastData.UVHit));
                            OnPointerDown.Invoke(data);
                        }
                    }
                    else
                    {
                        if (OnPointerPress != null)
                        {
                            var data = new PointerData(frameData.InputData, frameData.RaycastData, ConvertUVToTexturePosition(raycastData.UVHit));
                            OnPointerPress.Invoke(data);
                        }
                    }
                }
            }
        }

        public void OnMouseFailed(InputData inputData)
        {
            frameContainer.Data[inputData.FingerId].DoDispose();
        }

        public void OnMouseUp(InputData inputData)
        {
            FinishPainting(inputData.FingerId);
            if (OnPointerUp != null)
            {
                var data = new PointerUpData(inputData, IsInBounds(inputData.Ray));
                OnPointerUp.Invoke(data);
            }
        }

        public Vector2? GetTexturePosition(InputData inputData, RaycastData raycastData)
        {
            if (ObjectTransform == null)
            {
                Debug.LogError("ObjectForPainting has been destroyed!");
                return null;
            }

            var frameData = frameContainer.Data[inputData.FingerId].GetFrameData(0);
            UpdatePaintData(frameData, true);
            if (frameData.State.InBounds && raycastData != null)
            {
                return ConvertUVToTexturePosition(frameData.RaycastData.UVHit);
            }

            return null;
        }

        #endregion

        #region Drawing from code

        public PaintStateContainer SavePaintState(int fingerId = 0)
        {
            if (fingerId < 0 || fingerId >= frameContainer.Data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(fingerId));
            }

            var paintDataStorage = new PaintStateContainer(HistoryLength);
            if (frameContainer.Data[fingerId].Count > 0)
            {
                var dataStorage = paintDataStorage;
                for (int i = 0; i < frameContainer.Data[fingerId].Count; i++)
                {
                    var frameData = frameContainer.Data[fingerId].GetFrameData(i);
                    dataStorage.FrameBuffer.AddFrameData(frameData);
                }
            }

            paintDataStorage.PaintState.CopyFrom(statesData[fingerId]);
            return paintDataStorage;
        }

        public void RestorePaintState(PaintStateContainer paintContainerStorage, int fingerId = 0)
        {
            if (fingerId < 0 || fingerId >= frameContainer.Data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(fingerId));
            }

            if (paintContainerStorage.Equals(default(object)))
            {
                Debug.LogError("Saved states cannot be default!");
                return;
            }

            if (paintContainerStorage.FrameBuffer.Count > 0)
            {
                var frameData = frameContainer.Data[fingerId];
                frameData.DoDispose();
                for (int i = 0; i < paintContainerStorage.FrameBuffer.Count; i++)
                {
                    var data = paintContainerStorage.FrameBuffer.GetFrameData(i);
                    frameData.AddFrameData(data);
                }
            }

            statesData[fingerId].CopyFrom(paintContainerStorage.PaintState);
        }

        /// <summary>
        /// Draws a brush sample (point)
        /// </summary>
        /// <param name="texturePosition"></param>
        /// <param name="pressure"></param>
        /// <param name="fingerId"></param>
        /// <param name="onFinish"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void DrawPoint(Vector2 texturePosition, float pressure = 1f, int fingerId = 0, Action onFinish = null)
        {
            if (fingerId < 0 || fingerId >= frameContainer.Data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(fingerId));
            }

            if (PaintData.PaintSpace != PaintSpace.UV)
            {
                Debug.LogWarning("Paint Space is not UV!");
                return;
            }

            var state = SavePaintState(fingerId);
            frameContainer.Data[fingerId].DoDispose();
            var frameData = new FrameData(
                new InputData(fingerId, pressure),
                new RaycastData(null)
                {
                    UVHit = ConvertTextureToUVPosition(texturePosition)
                },
                PaintData.Brush.Size)
                {
                    State = new PaintStateData
                    {
                        InBounds = true,
                        IsPainting = true,
                        IsPaintingPerformed = true
                    }
                };

            statesData[fingerId].InBounds = true;
            statesData[fingerId].IsPainting = true;
            statesData[fingerId].IsPaintingPerformed = true;

            frameContainer.Data[fingerId].AddFrameData(frameData);
            IsPainted |= TryRenderPoint(fingerId);
            RenderToTextures();
            onFinish?.Invoke();
            frameContainer.Data[fingerId].DoDispose();
            RestorePaintState(state, fingerId);
        }

        /// <summary>
        /// Draws a brush sample (point)
        /// </summary>
        /// <param name="drawPointData"></param>
        /// <param name="onFinish"></param>
        public void DrawPoint(DrawPointData drawPointData, Action onFinish = null)
        {
            DrawPoint(drawPointData.InputData, drawPointData.RaycastData, onFinish);
        }

        /// <summary>
        /// Draws multiple brush samples (points) in a batch
        /// </summary>
        /// <param name="inputDataArray">Array of input data for each point</param>
        /// <param name="raycastDataArray">Array of raycast data for each point</param>
        /// <param name="fingerIds">Array of finger IDs for each point</param>
        /// <param name="onFinish">Callback when all points are processed</param>
        public void DrawPoints(InputData[] inputDataArray, RaycastData[] raycastDataArray, int[] fingerIds, Action onFinish = null)
        {
            if (inputDataArray == null || raycastDataArray == null || fingerIds == null)
            {
                Debug.LogWarning("BasePaintObject.DrawPoints: Null arrays provided");
                return;
            }

            if (inputDataArray.Length != raycastDataArray.Length || inputDataArray.Length != fingerIds.Length)
            {
                Debug.LogWarning("BasePaintObject.DrawPoints: Array lengths don't match");
                return;
            }

            if (inputDataArray.Length == 0)
            {
                onFinish?.Invoke();
                return;
            }

            // Use the first finger ID for state management
            int mainFingerId = fingerIds[0];
            var state = SavePaintState(mainFingerId);
            frameContainer.Data[mainFingerId].DoDispose();

            try
            {
                // Process all points in batch
                bool anyPainted = TryRenderPoints(inputDataArray, raycastDataArray, fingerIds);
                IsPainted |= anyPainted;

                // Render to textures only once at the end
                RenderToTextures();
                onFinish?.Invoke();
            }
            finally
            {
                frameContainer.Data[mainFingerId].DoDispose();
                RestorePaintState(state, mainFingerId);
            }
        }

        /// <summary>
        /// Draws multiple brush samples using DrawPointData array
        /// </summary>
        /// <param name="drawPointDataArray">Array of draw point data</param>
        /// <param name="fingerIds">Array of finger IDs for each point</param>
        /// <param name="onFinish">Callback when all points are processed</param>
        public void DrawPoints(DrawPointData[] drawPointDataArray, int[] fingerIds, Action onFinish = null)
        {
            if (drawPointDataArray == null || fingerIds == null)
            {
                Debug.LogWarning("BasePaintObject.DrawPoints: Null arrays provided");
                return;
            }

            var inputDataArray = new InputData[drawPointDataArray.Length];
            var raycastDataArray = new RaycastData[drawPointDataArray.Length];

            for (int i = 0; i < drawPointDataArray.Length; i++)
            {
                inputDataArray[i] = drawPointDataArray[i].InputData;
                raycastDataArray[i] = drawPointDataArray[i].RaycastData;
            }

            DrawPoints(inputDataArray, raycastDataArray, fingerIds, onFinish);
        }

        /// <summary>
        /// Draws a brush sample (point)
        /// </summary>
        /// <param name="inputData"></param>
        /// <param name="raycastData"></param>
        /// <param name="onFinish"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void DrawPoint(InputData inputData, RaycastData raycastData, Action onFinish = null)
        {
            if (inputData.FingerId < 0 || inputData.FingerId >= frameContainer.Data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(inputData.FingerId));
            }

            var state = SavePaintState(inputData.FingerId);
            frameContainer.Data[inputData.FingerId].DoDispose();
            var frameData = new FrameData(inputData, raycastData, PaintData.Brush.Size)
            {
                State = new PaintStateData
                {
                    InBounds = true,
                    IsPainting = true,
                    IsPaintingPerformed = true
                }
            };

            statesData[inputData.FingerId].InBounds = true;
            statesData[inputData.FingerId].IsPainting = true;
            statesData[inputData.FingerId].IsPaintingPerformed = true;

            frameContainer.Data[inputData.FingerId].AddFrameData(frameData);
            IsPainted |= TryRenderPoint(inputData.FingerId);
            RenderToTextures();
            onFinish?.Invoke();
            frameContainer.Data[inputData.FingerId].DoDispose();
            RestorePaintState(state, inputData.FingerId);
        }

        /// <summary>
        /// Draws a line with brush samples
        /// </summary>
        /// <param name="texturePositionStart"></param>
        /// <param name="texturePositionEnd"></param>
        /// <param name="pressureStart"></param>
        /// <param name="pressureEnd"></param>
        /// <param name="fingerId"></param>
        /// <param name="onFinish"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void DrawLine(Vector2 texturePositionStart, Vector2 texturePositionEnd, float pressureStart = 1f, float pressureEnd = 1f, int fingerId = 0, Action onFinish = null)
        {
            if (fingerId < 0 || fingerId >= frameContainer.Data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(fingerId));
            }

            if (PaintData.PaintSpace != PaintSpace.UV)
            {
                Debug.LogWarning("Paint Space is not UV!");
                return;
            }

            var state = SavePaintState(fingerId);
            frameContainer.Data[fingerId].DoDispose();
            var frameDataStart = new FrameData(new InputData(fingerId), null, PaintData.Brush.Size)
            {
                State = new PaintStateData
                {
                    InBounds = true,
                    IsPainting = true,
                    IsPaintingPerformed = true
                }
            };

            statesData[fingerId].InBounds = true;
            statesData[fingerId].IsPainting = true;
            statesData[fingerId].IsPaintingPerformed = true;
            frameContainer.Data[fingerId].AddFrameData(frameDataStart);

            var frameDataEnd = new FrameData(new InputData(fingerId), null, PaintData.Brush.Size)
            {
                State = new PaintStateData
                {
                    InBounds = true,
                    IsPainting = true,
                    IsPaintingPerformed = true
                }
            };

            frameContainer.Data[fingerId].AddFrameData(frameDataEnd);
            var texturePositions = new List<Vector2>(2)
            {
                texturePositionStart,
                texturePositionEnd
            };

            var brushes = new List<float>(2)
            {
                pressureStart * PaintData.Brush.Size,
                pressureEnd * PaintData.Brush.Size
            };

            LineDrawer.RenderLineUVInterpolated(texturePositions, RenderOffset, PaintData.Brush.RenderTexture, PaintData.Brush.Size, brushes, Tool.RandomizeLinesQuadsAngle);
            IsPainted = true;
            RenderToTextures();
            onFinish?.Invoke();
            frameContainer.Data[fingerId].DoDispose();
            RestorePaintState(state, fingerId);
        }

        /// <summary>
        /// Draws a line with brush samples
        /// </summary>
        /// <param name="drawLineData"></param>
        /// <param name="onFinish"></param>
        public void DrawLine(DrawLineData drawLineData, Action onFinish = null)
        {
            DrawLine(drawLineData.StartPointData.InputData, drawLineData.EndPointData.InputData,
                drawLineData.StartPointData.RaycastData, drawLineData.EndPointData.RaycastData, drawLineData.LineData, onFinish);
        }

        /// <summary>
        /// Draws a line with brush samples
        /// </summary>
        /// <param name="inputDataStart"></param>
        /// <param name="inputDataEnd"></param>
        /// <param name="raycastDataStart"></param>
        /// <param name="raycastDataEnd"></param>
        /// <param name="raycasts"></param>
        /// <param name="onFinish"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void DrawLine(InputData inputDataStart, InputData inputDataEnd, RaycastData raycastDataStart, RaycastData raycastDataEnd, KeyValuePair<Ray, RaycastData>[] raycasts = null, Action onFinish = null)
        {
            if (inputDataStart.FingerId < 0 || inputDataStart.FingerId >= frameContainer.Data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(inputDataStart.FingerId));
            }

            var state = SavePaintState(inputDataStart.FingerId);
            frameContainer.Data[inputDataStart.FingerId].DoDispose();
            var frameDataStart = new FrameData(inputDataStart, raycastDataStart, PaintData.Brush.Size)
            {
                State = new PaintStateData
                {
                    InBounds = true,
                    IsPainting = true,
                    IsPaintingPerformed = true
                }
            };

            statesData[inputDataStart.FingerId].InBounds = true;
            statesData[inputDataStart.FingerId].IsPainting = true;
            statesData[inputDataStart.FingerId].IsPaintingPerformed = true;
            frameContainer.Data[inputDataStart.FingerId].AddFrameData(frameDataStart);

            var frameDataEnd = new FrameData(inputDataEnd, raycastDataEnd, PaintData.Brush.Size)
            {
                State = new PaintStateData
                {
                    InBounds = true,
                    IsPainting = true,
                    IsPaintingPerformed = true
                }
            };
            frameContainer.Data[inputDataStart.FingerId].AddFrameData(frameDataEnd);

            if (Tool.Smoothing > 1)
            {
                frameContainer.Data[inputDataStart.FingerId].AddFrameData(frameDataEnd);
            }

            IsPainted |= TryRenderLine(inputDataStart.FingerId, false, raycasts);
            RenderToTextures();
            onFinish?.Invoke();
            frameContainer.Data[inputDataStart.FingerId].DoDispose();
            RestorePaintState(state, inputDataStart.FingerId);
        }

        #endregion

        public void FinishPainting(int fingerId = 0, bool forceFinish = false)
        {
            bool render = false;
            if (forceFinish)
            {
                render = true;
                RenderGeometries(true);
            }

            var frameData = frameContainer.Data[fingerId].GetFrameData(0);
            if (statesData[fingerId].IsPaintingPerformed || forceFinish)
            {
                if (PaintData.PaintMode.UsePaintInput)
                {
                    BakeInputToPaint();
                    ClearTexture(RenderTarget.Input);
                }

                if (frameData != null)
                {
                    frameData.State.IsPainting = false;
                    frameData.State.IsPaintingPerformed = false;
                }

                if ((statesData[fingerId].IsPaintingPerformed || forceFinish) && Tool.ProcessingFinished)
                {
                    SaveUndoTexture();
                }

                var paintState = statesData[fingerId];
                paintState.IsPainting = false;
                paintState.IsPaintingPerformed = false;

                frameData?.DoDispose();
                frameData = null;

                if (!PaintData.PaintMode.UsePaintInput)
                {
                    ClearTexture(RenderTarget.Input);
                    RenderToTextures();
                    render = false;
                }
            }

            if (render)
            {
                RenderToTextures();
            }

            Paint.SetPreviewVector(Vector4.zero);
            statesData[fingerId].DoDispose();
            frameData?.DoDispose();
        }

        /// <summary>
        /// Renders Points and Lines
        /// </summary>
        /// <param name="finishPainting"></param>
        public void RenderGeometries(bool finishPainting = false)
        {
            if (clearTexture)
            {
                ClearTexture(RenderTarget.Input);
                clearTexture = false;
                if (writeClear && Tool.RenderToTextures)
                {
                    SaveUndoTexture();
                    writeClear = false;
                }
            }

            IsPainted = false;
            for (int i = 0; i < frameContainer.Data.Length; i++)
            {
                if (frameContainer.Data[i].Count == 0)
                {
                    continue;
                }

                var frameData = frameContainer.Data[i].GetFrameData(0);
                if (IsPainting && (!Tool.ConsiderPreviousPosition || frameContainer.Data[i].Count == 1 || frameContainer.Data[i].Count > 1 &&
                        frameData.InputData.Position != frameContainer.Data[i].GetFrameData(1).InputData.Position &&
                        frameData.InputData.InputSource == frameContainer.Data[i].GetFrameData(1).InputData.InputSource) && Tool.AllowRender)
                {
                    if (frameContainer.Data[i].Count == 1 && frameData.RaycastData != null)
                    {
                        IsPainted |= TryRenderPoint(i);
                    }
                    else if (Tool.BaseSettings.CanPaintLines && AreRaycastDataValid(i, 2))
                    {
                        IsPainted |= TryRenderLine(i, finishPainting);
                    }
                }

                frameData.State.IsPaintingPerformed |= IsPainted;
                statesData[frameData.InputData.FingerId].IsPaintingPerformed |= frameData.State.IsPaintingPerformed;
            }
        }

        /// <summary>
        /// Combines textures, render preview
        /// </summary>
        public void RenderToTextures()
        {
            DrawPreProcess();
            ClearTexture(RenderTarget.Combined);
            DrawProcess();
        }

        public void RenderToTextureWithoutPreview(RenderTexture resultTexture)
        {
            DrawPreProcess();
            ClearTexture(RenderTarget.Combined);

            var boundsStack = new Stack<bool>();
            foreach (var buffer in frameContainer.Data)
            {
                for (int i = 0; i < buffer.Count; i++)
                {
                    boundsStack.Push(buffer.GetFrameData(i).State.InBounds);
                    buffer.GetFrameData(i).State.InBounds = false;
                }
            }

            DrawProcess();

            foreach (var buffer in frameContainer.Data)
            {
                for (int i = 0; i < buffer.Count; i++)
                {
                    buffer.GetFrameData(i).State.InBounds = boundsStack.Peek();
                }
            }

            Graphics.Blit(PaintData.TextureHelper.GetTexture(RenderTarget.Combined), resultTexture);
        }

        public void SaveUndoTexture()
        {
            PaintData.LayersController.ActiveLayer.SaveState();
        }

        public void SetRaycastProcessor(BaseRaycastProcessor processor)
        {
            raycastProcessor = processor;
        }

        /// <summary>
        /// Restores texture when Undo/Redo invoking
        /// </summary>
        private void OnExtraDraw()
        {
            if (!PaintData.PaintMode.UsePaintInput)
            {
                ClearTexture(RenderTarget.Input);
            }

            RenderToTextures();
        }

        private void OnClearTexture(RenderTexture renderTexture)
        {
            ClearTexture(renderTexture, Color.clear);
            RenderToTextures();
        }

        private void UpdatePaintData(FrameData frameData, bool updateBrushPreview)
        {
            frameData.State.InBounds = IsInBounds(frameData.InputData.Ray);
            var paintState = statesData[frameData.InputData.FingerId];
            paintState.InBounds |= frameData.State.InBounds;
            if (frameData.State.InBounds)
            {
                if (updateBrushPreview)
                {
                    UpdateBrushPreview(frameData);
                }
            }
        }

        private bool TryRenderPoint(int fingerId = 0)
        {
            var frameData = frameContainer.Data[fingerId].GetFrameData(0);
            if (frameData.RaycastData == null || raycastProcessor != null &&
                !raycastProcessor.TryProcessRaycastPosition(frameData.InputData.Ray, frameData.RaycastData, out _))
            {
                return false;
            }

            var texturePosition = ConvertUVToTexturePosition(frameData.RaycastData.UVHit);
            if (OnDrawPoint != null)
            {
                var data = new DrawPointData(frameData.InputData, frameData.RaycastData, texturePosition);
                OnDrawPoint.Invoke(data);
            }

            if (PaintData.PaintSpace == PaintSpace.UV)
            {
                UpdateQuadMesh(texturePosition, RenderOffset, PaintData.Brush.Size * frameData.InputData.Pressure, Tool.RandomizePointsQuadsAngle);
            }

            if (PaintData.PaintSpace == PaintSpace.World)
            {
                worldData.Positions[0] = ObjectTransform.TransformPoint(frameData.RaycastData.Hit);
                worldData.Rotations[0] = Tool.RandomizePointsQuadsAngle ? Random.value * 360f : 0f;
                worldData.Normals[0] = frameData.RaycastData.Triangle.WorldNormal;
                worldData.Count = 1;
                float[] brushSizes = new[] { PaintData.Brush.Size * frameData.InputData.Pressure, PaintData.Brush.Size * frameData.InputData.Pressure };
                SetPaintWorldProperties(worldData, frameData.InputData.Ray.origin, brushSizes);
            }

            RenderMesh();
            return true;
        }

        /// <summary>
        /// Renders multiple points in a batch
        /// </summary>
        /// <param name="inputDataArray">Array of input data for each point</param>
        /// <param name="raycastDataArray">Array of raycast data for each point</param>
        /// <param name="fingerIds">Array of finger IDs for each point</param>
        /// <returns>True if any point was rendered successfully</returns>
        private bool TryRenderPoints(InputData[] inputDataArray, RaycastData[] raycastDataArray, int[] fingerIds)
        {
            bool anyPainted = false;
            int mainFingerId = fingerIds[0];

            // For world space, we can batch up to the limit of Constants.MaxWorldDataPositions
            if (PaintData.PaintSpace == PaintSpace.World)
            {
                var validPositions = new List<Vector3>();
                var validRotations = new List<float>();
                var validNormals = new List<Vector3>();
                float commonBrushSize = PaintData.Brush.Size; // All brush sizes are the same

                // Process each point and collect valid world positions
                for (int i = 0; i < inputDataArray.Length; i++)
                {
                    var inputData = inputDataArray[i];
                    var raycastData = raycastDataArray[i];
                    int fingerId = fingerIds[i];

                    if (raycastData == null || raycastProcessor != null &&
                        !raycastProcessor.TryProcessRaycastPosition(inputData.Ray, raycastData, out _))
                    {
                        continue;
                    }

                    // Create frame data for event firing
                    float brushSize = commonBrushSize * inputData.Pressure;
                    var frameData = new FrameData(inputData, raycastData, brushSize);
                    frameContainer.Data[mainFingerId].AddFrameData(frameData);

                    var texturePosition = ConvertUVToTexturePosition(raycastData.UVHit);
                    if (OnDrawPoint != null)
                    {
                        var data = new DrawPointData(inputData, raycastData, texturePosition);
                        OnDrawPoint.Invoke(data);
                    }

                    // Collect world position data
                    validPositions.Add(ObjectTransform.TransformPoint(raycastData.Hit));
                    validRotations.Add(Tool.RandomizePointsQuadsAngle ? Random.value * 360f : 0f);
                    validNormals.Add(raycastData.Triangle.WorldNormal);

                    anyPainted = true;
                }

                // Render all valid positions in world space batch
                if (validPositions.Count > 0)
                {
                    // Limit to maximum supported positions
                    int maxPositions = Mathf.Min(validPositions.Count, 32); // Constants.MaxWorldDataPositions

                    for (int i = 0; i < maxPositions; i++)
                    {
                        worldData.Positions[i] = validPositions[i];
                        worldData.Rotations[i] = validRotations[i];
                        worldData.Normals[i] = validNormals[i];
                    }
                    worldData.Count = maxPositions;

                    // XDPaint shader expects maximum 2 brush sizes
                    float[] brushSizeArray = new float[2] { commonBrushSize, commonBrushSize };

                    SetPaintWorldProperties(worldData, inputDataArray[0].Ray.origin, brushSizeArray);
                    RenderMesh();
                }
            }
            else // UV Space - process each point individually
            {
                for (int i = 0; i < inputDataArray.Length; i++)
                {
                    var inputData = inputDataArray[i];
                    var raycastData = raycastDataArray[i];
                    int fingerId = fingerIds[i];

                    if (raycastData == null || raycastProcessor != null &&
                        !raycastProcessor.TryProcessRaycastPosition(inputData.Ray, raycastData, out _))
                    {
                        continue;
                    }

                    // Create frame data
                    float brushSize = PaintData.Brush.Size * inputData.Pressure;
                    var frameData = new FrameData(inputData, raycastData, brushSize);
                    frameContainer.Data[mainFingerId].AddFrameData(frameData);

                    var texturePosition = ConvertUVToTexturePosition(raycastData.UVHit);
                    if (OnDrawPoint != null)
                    {
                        var data = new DrawPointData(inputData, raycastData, texturePosition);
                        OnDrawPoint.Invoke(data);
                    }

                    UpdateQuadMesh(texturePosition, RenderOffset, brushSize, Tool.RandomizePointsQuadsAngle);
                    RenderMesh();
                    anyPainted = true;
                }
            }

            return anyPainted;
        }

        private bool TryRenderLine(int fingerId = 0, bool finishPainting = false, IList<KeyValuePair<Ray, RaycastData>> raycasts = null)
        {
            var frameData = frameContainer.Data[fingerId].GetFrameData(0);
            if (!CanSmoothLines || Tool.Smoothing == 1)
            {
                if (PaintData.PaintSpace == PaintSpace.UV)
                {
                    if (raycasts == null)
                    {
                        raycasts = GetRaycasts(!CanSmoothLines, 2, fingerId);
                    }

                    if (!(lineProcessor is LineUVProcessor))
                    {
                        lineProcessor = new LineUVProcessor(ConvertUVToTexturePosition);
                    }

                    if (lineProcessor.TryProcessLine(frameContainer.Data[fingerId], raycasts, finishPainting, out var linesData))
                    {
                        if (OnDrawLine != null)
                        {
                            var frameDataStart = frameContainer.Data[frameData.InputData.FingerId].GetFrameData(1);
                            var frameDataEnd = frameContainer.Data[frameData.InputData.FingerId].GetFrameData(0);
                            var data = new DrawLineData(
                                new DrawPointData(frameDataStart.InputData, frameDataStart.RaycastData, ConvertUVToTexturePosition(frameDataStart.RaycastData.UVHit)),
                                new DrawPointData(frameDataEnd.InputData, frameDataEnd.RaycastData, ConvertUVToTexturePosition(frameDataEnd.RaycastData.UVHit)), raycasts.ToArray());
                            OnDrawLine.Invoke(data);
                        }

                        var uvLineData = (TextureLineData)linesData[0];
                        if (CanSmoothLines)
                        {
                            LineDrawer.RenderLineUVInterpolated(uvLineData.TexturePositions, RenderOffset, PaintData.Brush.RenderTexture, PaintData.Brush.Size, uvLineData.Pressures, Tool.RandomizeLinesQuadsAngle);
                        }
                        else
                        {
                            LineDrawer.RenderLineUV(uvLineData.TexturePositions, RenderOffset, PaintData.Brush.RenderTexture, uvLineData.Pressures, Tool.RandomizeLinesQuadsAngle);
                        }

                        return true;
                    }

                    return false;
                }

                if (PaintData.PaintSpace == PaintSpace.World)
                {
                    if (raycasts == null)
                    {
                        raycasts = GetRaycasts(true, 2, fingerId);
                    }

                    if (!(lineProcessor is LineWorldProcessor))
                    {
                        lineProcessor = new LineWorldProcessor();
                    }

                    if (lineProcessor.TryProcessLine(frameContainer.Data[fingerId], raycasts, finishPainting, out var linesData))
                    {
                        if (OnDrawLine != null)
                        {
                            var frameDataStart = frameContainer.Data[frameData.InputData.FingerId].GetFrameData(1);
                            var frameDataEnd = frameContainer.Data[frameData.InputData.FingerId].GetFrameData(0);
                            var data = new DrawLineData(
                                new DrawPointData(frameDataStart.InputData, frameDataStart.RaycastData, ConvertUVToTexturePosition(frameDataStart.RaycastData.UVHit)),
                                new DrawPointData(frameDataEnd.InputData, frameDataEnd.RaycastData, ConvertUVToTexturePosition(frameDataEnd.RaycastData.UVHit)), raycasts.ToArray());
                            OnDrawLine.Invoke(data);
                        }

                        float[] brushSizes = new float[2] { PaintData.Brush.Size, PaintData.Brush.Size };
                        for (int i = 0; i < brushSizes.Length; i++)
                        {
                            if (frameContainer.Data[fingerId].GetFrameData(i).RaycastData == null)
                            {
                                break;
                            }

                            brushSizes[brushSizes.Length - i - 1] *= frameContainer.Data[fingerId].GetFrameData(i).InputData.Pressure;
                        }

                        foreach (var lineData in linesData)
                        {
                            var worldLineData = (WorldLineData)lineData;
                            for (int i = 0; i < worldLineData.Positions.Length; i++)
                            {
                                worldData.Positions[i] = worldLineData.Positions[i];
                                worldData.Normals[i] = worldLineData.Normals[i];
                            }

                            for (int i = 0; i < worldData.Rotations.Length; i++)
                            {
                                worldData.Rotations[i] = Tool.RandomizeLinesQuadsAngle ? Random.value * 360f : 0f;
                            }

                            worldData.Count = worldLineData.Count;
                            SetPaintWorldProperties(worldData, worldLineData.PointerPosition, brushSizes);
                            LineDrawer.RenderLineWorld();
                        }

                        return true;
                    }

                    return false;
                }
            }

            if (CanSmoothLines && AreRaycastDataValid(fingerId, 3))
            {
                if (raycasts == null)
                {
                    raycasts = GetRaycasts(false, 4, fingerId);
                }

                if (!(lineProcessor is LineSmoothUVProcessor))
                {
                    lineProcessor = new LineSmoothUVProcessor(ConvertUVToTexturePosition);
                }

                ((LineSmoothUVProcessor)lineProcessor).SetSmoothing(Tool.Smoothing);
                if (lineProcessor.TryProcessLine(frameContainer.Data[fingerId], raycasts, finishPainting, out var linesData))
                {
                    var textureLineSmoothData = (TextureSmoothLinesData)linesData[0];
                    foreach (var textureLineData in textureLineSmoothData.Data)
                    {
                        if (OnDrawLine != null)
                        {
                            const int lineElements = 3;
                            if (finishPainting)
                            {
                                var frameDataStart = frameContainer.Data[frameData.InputData.FingerId].GetFrameData(1);
                                var frameDataEnd = frameContainer.Data[frameData.InputData.FingerId].GetFrameData(0);
                                var data = new DrawLineData(
                                    new DrawPointData(frameDataStart.InputData, frameDataStart.RaycastData, ConvertUVToTexturePosition(frameDataStart.RaycastData.UVHit)),
                                    new DrawPointData(frameDataEnd.InputData, frameDataEnd.RaycastData, ConvertUVToTexturePosition(frameDataEnd.RaycastData.UVHit)),
                                    null);
                                OnDrawLine.Invoke(data);
                            }
                            else
                            {
                                var frameDataStart = textureLineData.TexturePositions.Length == lineElements
                                    ? frameContainer.Data[frameData.InputData.FingerId].GetFrameData(1)
                                    : frameContainer.Data[frameData.InputData.FingerId].GetFrameData(2);
                                var frameDataEnd = textureLineData.TexturePositions.Length == lineElements
                                    ? frameContainer.Data[frameData.InputData.FingerId].GetFrameData(0)
                                    : frameContainer.Data[frameData.InputData.FingerId].GetFrameData(1);
                                var data = new DrawLineData(
                                    new DrawPointData(frameDataStart.InputData, frameDataStart.RaycastData, ConvertUVToTexturePosition(frameDataStart.RaycastData.UVHit)),
                                    new DrawPointData(frameDataEnd.InputData, frameDataEnd.RaycastData, ConvertUVToTexturePosition(frameDataEnd.RaycastData.UVHit)),
                                    null);
                                OnDrawLine.Invoke(data);
                            }
                        }
                    }

                    foreach (var textureLineData in textureLineSmoothData.Data)
                    {
                        LineDrawer.RenderLineUVInterpolated(textureLineData.TexturePositions, RenderOffset, PaintData.Brush.RenderTexture, PaintData.Brush.Size, textureLineData.Pressures, Tool.RandomizeLinesQuadsAngle);
                    }

                    return true;
                }

                return false;
            }

            return false;
        }

        protected void UpdateBrushPreview(FrameData frameData)
        {
            if (PaintData.Brush.Preview && frameData.State.InBounds)
            {
                if (frameData.RaycastData != null)
                {
                    if (PaintData.PaintSpace == PaintSpace.UV)
                    {
                        var previewVector = GetPreviewVector();
                        Paint.SetPreviewVector(previewVector);
                    }
                    else if (PaintData.PaintSpace == PaintSpace.World)
                    {
                        worldData.Positions[0] = ObjectTransform.TransformPoint(frameData.RaycastData.Hit);
                        worldData.Rotations[0] = 0f;
                        worldData.Normals[0] = frameData.RaycastData.Triangle.WorldNormal;
                        float[] brushSizes = new[] { PaintData.Brush.Size * frameData.InputData.Pressure, PaintData.Brush.Size * frameData.InputData.Pressure };
                        if (raycastProcessor != null)
                        {
                            worldData.Count = raycastProcessor.TryProcessRaycastPosition(frameData.InputData.Ray, frameData.RaycastData, out _) ? 1 : 0;
                        }
                        else
                        {
                            worldData.Count = 1;
                        }

                        SetPaintWorldProperties(worldData, frameData.InputData.Ray.origin, brushSizes);
                    }
                }
                else
                {
                    if (PaintData.PaintSpace == PaintSpace.UV)
                    {
                        Paint.SetPreviewVector(Vector4.zero);
                    }
                    else if (PaintData.PaintSpace == PaintSpace.World)
                    {
                        worldData.Count = 0;
                        SetPaintWorldCount(worldData.Count);
                    }
                }
            }

            return;

            Vector4 GetPreviewVector()
            {
                var brushRatio = new Vector2(
                    Paint.SourceTexture.width / (float)PaintData.Brush.RenderTexture.width,
                    Paint.SourceTexture.height / (float)PaintData.Brush.RenderTexture.height) / PaintData.Brush.Size / frameData.InputData.Pressure;
                var texturePosition = frameData.RaycastData.UVHit * new Vector2(Paint.SourceTexture.width, Paint.SourceTexture.height);
                var brushOffset = new Vector4(
                    texturePosition.x / Paint.SourceTexture.width * brushRatio.x + PaintData.Brush.RenderOffset.x,
                    texturePosition.y / Paint.SourceTexture.height * brushRatio.y + PaintData.Brush.RenderOffset.y,
                    brushRatio.x, brushRatio.y);
                return brushOffset;
            }
        }

        private IList<KeyValuePair<Ray, RaycastData>> GetRaycasts(bool raycast, int count, int fingerId = 0)
        {
            var raycasts = new List<KeyValuePair<Ray, RaycastData>>();
            if (frameContainer.Data[fingerId].Count >= 2 && raycast)
            {
                var raycastsData = new RaycastData[2];
                float averageBrushSize = 0f;
                var frameData = frameContainer.Data[fingerId];
                for (int i = 0; i < 2; i++)
                {
                    var data = frameData.GetFrameData(i);
                    if (data.RaycastData == null)
                    {
                        break;
                    }

                    raycastsData[i] = data.RaycastData;
                    averageBrushSize += data.InputData.Pressure * PaintData.Brush.Size;
                }

                averageBrushSize /= 2f;
                raycasts = LineDrawer.GetLineRaycasts(raycastsData[1], raycastsData[0], frameContainer.Data[fingerId].GetFrameData(0).InputData.Ray.origin, averageBrushSize, fingerId);
            }
            else
            {
                count = Mathf.Min(count, frameContainer.Data[fingerId].Count);
                for (int i = 0; i < count; i++)
                {
                    var data = frameContainer.Data[fingerId].GetFrameData(i);
                    if (data.RaycastData == null)
                    {
                        break;
                    }

                    raycasts.Add(new KeyValuePair<Ray, RaycastData>(data.InputData.Ray, data.RaycastData));
                }
            }

            if (raycastProcessor != null)
            {
                for (int i = raycasts.Count - 1; i >= 0; i--)
                {
                    var pair = raycasts[i];
                    if (!raycastProcessor.TryProcessRaycastPosition(pair.Key, pair.Value, out _))
                    {
                        raycasts.RemoveAt(i);
                    }
                }
            }

            return raycasts;
        }

        private bool AreRaycastDataValid(int fingerId, int frames)
        {
            if (frameContainer.Data[fingerId].Count < frames)
            {
                return false;
            }

            for (int i = 0; i < frames; i++)
            {
                if (frameContainer.Data[fingerId].GetFrameData(i).RaycastData == null)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
