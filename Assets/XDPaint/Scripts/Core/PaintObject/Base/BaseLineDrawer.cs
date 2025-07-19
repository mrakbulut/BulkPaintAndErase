using System;
using System.Collections.Generic;
using UnityEngine;
using XDPaint.Controllers;
using XDPaint.Tools;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Utils;
using Random = UnityEngine.Random;

namespace XDPaint.Core.PaintObject.Base
{
    /// <summary>
    /// Performs lines drawing
    /// </summary>
    public class BaseLineDrawer
    {
        private IPaintManager paintManager;
        private Action<List<Vector3>, List<Vector2>, List<int>, List<Color>> drawLineUV;
        private Action<Mesh> onRenderMesh;
        private List<KeyValuePair<Ray, RaycastData>> raycasts = new List<KeyValuePair<Ray, RaycastData>>();
        private List<Vector3> vertices = new List<Vector3>();
        private List<Color> colors = new List<Color>();
        private List<int> indices = new List<int>();
        private List<Vector2> uv = new List<Vector2>();
        private Vector2Int textureSize;

        public BaseLineDrawer(IPaintManager paintManagerInstance)
        {
            paintManager = paintManagerInstance;
        }

        public void Init(Vector2Int sourceTextureSize, Action<List<Vector3>, List<Vector2>, List<int>, List<Color>> drawUVLine, Action<Mesh> renderMesh)
        {
            textureSize = sourceTextureSize;
            drawLineUV = drawUVLine;
            onRenderMesh = renderMesh;
        }

        public List<KeyValuePair<Ray, RaycastData>> GetLineRaycasts(RaycastData raycastStart, RaycastData raycastEnd, Vector3 pointerPosition, float averageBrushSize, int fingerId)
        {
            raycasts.Clear();
            if (raycastStart == raycastEnd)
            {
                raycasts.Add(new KeyValuePair<Ray, RaycastData>(new Ray(pointerPosition, pointerPosition - raycastEnd.WorldHit), raycastEnd));
                return raycasts;
            }

            raycasts.Add(new KeyValuePair<Ray, RaycastData>(new Ray(pointerPosition, pointerPosition - raycastStart.WorldHit), raycastStart));
            var direction = (raycastStart.WorldHit - raycastEnd.WorldHit).normalized;
            float distance = Vector3.Distance(raycastStart.WorldHit, raycastEnd.WorldHit);
            int steps = Mathf.FloorToInt(distance / averageBrushSize / Settings.Instance.RaycastInterval);
            for (int i = 0; i < steps; i++)
            {
                var position = raycastStart.WorldHit - direction * ((i + 1) * Settings.Instance.RaycastInterval * averageBrushSize);
                var ray = GetWorldRay(pointerPosition, position);
                var raycastData = RaycastController.Instance.RaycastLocal(paintManager, ray, pointerPosition, fingerId);
                if (raycastData == null)
                {
                    continue;
                }

                raycasts.Add(new KeyValuePair<Ray, RaycastData>(ray, raycastData));
            }

            raycasts.Add(new KeyValuePair<Ray, RaycastData>(new Ray(pointerPosition, pointerPosition - raycastEnd.WorldHit), raycastEnd));
            return raycasts;
        }

        /// <summary>
        /// Creates line mesh for UV-space
        /// </summary>
        public Mesh GenerateMeshUV(IList<Vector2> lineTexturePositions, Vector2 renderOffset, Texture brushTexture, IList<float> brushSizes, Color initialColor, bool randomizeAngle = false)
        {
            float pressureStart = brushSizes[0];
            float pressureEnd = brushSizes.Count > 1 ? brushSizes[1] : brushSizes[0];
            int brushWidth = brushTexture.width;
            int brushHeight = brushTexture.height;
            int quadsCount = Mathf.Min(lineTexturePositions.Count, 16384);
            ClearMeshData();
            var scale = new Vector2(textureSize.x, textureSize.y);
            float minDistance = Mathf.Max(1, (float)(quadsCount - 1));
            for (int i = 0; i < quadsCount; i++)
            {
                var center = (Vector3)lineTexturePositions[i];
                float t = Mathf.Clamp01(i / minDistance);
                float quadScale = Mathf.Lerp(pressureStart, pressureEnd, t);
                var v1 = center + new Vector3(-brushWidth, brushHeight) * quadScale / 2f;
                var v2 = center + new Vector3(brushWidth, brushHeight) * quadScale / 2f;
                var v3 = center + new Vector3(brushWidth, -brushHeight) * quadScale / 2f;
                var v4 = center + new Vector3(-brushWidth, -brushHeight) * quadScale / 2f;
                if (randomizeAngle)
                {
                    var quaternion = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
                    v1 = quaternion * (v1 - center) + center;
                    v2 = quaternion * (v2 - center) + center;
                    v3 = quaternion * (v3 - center) + center;
                    v4 = quaternion * (v4 - center) + center;
                }

                v1 = v1 / scale + renderOffset;
                v2 = v2 / scale + renderOffset;
                v3 = v3 / scale + renderOffset;
                v4 = v4 / scale + renderOffset;

                vertices.Add(v1);
                vertices.Add(v2);
                vertices.Add(v3);
                vertices.Add(v4);

                uv.Add(Vector2.up);    // (0,1) - top left
                uv.Add(Vector2.one);   // (1,1) - top right  
                uv.Add(Vector2.right); // (1,0) - bottom right
                uv.Add(Vector2.zero);  // (0,0) - bottom left

                colors.Add(Color.white);
                colors.Add(Color.white);
                colors.Add(Color.white);
                colors.Add(Color.white);

                // Use same triangle index pattern as original RenderLineUV
                indices.Add(0 + i * 4);
                indices.Add(1 + i * 4);
                indices.Add(2 + i * 4);
                indices.Add(2 + i * 4);
                indices.Add(3 + i * 4);
                indices.Add(0 + i * 4);
            }

            // Create and return mesh
            var mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(indices, 0);
            mesh.SetColors(colors);
            return mesh;
        }

        public void RenderLineUV(IList<Vector2> lineTexturePositions, Vector2 renderOffset, Texture brushTexture, IList<float> brushSizes, bool randomizeAngle = false)
        {
            float pressureStart = brushSizes[0];
            float pressureEnd = brushSizes.Count > 1 ? brushSizes[1] : brushSizes[0];
            int brushWidth = brushTexture.width;
            int brushHeight = brushTexture.height;
            int quadsCount = Mathf.Min(lineTexturePositions.Count, 16384);
            ClearMeshData();
            var scale = new Vector2(textureSize.x, textureSize.y);
            float minDistance = Mathf.Max(1, (float)(quadsCount - 1));
            for (int i = 0; i < quadsCount; i++)
            {
                var center = (Vector3)lineTexturePositions[i];
                float t = Mathf.Clamp01(i / minDistance);
                float quadScale = Mathf.Lerp(pressureStart, pressureEnd, t);
                var v1 = center + new Vector3(-brushWidth, brushHeight) * quadScale / 2f;
                var v2 = center + new Vector3(brushWidth, brushHeight) * quadScale / 2f;
                var v3 = center + new Vector3(brushWidth, -brushHeight) * quadScale / 2f;
                var v4 = center + new Vector3(-brushWidth, -brushHeight) * quadScale / 2f;
                if (randomizeAngle)
                {
                    var quaternion = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
                    v1 = quaternion * (v1 - center) + center;
                    v2 = quaternion * (v2 - center) + center;
                    v3 = quaternion * (v3 - center) + center;
                    v4 = quaternion * (v4 - center) + center;
                }

                v1 = v1 / scale + renderOffset;
                v2 = v2 / scale + renderOffset;
                v3 = v3 / scale + renderOffset;
                v4 = v4 / scale + renderOffset;

                vertices.Add(v1);
                vertices.Add(v2);
                vertices.Add(v3);
                vertices.Add(v4);

                colors.Add(Color.white);
                colors.Add(Color.white);
                colors.Add(Color.white);
                colors.Add(Color.white);

                uv.Add(Vector2.up);
                uv.Add(Vector2.one);
                uv.Add(Vector2.right);
                uv.Add(Vector2.zero);

                indices.Add(0 + i * 4);
                indices.Add(1 + i * 4);
                indices.Add(2 + i * 4);
                indices.Add(2 + i * 4);
                indices.Add(3 + i * 4);
                indices.Add(0 + i * 4);
            }

            if (vertices.Count > 0)
            {
                //BasePaintObjectRenderer.RenderLineUV
                drawLineUV(vertices, uv, indices, colors);
            }

            ClearMeshData();
        }

        public void RenderLineUVInterpolated(IList<Vector2> paintLinePositions, Vector2 renderOffset, Texture brushTexture, float brushSizeActual, IList<float> brushSizes, bool randomizeAngle = false)
        {
            float pressureStart = brushSizes[0];
            float pressureEnd = brushSizes[1];
            int brushWidth = brushTexture.width;
            int brushHeight = brushTexture.height;
            float maxBrushPressure = Mathf.Max(pressureStart, pressureEnd);
            var brushOffset = new Vector2(brushWidth, brushHeight) * maxBrushPressure;
            float[] distances = new float[paintLinePositions.Count / 2];
            float totalDistance = 0f;
            for (int i = 0; i < paintLinePositions.Count - 1; i += 2)
            {
                var from = paintLinePositions[i + 0];
                from = from.Clamp(Vector2.zero - brushOffset, textureSize + brushOffset);
                var to = paintLinePositions[i + 1];
                to = to.Clamp(Vector2.zero - brushOffset, textureSize + brushOffset);
                paintLinePositions[i + 0] = from;
                paintLinePositions[i + 1] = to;
                distances[i / 2] = Vector2.Distance(from, to);
                totalDistance += distances[i / 2];
            }

            float ratio = GetRatio(totalDistance, brushSizeActual, brushSizes) * 2f;
            int quadsCount = 0;
            for (int i = 0; i < paintLinePositions.Count - 1; i += 2)
            {
                quadsCount += (int)(distances[i / 2] * ratio + 1);
            }

            quadsCount = Mathf.Clamp(quadsCount, paintLinePositions.Count / 2, 16384);
            ClearMeshData();
            int count = 0;
            var scale = new Vector2(textureSize.x, textureSize.y);
            for (int i = 0; i < paintLinePositions.Count - 1; i += 2)
            {
                var from = paintLinePositions[i + 0];
                var to = paintLinePositions[i + 1];
                int currentDistance = Mathf.Max(1, (int)(distances[i / 2] * ratio));
                for (int j = 0; j < currentDistance; j++)
                {
                    float minDistance = Mathf.Max(1, (float)(quadsCount - 1));
                    float t = Mathf.Clamp(count / minDistance, 0, 1);
                    float quadScale = Mathf.Lerp(pressureStart, pressureEnd, t);
                    var center = (Vector3)(from + (to - from) / currentDistance * j);
                    var v1 = center + new Vector3(-brushWidth, brushHeight) * quadScale / 2f;
                    var v2 = center + new Vector3(brushWidth, brushHeight) * quadScale / 2f;
                    var v3 = center + new Vector3(brushWidth, -brushHeight) * quadScale / 2f;
                    var v4 = center + new Vector3(-brushWidth, -brushHeight) * quadScale / 2f;
                    if (randomizeAngle)
                    {
                        var quaternion = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
                        v1 = quaternion * (v1 - center) + center;
                        v2 = quaternion * (v2 - center) + center;
                        v3 = quaternion * (v3 - center) + center;
                        v4 = quaternion * (v4 - center) + center;
                    }

                    v1 = v1 / scale + renderOffset;
                    v2 = v2 / scale + renderOffset;
                    v3 = v3 / scale + renderOffset;
                    v4 = v4 / scale + renderOffset;

                    vertices.Add(v1);
                    vertices.Add(v2);
                    vertices.Add(v3);
                    vertices.Add(v4);

                    colors.Add(Color.white);
                    colors.Add(Color.white);
                    colors.Add(Color.white);
                    colors.Add(Color.white);

                    uv.Add(Vector2.up);
                    uv.Add(Vector2.one);
                    uv.Add(Vector2.right);
                    uv.Add(Vector2.zero);

                    indices.Add(0 + count * 4);
                    indices.Add(1 + count * 4);
                    indices.Add(2 + count * 4);
                    indices.Add(2 + count * 4);
                    indices.Add(3 + count * 4);
                    indices.Add(0 + count * 4);

                    count++;
                }
            }

            if (vertices.Count > 0)
            {
                //BasePaintObjectRenderer.RenderLineUV
                drawLineUV(vertices, uv, indices, colors);
            }

            ClearMeshData();
        }

        public void RenderLineWorld()
        {
            //BasePaintObjectRenderer.RenderMesh
            onRenderMesh(null);
            raycasts.Clear();
        }

        private Ray GetWorldRay(Vector3 pointerPosition, Vector3 point)
        {
            var direction = point - pointerPosition;
            var ray = new Ray(point + direction, -direction);
            return ray;
        }

        private float GetRatio(float totalDistanceInPixels, float brushSize, IList<float> brushSizes)
        {
            float brushPressureStart = brushSizes[0];
            float brushPressureEnd = brushSizes[1];
            float pressureDifference = Mathf.Abs(brushPressureStart - brushPressureEnd);
            float brushCenterPartWidth = Mathf.Clamp(Settings.Instance.BrushDuplicatePartWidth * brushSize, 1f, 100f);
            float ratioBrush = totalDistanceInPixels * pressureDifference / brushCenterPartWidth;
            float ratioSource = totalDistanceInPixels / brushCenterPartWidth;
            return (ratioSource + ratioBrush) / totalDistanceInPixels;
        }

        private void ClearMeshData()
        {
            raycasts.Clear();
            vertices.Clear();
            colors.Clear();
            indices.Clear();
            uv.Clear();
        }
    }
}
