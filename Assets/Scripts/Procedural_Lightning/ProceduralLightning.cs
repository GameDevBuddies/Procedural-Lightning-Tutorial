using UnityEngine;

namespace GameDevBuddies.ProceduralLightning
{
    /// <summary>
    /// Class responsible for controlling the process of creating a procedural lightning mesh.
    /// It guides the creation of the lightning shape and propagates that data to the 
    /// <see cref="LightningMesh"/> so that the mesh would be created.
    /// Finally, it's responsible for providing that mesh to the <see cref="MeshFilter"/>
    /// so that it would be submitted for rendering.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(MeshFilter))]
    public class ProceduralLightning : MonoBehaviour
    {
        [Header("References: ")]
        [SerializeField] private Transform _boltOriginTransform = null;
        [SerializeField] private Transform _boltImpactTransform = null;
        [SerializeField] private LightningShapeGenerator _lightningShapeGenerator = null;

        [Header("Lightning Settings: ")]
        [Tooltip("How many vertices are used to construct a segment around one point of the lightning bolt. In case the bolt is " +
            "seen from up close, use a higher segment resolution. Otherwise, use a lower one.")]
        [SerializeField, Range(3, 32)] private int _segmentResolution = 6;
        [Tooltip("Radius of each segment, used when creating vertices around the lightning point. " +
            "Basically represents the width of the lightning mesh.")]
        [SerializeField, Range(0.0001f, 0.1f)] private float _segmentRadius = 0.0003f;

        private MeshFilter _meshFilter = null;
        private LightningMesh _lightningMesh = null;

        /// <summary>
        /// Reference to the <see cref="UnityEngine.MeshFilter"/> component that holds the resulting lightning mesh.
        /// </summary>
        private MeshFilter MeshFilter
        {
            get
            {
                if (_meshFilter == null)
                {
                    // Lazy component fetching upon first request of the mesh filter.
                    _meshFilter = GetComponent<MeshFilter>();
                }
                return _meshFilter;
            }
        }
        /// <summary>
        /// Reference to the <see cref="LightningMesh"/> instance that is responsible for creation of the mesh
        /// for the lightning, based on the provided <see cref="LightningPoint"/>s.
        /// </summary>
        private LightningMesh LightningMesh
        {
            get
            {
                if (_lightningMesh == null)
                {
                    // Lazy initialization of a new LightningMesh instance.
                    _lightningMesh = new LightningMesh();
                }
                return _lightningMesh;
            }
            set
            {
                _lightningMesh = value;
            }
        }

        private void Update()
        {
            if (!AreAllReferencesConnected())
            {
                return;
            }

            // Construct the shape of the lightning that spans between the provided positions.
            _lightningShapeGenerator.CreateLightningShape(_boltOriginTransform.position, _boltImpactTransform.position);

            // Update the mesh to reflect the changes in the shape of the lightning. This will possibly require mesh re-construction
            // if the shape changed drastically.
            LightningMesh.UpdateMesh(_lightningShapeGenerator.LightningBranches, _segmentResolution, _segmentRadius);

            // Updating the mesh reference to the MeshFilter in case a new mesh has been constructed.
            if (MeshFilter.sharedMesh != LightningMesh.Mesh)
            {
                MeshFilter.sharedMesh = LightningMesh.Mesh;
            }
        }

        private void OnDestroy()
        {
            LightningMesh.CleanUpMesh();
            LightningMesh = null;
        }

        private bool AreAllReferencesConnected()
        {
            return _boltOriginTransform != null && _boltImpactTransform != null && _lightningShapeGenerator != null;
        }
    }
}