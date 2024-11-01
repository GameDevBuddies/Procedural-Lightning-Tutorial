using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameDevBuddies.ProceduralLightning
{
    /// <summary>
    /// Class responsible for creating a <see cref="UnityEngine.Mesh"/> asset from the provided set of 
    /// <see cref="LightningPoint"/>s that define the shape of the lightning.
    /// </summary>
    [Serializable]
    public class LightningMesh
    {
        /// <summary>
        /// Constant that specifies that a certain field hasn't been initialized yet.
        /// </summary>
        private const int NOT_INITIALIZED = -1;

        private Mesh _mesh;
        // Total number of points used when specifying the shape of the lightning.
        private int _totalPointsCount;
        // Number of vertices that surround each point that specifies the shape.
        private int _segmentResolution;
        // Offset of each vertex from the point. Basically the width of the lightning.
        private float _segmentRadius;

        public LightningMesh()
        {
            _mesh = null;
            _totalPointsCount = NOT_INITIALIZED;
            _segmentResolution = NOT_INITIALIZED;
            _segmentRadius = NOT_INITIALIZED;
        }

        public void CleanUpMesh()
        {
            DestroyMesh();

            _totalPointsCount = NOT_INITIALIZED;
            _segmentResolution = NOT_INITIALIZED;
            _segmentRadius = NOT_INITIALIZED;
        }

        /// <summary>
        /// Function updates the mesh to match the provided <paramref name="lightningBranches"/>.
        /// In case the number of branches and points changed, a new mesh might be constructed.
        /// </summary>
        /// <param name="lightningBranches">Collection of <see cref="LightningBranch"/>es that define
        /// the shape of the lightning.</param>
        /// <param name="segmentResolution">Number of vertices that form each segment of a lightning mesh.</param>
        /// <param name="segmentRadius">Vertices radius that defines the total width of the lightning bolt.</param>
        public Mesh CreateLightningMesh(List<LightningBranch> lightningBranches, int segmentResolution, float segmentRadius)
        {
            // Calculate total points count.
            int totalPointsCount = 0;
            foreach (LightningBranch lightningBranch in lightningBranches)
            {
                totalPointsCount += lightningBranch.LightningPoints.Count;
            }

            // Check if the mesh needs to be re-constructed or the already constructed mesh can be used.
            if (NeedMeshReconstruction(totalPointsCount, segmentResolution))
            {
                // This will destroy the current mesh instance and create a new one.
                ReconstructMesh(lightningBranches, totalPointsCount, segmentResolution, segmentRadius);
            }

            // Total vertices count = number of segments *vertices count per segment.
            Vector3[] updatedVertices = new Vector3[totalPointsCount * _segmentResolution];
            // Offset of the first vertex index for a lightning branch in the final vertices array.
            int vertexIndexOffset = 0;

            // Updating vertices positions to match the new lightning shape.
            foreach (LightningBranch lightningBranch in lightningBranches)
            {
                // Reduce the width of the lightning branch based on generation in which it was created.
                // This will result in smaller supporting lightning bolts.
                float branchSegmentRadius = _segmentRadius * lightningBranch.WidthPercentage;
                foreach (LightningPoint lightningPoint in lightningBranch.LightningPoints)
                {
                    UpdatePointVertices(ref updatedVertices, ref vertexIndexOffset, lightningPoint, branchSegmentRadius);
                }
            }
            _mesh.vertices = updatedVertices;

            return _mesh;
        }

        private bool NeedMeshReconstruction(int pointsCount, int segmentResolution)
        {
            return _mesh == null || _totalPointsCount != pointsCount || _segmentResolution != segmentResolution;
        }

        private void ReconstructMesh(List<LightningBranch> lightningBranches, int pointsCount, int segmentResolution, float segmentRadius)
        {
            // Caching the latest mesh settings.
            _totalPointsCount = pointsCount;
            _segmentResolution = segmentResolution;
            _segmentRadius = segmentRadius;

            DestroyMesh();
            CreateMesh(lightningBranches);
        }

        /// <summary>
        /// Function updates position of vertices for a specific segment defined by the lightning point.
        /// </summary>
        /// <param name="vertices">Reference to the collection of vertices, where new updated positions will be added, for the provided point.</param>
        /// <param name="vertexIndex">Index of the first free slot in the vertices array.</param>
        /// <param name="lightningPoint">Class containing information of a specific point on the lightning bolt, that defines the shape of the lightning.</param>
        /// <param name="segmentRadius">Radius of the circular offset that will be applied to the vertices when creating segments.</param>
        private void UpdatePointVertices(ref Vector3[] vertices, ref int vertexIndex, LightningPoint lightningPoint, float segmentRadius)
        {
            // Local axes for the orientation of the lightning construction point.
            Vector3 right = lightningPoint.RightAxis;
            Vector3 up = lightningPoint.UpAxis;

            // Generating vertices positions in a circle around the lightning point.
            for (int i = 0; i < _segmentResolution; i++)
            {
                float pointPercentage = (float)i / (this._segmentResolution - 1);
                float angle = Mathf.Deg2Rad * pointPercentage * 360f;
                float offsetX = segmentRadius * Mathf.Cos(angle);
                float offsetY = segmentRadius * Mathf.Sin(angle);

                vertices[vertexIndex++] = lightningPoint.Position + right * offsetX + up * offsetY;
            }
        }

        /// <summary>
        /// Function creates a new mesh that can render the provided <paramref name="lightningBranches"/>.
        /// </summary>
        /// <param name="lightningBranches">Collection of <see cref="LightningBranch"/>es that contain the
        /// points that define the shape of the lightning.</param>
        private void CreateMesh(List<LightningBranch> lightningBranches)
        {
            _mesh = new Mesh();

            // Calculating total vertices and triangles count so that we can prevent continuous GC allocations.
            int totalVerticesCount = 0;
            int totalTrianglesCount = 0;
            foreach (LightningBranch lightningBranch in lightningBranches)
            {
                int branchPointsCount = lightningBranch.LightningPoints.Count;
                // Total vertices count = number of segments * vertices count per segment.
                int branchVerticesCount = _segmentResolution * branchPointsCount;
                // If there are X points, there are (X-1) segments connecting them. Each segment
                // requires 2 * verticesCount of triangles.
                int branchTrianglesCount = 2 * _segmentResolution * (branchPointsCount - 1);

                totalVerticesCount += branchVerticesCount;
                totalTrianglesCount += branchTrianglesCount;
            }

            // Initializing data structures for the mesh data.
            Vector3[] vertices = new Vector3[totalVerticesCount];
            Vector2[] uvs = new Vector2[totalVerticesCount];
            int[] triangleIndices = new int[totalTrianglesCount * 3];

            // Helper variables used to track offset in the vertices and triangles arrays.
            int totalFilledVertices = 0;
            int totalFilledTriangles = 0;
            foreach (LightningBranch lightningBranch in lightningBranches)
            {
                // Fill in the vertices, UVs and triangle indices array with the data for a single lightning branch.
                CreateMeshData(lightningBranch, ref vertices, ref uvs, ref triangleIndices, ref totalFilledVertices, ref totalFilledTriangles);
            }

            // Filling the mesh with the generated data.
            _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            _mesh.vertices = vertices;
            _mesh.uv = uvs;
            _mesh.triangles = triangleIndices;
        }

        /// <summary>
        /// Function that fills in the data to the provided <paramref name="vertices"/>, <paramref name="uvs"/> and <paramref name="triangleIndices"/> collections,
        /// for the specified <paramref name="lightningBranch"/>. 
        /// NOTE: The UV coordinates hold the emission intensity strength.
        /// </summary>
        /// <param name="lightningBranch">Reference to the <see cref="LightningBranch"/> for which the mesh data will be created.</param>
        /// <param name="vertices">Collection of mesh vertices for all branches.</param>
        /// <param name="uvs">Collection of mesh UV coordinates for all branches.</param>
        /// <param name="triangleIndices">Collection of triangle indices for all branches.</param>
        /// <param name="initialVerticesOffset">Offset of the first index in the vertices array for this branch. This is required to skip all previous branches
        /// that have already inserted their data to the vertices array.</param>
        /// <param name="initialTrianglesOffset">Offset of the first triangle index in the triangle indices array for this branch. This is required to skip 
        /// all previous branches that have already inserted their data to the triangle indices array.</param>
        private void CreateMeshData(LightningBranch lightningBranch, ref Vector3[] vertices, ref Vector2[] uvs, ref int[] triangleIndices,
            ref int initialVerticesOffset, ref int initialTrianglesOffset)
        {
            // Helper variables.
            List<LightningPoint> lightningPoints = lightningBranch.LightningPoints;
            int pointsCount = lightningPoints.Count;
            int vertexOffset = initialVerticesOffset;
            int trianglesOffset = initialTrianglesOffset;

            // UV coordinate that will be assigned to all UVs.
            Vector2 uvCoordinate = new Vector2(lightningBranch.IntensityPercentage, 0f);

            for (int i = 0; i < pointsCount; i++)
            {
                // Generating segment vertices and UV coordinates.
                for (int counter = 0; counter < _segmentResolution; counter++)
                {
                    vertices[vertexOffset] = Vector3.zero;
                    uvs[vertexOffset] = uvCoordinate;
                    vertexOffset++;
                }

                // Connecting triangles are added to connect the previous segment's vertices with the current one. 
                // Since the first segment doesn't have one before him, we need to skip it.
                if (i > 0)
                {
                    // Creating triangles that connect the current segment with the previous one.
                    int pointIndex = i;
                    AddTriangleIndices(ref triangleIndices, ref trianglesOffset, initialVerticesOffset, pointIndex);
                }
            }

            // Update the offset variables for the next lightning branch to skip all the data we've just added for this one.
            initialVerticesOffset += pointsCount * _segmentResolution;
            initialTrianglesOffset += 3 * 2 * (pointsCount - 1) * _segmentResolution;
        }

        private void AddTriangleIndices(ref int[] triangleIndices, ref int trianglesOffset, int vertexIndexOffset, int pointIndex)
        {
            int previousSegmentFirstIndex = (pointIndex - 1) * _segmentResolution;
            int previousSegmentLastIndex = previousSegmentFirstIndex + _segmentResolution - 1;
            int currentSegmentFirstIndex = previousSegmentLastIndex + 1;
            int currentSegmentLastIndex = currentSegmentFirstIndex + _segmentResolution - 1;

            for (int i = 0; i < _segmentResolution; i++)
            {
                // Previous segment vertex indices.
                int previousSegmentFirst = previousSegmentFirstIndex + i;
                int previousSegmentSecond = previousSegmentFirst + 1;
                if (previousSegmentSecond > previousSegmentLastIndex)
                {
                    previousSegmentSecond -= _segmentResolution;
                }

                // Current segment vertex indices.
                int currentSegmentFirst = currentSegmentFirstIndex + i;
                int currentSegmentSecond = currentSegmentFirst + 1;
                if (currentSegmentSecond > currentSegmentLastIndex)
                {
                    currentSegmentSecond -= _segmentResolution;
                }

                // First connecting triangle.
                triangleIndices[trianglesOffset++] = previousSegmentFirst + vertexIndexOffset;
                triangleIndices[trianglesOffset++] = currentSegmentSecond + vertexIndexOffset;
                triangleIndices[trianglesOffset++] = currentSegmentFirst + vertexIndexOffset;

                // Second connecting triangle.
                triangleIndices[trianglesOffset++] = previousSegmentFirst + vertexIndexOffset;
                triangleIndices[trianglesOffset++] = previousSegmentSecond + vertexIndexOffset;
                triangleIndices[trianglesOffset++] = currentSegmentSecond + vertexIndexOffset;
            }
        }

        private void DestroyMesh()
        {
            if (_mesh != null)
            {
#if UNITY_EDITOR
                if (!UnityEditor.EditorApplication.isPlaying)
                {
                    GameObject.DestroyImmediate(_mesh);
                    _mesh = null;
                    return;
                }
#endif
                GameObject.Destroy(_mesh);
                _mesh = null;
            }
        }
    }
}