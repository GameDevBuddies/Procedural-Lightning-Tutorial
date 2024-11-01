using System;
using System.Collections.Generic;

namespace GameDevBuddies.ProceduralLightning
{
    /// <summary>
    /// Class containing data for one lightning branch. The branch is defined
    /// by a collection of <see cref="LightningPoint"/>s that define the shape of the
    /// lightning, as well as some supporting properties that define the width
    /// and the emission intensity for the rendering of the branch.
    /// </summary>
    [Serializable]
    public class LightningBranch
    {
        /// <summary>
        /// In which generation of the shape creation algorithm was this branch created.
        /// </summary>
        public int CreationGeneration;
        /// <summary>
        /// The index of the first point that should be used to continue executing the 
        /// shape creation algorithm on this branch after creation.
        /// </summary>
        public int SpawnPointIndex;
        /// <summary>
        /// Emission intensity percentage for this branch. Smaller child branches need to
        /// have a reduced emission intensity in relation to the main branch.
        /// </summary>
        public float IntensityPercentage;
        /// <summary>
        /// Width percentage of the main branch. Used when creating the mesh data for rendering.
        /// </summary>
        public float WidthPercentage;
        /// <summary>
        /// Collection of <see cref="LightningPoint"/>s that define the shape of the lightning.
        /// </summary>
        public List<LightningPoint> LightningPoints;
    }
}