using System;
using UnityEngine;

namespace GameDevBuddies.ProceduralLightning
{
    /// <summary>
    /// Class containing information required for every point that is used to construct
    /// the lighting. Each point represents a place on the lightning where the lightning mesh
    /// can deform and animate.
    /// </summary>
    [Serializable]
    public class LightningPoint
    {
        /// <summary>
        /// Local position of the point.
        /// </summary>
        public Vector3 Position;
        /// <summary>
        /// Direction of the forward vector of the point, in local space of the lightning.
        /// </summary>
        public Vector3 ForwardAxis;
        /// <summary>
        /// Direction of the right vector of the point in local space of the lightning.
        /// </summary>
        public Vector3 RightAxis;
        /// <summary>
        /// Direction of the up vector of the point in local space of the lightning.
        /// </summary>
        public Vector3 UpAxis;
        /// <summary>
        /// Boolean specifying if the point supports displacement of future generations or not.
        /// If this is false, no newly added middle points, that connect with this one, will 
        /// be displaced.
        /// </summary>
        public bool SupportsNextGenerations;
    }
}