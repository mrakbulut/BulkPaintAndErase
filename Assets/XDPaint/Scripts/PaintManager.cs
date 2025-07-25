﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using XDPaint.Controllers;
using XDPaint.Controllers.InputData;
using XDPaint.Controllers.InputData.Base;
using XDPaint.Core;
using XDPaint.Core.Layers;
using XDPaint.Core.Materials;
using XDPaint.Core.PaintModes;
using XDPaint.Core.PaintObject;
using XDPaint.Core.PaintObject.Base;
using XDPaint.Core.PaintObject.RaycastProcessor;
using XDPaint.States;
using XDPaint.Tools;
using XDPaint.Tools.Image.Base;
using XDPaint.Tools.Layers;
using XDPaint.Tools.Raycast;
using XDPaint.Tools.Triangles;
using XDPaint.Utils;

namespace XDPaint
{
    public class PaintManager : MonoBehaviour, IPaintManager
    {
        #region Events

        public event Action<PaintManager> OnInitialized;
        public event Action OnDisposed;

        #endregion
        
        #region Properties and variables

        [SerializeField] private GameObject objectForPainting;
        public GameObject ObjectForPainting
        {
            get => objectForPainting;
            set => objectForPainting = value;
        }

        [SerializeField] private Paint material = new Paint();
        public Paint Material => material;

        [SerializeField] private LayersController layersController;
        public ILayersController LayersController => layersController;
        
        [SerializeField] private LayersContainer layersContainer;
        public LayersContainer LayersContainer
        {
            get => layersContainer;
            set => layersContainer = value;
        }

        public BasePaintObject PaintObject { get; private set; }

        private StatesController statesController;
        public IStatesController StatesController => statesController;
        
        [SerializeField] private PaintSpace paintSpace;
        public PaintSpace PaintSpace
        {
            get => paintSpace;
            set => paintSpace = value;
        }

        [SerializeField] private PaintMode paintModeType;
        [SerializeField] private FilterMode filterMode = FilterMode.Bilinear;
        public FilterMode FilterMode
        {
            get => filterMode;
            set
            {
                filterMode = value;
                if (initialized)
                {
                    layersController.SetFilterMode(filterMode);
                }
            }
        }

        [NonSerialized] private IBrush currentBrush;
        [SerializeField] private Brush brush = new Brush();
        public IBrush Brush
        {
            get
            {
                if (Application.isPlaying)
                {
                    return currentBrush;
                }
                return brush;
            }
            set
            {
                if (Application.isPlaying)
                {
                    currentBrush = value;
                    currentBrush.Init(PaintMode);
                    toolsManager.CurrentTool.OnBrushChanged(currentBrush);
                }
                else
                {
                    brush = (Brush)value;
                }
                
                if (initialized)
                {
                    Material.SetPreviewTexture(currentBrush.RenderTexture);
                }
            }
        }
        
        [SerializeField] private ToolsManager toolsManager;
        public ToolsManager ToolsManager => toolsManager;

        [SerializeField] private PaintTool paintTool;
        public PaintTool Tool
        {
            get
            {
                if (toolsManager.CurrentTool != null)
                {
                    return toolsManager.CurrentTool.Type;
                }
                return PaintTool.Brush;
            } 
            set
            {
                paintTool = value;
                if (initialized)
                {
                    Material.SetPreviewTexture(currentBrush.RenderTexture);
                    if (toolsManager != null)
                    {
                        toolsManager.SetTool(paintTool);
                        toolsManager.CurrentTool.SetPaintMode(PaintMode);
                    }
                }
            }
        }
        
        [SerializeField] private bool copySourceTextureToLayer = true;
        public bool CopySourceTextureToLayer
        {
            get => copySourceTextureToLayer;
            set => copySourceTextureToLayer = value;
        }

        [SerializeField] private bool useSourceTextureAsBackground;
        public bool UseSourceTextureAsBackground
        {
            get => useSourceTextureAsBackground;
            set => useSourceTextureAsBackground = value;
        }
        
        [NonSerialized] private Triangle[] triangles;
        public Triangle[] Triangles => triangles;

        [SerializeField] private int subMesh;
        public int SubMesh
        {
            get => subMesh;
            set => subMesh = value;
        }
        
        [SerializeField] private int uvChannel;
        public int UVChannel
        {
            get => uvChannel;
            set => uvChannel = value;
        }

        private ObjectComponentType componentType;
        public ObjectComponentType ComponentType => componentType;
        
        private IPaintMode paintMode;
        public IPaintMode PaintMode
        {
            get
            {
                if (initialized && Application.isPlaying)
                {
                    paintMode = PaintController.Instance.GetPaintMode(PaintController.Instance.UseSharedSettings ? PaintController.Instance.PaintMode : paintModeType);
                }
                return paintMode;
            }            
        }

        public bool Initialized => initialized;

        private LayersMergeController layersMergeController;
        private IRenderTextureHelper renderTextureHelper;
        private IRenderComponentsHelper renderComponentsHelper;
        private IPaintData paintData;
        private BaseInputData inputData;
        private LayersContainer loadedLayersContainer;
        private bool initialized;
        
        private string DefaultPath => Path.Combine(Application.persistentDataPath, "XDPaint");
        private const string FilenameFormat = "{0}_LayersContainer.json";
        private const string TextureFilenameFormat = "_{0}.png";
        private const string MaskFilenameFormat = "_{0}_Mask.png";
        
        #endregion
        
        private void Start()
        {
            if (initialized)
                return;
            
            Init();
        }

        private void LateUpdate()
        {
            if (!initialized)
                return;

            if (PaintObject.IsPainting || currentBrush.Preview)
            {
                Render();
            }
        }

        private void OnDestroy()
        {
            DoDispose();
        }

        public void Init()
        {
            if (initialized)
                DoDispose();
            
            initialized = false;
            if (ObjectForPainting == null)
            {
                Debug.LogError("ObjectForPainting is null!");
                return;
            }

            RestoreSourceMaterialTexture();

            InitRenderComponentsHelper();
            if (componentType == ObjectComponentType.Unknown)
            {
                Debug.LogError("Unknown component type!");
                return;
            }

            if (ControllersContainer.Instance == null)
            {
                var containerGameObject = new GameObject(Settings.Instance.ContainerGameObjectName);
                containerGameObject.AddComponent<ControllersContainer>();
            }

            if (triangles == null || triangles.Length == 0)
            {
                if (renderComponentsHelper.IsMesh())
                {
                    var mesh = renderComponentsHelper.GetMesh(this);
                    if (mesh != null)
                    {
                        triangles = TrianglesData.GetData(mesh, subMesh, uvChannel);
                    }
                }
                else
                {
                    triangles = TrianglesData.GetPlaneData();
                }

                if (triangles == null || triangles.Length == 0)
                {
                    Debug.LogError("Can't get triangles data!");
                    return;
                }
            }

            var paintComponent = renderComponentsHelper.PaintComponent;
            var renderComponent = renderComponentsHelper.RendererComponent;
            RaycastController.Instance.InitObject(this, paintComponent, renderComponent);

            InitRenderTextures();
            InitLayers();
            InitStates();
            CreateLayers();
            InitMaterial();

            PaintController.Instance.RegisterPaintManager(this);
            InitBrush();
            InitPaintObject();
            InitTools();

            SubscribeInputEvents();
            initialized = true;
            Render();

            OnInitialized?.Invoke(this);
        }

        public void DoDispose()
        {
            if (!initialized)
                return;
            
            //unregister PaintManager
            PaintController.Instance.UnRegisterPaintManager(this);
            //restore source material and texture
            RestoreSourceMaterialTexture();
            //free tools resources
            toolsManager.OnToolChanged -= OnToolChanged;
            toolsManager.DoDispose();
            paintData.DoDispose();
            
            //free brush resources
            if (Brush != null)
            {
                Brush.OnTextureChanged -= Material.SetPreviewTexture;
                Brush.OnPreviewChanged -= UpdatePreviewInput;
            }

            if (brush != null)
            {
                brush.DoDispose();
            }
            
            //destroy created material
            Material.DoDispose();
            //free RenderTextures
            renderTextureHelper.DoDispose();
            if (statesController != null)
            {
                layersController.OnLayersCollectionChanged -= statesController.AddState;
                statesController.OnUndo -= TryRender;
                statesController.OnRedo -= TryRender;
            }
            else
            {
                layersController.OnLayersCollectionChanged -= OnLayersCollectionChanged;
            }
            
            layersController.DoDispose();
            UnloadLayersContainer();
            statesController?.DoDispose();
            //destroy raycast data
            if (renderComponentsHelper.IsMesh())
            {
                RaycastController.Instance.DestroyMeshData(this);
            }
            
            //unsubscribe input events
            UnsubscribeInputEvents();
            inputData.DoDispose();
            //free undo/redo RenderTextures and meshes
            PaintObject.DoDispose();
            initialized = false;
            OnDisposed?.Invoke();
        }
        
        public bool IsActive()
        {
            return enabled && gameObject.activeInHierarchy && ObjectForPainting != null && PaintObject != null && PaintObject.ProcessInput;
        }

        public void Render()
        {
            if (initialized)
            {
                PaintObject.RenderGeometries();
                PaintObject.RenderToTextures();
            }
        }
        
        public void SetPaintMode(PaintMode mode)
        {
            paintModeType = mode;
            if (Application.isPlaying)
            {
                paintMode = PaintController.Instance.GetPaintMode(PaintController.Instance.UseSharedSettings ? PaintController.Instance.PaintMode : paintModeType);
                toolsManager.CurrentTool.SetPaintMode(paintMode);
                Material.SetPreviewTexture(currentBrush.RenderTexture);
            }
        }

        public RenderTexture GetPaintTexture()
        {
            return layersController.ActiveLayer.RenderTexture;
        }

        public RenderTexture GetPaintInputTexture()
        {
            return renderTextureHelper.GetTexture(RenderTarget.Input);
        }

        public RenderTexture GetResultRenderTexture()
        {
            return renderTextureHelper.GetTexture(RenderTarget.Combined);
        }

        /// <summary>
        /// Returns result texture
        /// </summary>
        /// <param name="hideBrushPreview">Whether to hide brush preview</param>
        /// <returns></returns>
        public Texture2D GetResultTexture(bool hideBrushPreview = false)
        {
            var needToHideBrushPreview = hideBrushPreview && currentBrush.Preview;
            if (needToHideBrushPreview)
            {
                currentBrush.Preview = false;
                Render();
            }

            RenderTexture temp = null;
            var renderTexture = renderTextureHelper.GetTexture(RenderTarget.Combined);
            if (renderComponentsHelper.ComponentType == ObjectComponentType.SpriteRenderer)
            {
                var spriteRenderer = renderComponentsHelper.RendererComponent as SpriteRenderer;
                if (spriteRenderer != null && spriteRenderer.material != null &&
                    spriteRenderer.material.shader == Settings.Instance.SpriteMaskShader)
                {
                    temp = RenderTextureFactory.CreateTemporaryRenderTexture(renderTexture, false);
                    var rti = new RenderTargetIdentifier(temp);
                    var commandBufferBuilder = new CommandBufferBuilder("ResultTexture");
                    commandBufferBuilder.LoadOrtho().Clear().SetRenderTarget(rti).ClearRenderTarget().Execute();
                    Graphics.Blit(spriteRenderer.material.mainTexture, temp, spriteRenderer.material);
                    commandBufferBuilder.Release();
                }
            }

            var resultTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);
            var previousRenderTexture = RenderTexture.active;
            RenderTexture.active = temp != null ? temp : renderTexture;
            resultTexture.ReadPixels(new Rect(0, 0, resultTexture.width, resultTexture.height), 0, 0, false);
            resultTexture.Apply();
            RenderTexture.active = previousRenderTexture;
            if (temp != null)
            {
                RenderTexture.ReleaseTemporary(temp);
            }

            if (needToHideBrushPreview)
            {
                currentBrush.Preview = true;
            }

            return resultTexture;
        }

        /// <summary>
        /// Set Layers data from LayersContainer
        /// </summary>
        /// <param name="container"></param>
        public void SetLayersData(LayersContainer container)
        {
            foreach (var layerData in container.LayersData)
            {
                ILayer layer;
                if (layerData.SourceTexture == null)
                {
                    layer = layersController.AddNewLayer(layerData.Name);
                }
                else
                {
                    layer = layersController.AddNewLayer(layerData.Name, layerData.SourceTexture);
                }
                
                layer.Enabled = layerData.IsEnabled;
                layer.MaskEnabled = layerData.IsMaskEnabled;
                layer.Opacity = layerData.Opacity;
                layer.BlendingMode = layerData.BlendingMode;
                if (layerData.Mask != null)
                {
                    layersController.AddLayerMask(layer);
                    Graphics.Blit(layerData.Mask, layer.MaskRenderTexture);
                }
            }
            
            layersController.SetActiveLayer(container.ActiveLayerIndex);
            statesController.SetMinUndoStatesCount(statesController.GetUndoActionsCount());
        }

        /// <summary>
        /// Returns Layers data
        /// </summary>
        /// <returns></returns>
        public LayerData[] GetLayersData()
        {
            var layersData = new LayerData[layersController.Layers.Count];
            for (var i = 0; i < layersController.Layers.Count; i++)
            {
                var layer = layersController.Layers[i];
                layersData[i] = new LayerData(layer);
            }
            return layersData;
        }
        
        public bool IsLayersContainerExists(string filename)
        {
            var path = Path.Combine(DefaultPath, string.Format(FilenameFormat, filename));
            return path.IsCorrectFilename(true) && File.Exists(path);
        }
        
        public bool SaveToLayersContainer(string filename)
        {
            if (!initialized)
                return false;
            
            if (!filename.IsCorrectFilename(true))
                return false;
            
            var container = ScriptableObject.CreateInstance<LayersContainer>();
            container.ActiveLayerIndex = LayersController.ActiveLayerIndex;
            container.LayersData = GetLayersData();
            
            foreach (var layerData in container.LayersData)
            {
                if (!layerData.Name.IsCorrectFilename(true))
                    return false;
            }

            try
            {
                if (!Directory.Exists(DefaultPath))
                {
                    Directory.CreateDirectory(DefaultPath);
                }

                var directory = Path.Combine(DefaultPath, filename);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            
                for (var i = 0; i < container.LayersData.Length; i++)
                {
                    var layerData = container.LayersData[i];
                    if (layerData.Texture != null)
                    {
                        var texture2D = layerData.Texture;
                        var pngData = texture2D.EncodeToPNG();
                        if (pngData != null)
                        {
                            var filePath = Path.Combine(DefaultPath, Path.Combine(filename, layerData.Name + string.Format(TextureFilenameFormat, i)));
                            File.WriteAllBytes(filePath, pngData);
                        }
                        Destroy(texture2D);
                    }

                    if (layerData.Mask != null)
                    {
                        var texture2D = layerData.Mask;
                        var pngData = texture2D.EncodeToPNG();
                        if (pngData != null)
                        {
                            var filePath = Path.Combine(DefaultPath, Path.Combine(filename, layerData.Name + string.Format(MaskFilenameFormat, i)));
                            File.WriteAllBytes(filePath, pngData);
                        }
                        Destroy(texture2D);
                    }
                }

                var json = JsonUtility.ToJson(container);
                var path = Path.Combine(DefaultPath, string.Format(FilenameFormat, filename));
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return false;
            }
            
            return true;
        }

        public bool DeleteLayersContainer(string filename)
        {
            if (!filename.IsCorrectFilename(true))
                return false;

            var jsonPath = Path.Combine(DefaultPath, string.Format(FilenameFormat, filename));
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"Can't find file at path {jsonPath}");
                return false;
            }
            
            var json = File.ReadAllText(jsonPath);
            var container = ScriptableObject.CreateInstance<LayersContainer>();
            JsonUtility.FromJsonOverwrite(json, container);

            foreach (var layerData in container.LayersData)
            {
                if (!layerData.Name.IsCorrectFilename(true))
                    return false;
            }

            try
            {
                var directory = Path.Combine(DefaultPath, filename);
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return false;
            }
            finally
            {
                Destroy(container);
                File.Delete(jsonPath);
            }

            return true;
        }

        public bool LoadFromLayersContainer(string filename)
        {
            if (!filename.IsCorrectFilename(true))
                return false;
            
            var jsonPath = Path.Combine(DefaultPath, string.Format(FilenameFormat, filename));
            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"Can't find file at path {jsonPath}");
                return false;
            }
            
            DoDispose();
            
            var json = File.ReadAllText(jsonPath);
            var container = ScriptableObject.CreateInstance<LayersContainer>();
            JsonUtility.FromJsonOverwrite(json, container);
            loadedLayersContainer = container;

            try
            {
                for (var i = 0; i < loadedLayersContainer.LayersData.Length; i++)
                {
                    var layerData = container.LayersData[i];
                    var texturePath = Path.Combine(DefaultPath,
                        Path.Combine(filename, layerData.Name + string.Format(TextureFilenameFormat, i)));
                    var textureData = File.ReadAllBytes(texturePath);
                    var layerTexture = new Texture2D(1, 1);
                    layerTexture.LoadImage(textureData);
                    layerData.SourceTexture = layerTexture;
                    layerData.Texture = layerTexture;
                    var maskPath = Path.Combine(DefaultPath,
                        Path.Combine(filename, layerData.Name + string.Format(MaskFilenameFormat, i)));
                    if (File.Exists(maskPath))
                    {
                        var maskData = File.ReadAllBytes(maskPath);
                        var maskTexture = new Texture2D(1, 1);
                        maskTexture.LoadImage(maskData);
                        layerData.Mask = maskTexture;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return false;
            }
            finally
            {
                LayersContainer = container;
                Init();
            }

            return true;
        }
        
        public void InitBrush()
        {
            if (PaintController.Instance.UseSharedSettings)
            {
                currentBrush = PaintController.Instance.Brush;
            }
            else
            {
                if (currentBrush != null)
                {
                    currentBrush.OnTextureChanged -= Material.SetPreviewTexture;
                }
                
                currentBrush = brush;
                currentBrush.Init(PaintMode);
            }
            
            Material.SetPreviewTexture(currentBrush.RenderTexture);
            Brush.OnTextureChanged -= Material.SetPreviewTexture;
            Brush.OnTextureChanged += Material.SetPreviewTexture;
            Brush.OnPreviewChanged -= UpdatePreviewInput;
            Brush.OnPreviewChanged += UpdatePreviewInput;
        }

        public Ray GetRay(Vector3 screenPosition)
        {
            return inputData?.GetRay(screenPosition) ?? PaintController.Instance.Camera.ScreenPointToRay(screenPosition);
        }

        private void UnloadLayersContainer()
        {
            if (loadedLayersContainer != null)
            {
                foreach (var layerData in loadedLayersContainer.LayersData)
                {
                    if (layerData.SourceTexture != null)
                    {
                        Destroy(layerData.SourceTexture);
                    }

                    if (layerData.Texture != null)
                    {
                        Destroy(layerData.Texture);
                    }
                    
                    if (layerData.Mask != null)
                    {
                        Destroy(layerData.Mask);
                    }
                }

                if (layersContainer == loadedLayersContainer)
                {
                    layersContainer = null;
                }

                loadedLayersContainer.LayersData = null;
                loadedLayersContainer = null;
            }
        }

        /// <summary>
        /// Restore source material and texture
        /// </summary>
        private void RestoreSourceMaterialTexture()
        {
            if (initialized && Material.SourceMaterial != null)
            {
                if (Material.SourceMaterial.GetTexture(Material.ShaderTextureName) == null)
                {
                    Material.SourceMaterial.SetTexture(Material.ShaderTextureName, Material.SourceTexture);
                }
                renderComponentsHelper.SetSourceMaterial(Material.SourceMaterial, Material.MaterialIndex);
            }
        }

        private void InitRenderComponentsHelper()
        {
            if (renderComponentsHelper == null)
            {
                renderComponentsHelper = new RenderComponentsHelper();
            }
            renderComponentsHelper.Init(ObjectForPainting, out componentType);
        }
        
        private void InitRenderTextures()
        {
            paintMode = PaintController.Instance.GetPaintMode(PaintController.Instance.UseSharedSettings ? PaintController.Instance.PaintMode : paintModeType);
            if (renderTextureHelper == null)
            {
                renderTextureHelper = new RenderTextureHelper();
            }
            
            var sourceTexture = layersContainer != null ? layersContainer.LayersData[0].SourceTexture : null;
            Material.Init(renderComponentsHelper, sourceTexture);
            renderTextureHelper.Init(new Vector2Int(Material.SourceTexture.width, Material.SourceTexture.height), filterMode);
            if (PaintSpace == PaintSpace.World)
            {
                Material.CreateWorldMaterial();
                if (renderComponentsHelper.IsMesh())
                {
                    renderTextureHelper.CreateRenderTexture(RenderTarget.Unwrapped, RenderTextureFormat.R8);
                }
            }
        }

        private void InitLayers()
        {
            layersMergeController = new LayersMergeController();
            layersController?.DoDispose();
            layersController = new LayersController(layersMergeController);
            layersController.Init(Material.SourceTexture.width, Material.SourceTexture.height);
            layersController.SetFilterMode(filterMode);
        }

        private void InitMaterial()
        {
            if (Material.SourceTexture != null)
            {
                Graphics.Blit(Material.SourceTexture, renderTextureHelper.GetTexture(RenderTarget.Combined));
            }

            Material.SetObjectMaterialTexture(renderTextureHelper.GetTexture(RenderTarget.Combined));
            Material.SetPaintTexture(layersController.ActiveLayer.RenderTexture);
            Material.SetInputTexture(renderTextureHelper.GetTexture(RenderTarget.Input));
        }

        private void InitStates()
        {
            if (StatesSettings.Instance.UndoRedoEnabled)
            {
                statesController = new StatesController();
                statesController.Init(layersController);
                layersController.SetStateController(statesController);
                layersController.OnLayersCollectionChanged -= statesController.AddState;
                layersController.OnLayersCollectionChanged += statesController.AddState;
                statesController.OnUndo -= TryRender;
                statesController.OnUndo += TryRender;
                statesController.OnRedo -= TryRender;
                statesController.OnRedo += TryRender;
                statesController.Enable();
            }
            else
            {
                layersController.OnLayersCollectionChanged -= OnLayersCollectionChanged;
                layersController.OnLayersCollectionChanged += OnLayersCollectionChanged;
            }
        }

        private void CreateLayers()
        {
            if (layersContainer != null)
            {
                SetLayersData(layersContainer);
            }
            else
            {
                layersController.CreateBaseLayers(Material.SourceTexture, copySourceTextureToLayer, useSourceTextureAsBackground);
                if (statesController != null)
                {
                    statesController.SetMinUndoStatesCount(useSourceTextureAsBackground ? 2 : 1);
                }
            }
        }
        
        private void OnLayersCollectionChanged(ObservableCollection<ILayer> collection, NotifyCollectionChangedEventArgs args)
        {
            TryRender();
        }

        private void TryRender()
        {
            var isLayersProcessing = (statesController != null && (statesController.IsRedoProcessing || statesController.IsUndoProcessing)) ||
                                     statesController == null ||
                                     layersController.IsMerging;
            if (isLayersProcessing)
                return;

            if (currentBrush != null && !currentBrush.Preview)
            {
                Render();
            }
        }

        private void InitPaintObject()
        {
            if (PaintObject != null)
            {
                UnsubscribeInputEvents();
                PaintObject.DoDispose();
            }

            if (renderComponentsHelper.ComponentType == ObjectComponentType.RawImage)
            {
                PaintObject = new CanvasRendererPaintObject();
            }
            else if (renderComponentsHelper.ComponentType == ObjectComponentType.SpriteRenderer)
            {
                PaintObject = new SpriteRendererPaintObject();
            }
            else if (renderComponentsHelper.ComponentType == ObjectComponentType.MeshFilter)
            {
                PaintObject = new MeshRendererPaintObject();
            }
            else if (renderComponentsHelper.ComponentType == ObjectComponentType.SkinnedMeshRenderer)
            {
                PaintObject = new SkinnedMeshRendererPaintObject();
            }
            else
            {
                throw new MissingComponentException($"A supported component was not found on {objectForPainting}");
            }
            
            paintData = new BasePaintData(this, renderTextureHelper, renderComponentsHelper);
            PaintObject.Init(this, paintData, ObjectForPainting.transform, Material);
            if (PaintSpace == PaintSpace.World)
            {
                PaintObject.InitRenderer(renderComponentsHelper.RendererComponent as Renderer);
            }
            
            if (Settings.Instance.VRModeEnabled)
            {
                PaintObject.SetRaycastProcessor(new PenRaycastProcessor());
            }
            
            layersMergeController.OnLayersMerge = PaintObject.RenderToTextureWithoutPreview;
        }

        private void InitTools()
        {
            if (toolsManager != null)
            {
                toolsManager.OnToolChanged -= OnToolChanged;
            }
            
            toolsManager?.DoDispose();
            if (PaintController.Instance.UseSharedSettings)
            {
                paintTool = PaintController.Instance.Tool;
            }
            
            toolsManager = new ToolsManager(paintTool, paintData);
            toolsManager.OnToolChanged += OnToolChanged;
            toolsManager.Init(this);
            toolsManager.CurrentTool.SetPaintMode(PaintMode);
            PaintObject.Tool = toolsManager.CurrentTool;
        }

        private void OnToolChanged(IPaintTool tool)
        {
            PaintObject.Tool = tool;
        }

        #region Setup Input Events

        private void SubscribeInputEvents()
        {
            if (inputData != null)
            {
                UnsubscribeInputEvents();
                inputData.DoDispose();
            }
            
            inputData = Settings.Instance.VRModeEnabled ? new InputDataVR() : new InputDataMesh();
            inputData.Init(this, PaintController.Instance.Camera);
            UpdatePreviewInput(currentBrush.Preview);
            InputController.Instance.OnUpdate += inputData.OnUpdate;
            InputController.Instance.OnMouseDown += inputData.OnDown;
            InputController.Instance.OnMouseButton += inputData.OnPress;
            InputController.Instance.OnMouseUp += inputData.OnUp;
            inputData.OnDownHandler += PaintObject.OnMouseDown;
            inputData.OnDownFailedHandler += PaintObject.OnMouseFailed;
            inputData.OnPressHandler += PaintObject.OnMouseButton;
            inputData.OnPressFailedHandler += PaintObject.OnMouseFailed;
            inputData.OnUpHandler += PaintObject.OnMouseUp;
        }

        private void UnsubscribeInputEvents()
        {
            inputData.OnHoverSuccessHandler -= PaintObject.OnMouseHover;
            inputData.OnHoverFailedHandler -= PaintObject.OnMouseHoverFailed;
            inputData.OnDownHandler -= PaintObject.OnMouseDown;
            inputData.OnDownFailedHandler -= PaintObject.OnMouseFailed;
            inputData.OnPressHandler -= PaintObject.OnMouseButton;
            inputData.OnPressFailedHandler -= PaintObject.OnMouseFailed;
            inputData.OnUpHandler -= PaintObject.OnMouseUp;
            InputController.Instance.OnUpdate -= inputData.OnUpdate;
            InputController.Instance.OnMouseHover -= inputData.OnHover;
            InputController.Instance.OnMouseDown -= inputData.OnDown;
            InputController.Instance.OnMouseButton -= inputData.OnPress;
            InputController.Instance.OnMouseUp -= inputData.OnUp;
        }

        private void UpdatePreviewInput(bool preview)
        {
            if (preview)
            {
                inputData.OnHoverSuccessHandler -= PaintObject.OnMouseHover;
                inputData.OnHoverSuccessHandler += PaintObject.OnMouseHover;
                inputData.OnHoverFailedHandler -= PaintObject.OnMouseHoverFailed;
                inputData.OnHoverFailedHandler += PaintObject.OnMouseHoverFailed;
                InputController.Instance.OnMouseHover -= inputData.OnHover;
                InputController.Instance.OnMouseHover += inputData.OnHover;
            }
            else
            {
                inputData.OnHoverSuccessHandler -= PaintObject.OnMouseHover;
                inputData.OnHoverFailedHandler -= PaintObject.OnMouseHoverFailed;
                InputController.Instance.OnMouseHover -= inputData.OnHover;
            }
        }

        #endregion
    }
}