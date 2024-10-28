using System.Collections.Generic;
using UnityEngine;

namespace GameDevBuddies.ProceduralLightning
{
    /// <summary>
    /// Class responsible for defining the shape of the lightning bolt. It executes a custom algorithm
    /// that can be sorted in the L-Systems category. Additionally, it applies a custom "animation" logic
    /// to the lightning so that it would look realistic.
    /// </summary>
    public class LightningShapeGenerator : MonoBehaviour
    {
        [Header("Update/Animation Settings: ")]
        [SerializeField, Range(0f, 1f)] private float _minTimeBeforeShapeChange = 0.1f;
        [SerializeField, Range(0f, 1f)] private float _maxTimeBeforeShapeChange = 0.3f;
        [Space]
        [Tooltip("How much (expressed as lengthwise percentage of the segment) can mid point offset " +
            "during the animation cycle of the lightning bolt.")]
        [SerializeField, Range(0f, 0.5f)] private float _animationEndMinHorizontalOffset = 0.01f;
        [SerializeField, Range(0f, 0.5f)] private float _animationEndMaxHorizontalOffset = 0.05f;

        [Header("Subdivision Settings: ")]
        [SerializeField, Range(0, 10)] private int _minGenerationsCount = 6;
        [SerializeField, Range(0, 10)] private int _maxGenerationsCount = 8;
        [SerializeField, Range(0f, 1f)] private float _nextGenerationSupportPercentage = 0.8f;

        [Header("Birth Settings: ")]
        [SerializeField, Range(0f, 1f)] private float _newLightningBirthChance = 0.15f;
        [SerializeField, Range(0f, 2f)] private float _birthChanceMultiplierByGeneration = 1.25f;
        [SerializeField, Range(1, 20)] private int _maxBranchesCount = 10;
        [SerializeField, Range(0f, 1f)] private float _newBranchIntensityDecreaseMultiplier = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _newBranchWidthDecreaseMultiplier = 0.8f;

        [Header("Displacement Settings: ")]
        [SerializeField, Range(0.00001f, 1f)] private float _maxMiddlePointDisplacement = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _displacementDecreaseMultiplierByGeneration = 0.55f;

        [Header("Animation Settings: ")]
        [SerializeField, Range(0f, 1f)] private float _branchInvisibleChance = 0.25f;
        [SerializeField, Range(1f, 2f)] private float _branchInvisibleChanceMultiplierByGeneration = 1.25f;
        [SerializeField, Range(0f, 1f)] private float _maxBranchInvisibleChance = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _minTimeBeforeVisibilityUpdate = 0.015f;
        [SerializeField, Range(0f, 1f)] private float _maxTimeBeforeVisibilityUpdate = 0.1f;

        private List<LightningBranch> _lightningBranches = new List<LightningBranch>();

        // Lightning shape animation helper variables.
        private float _timeFromLastShapeChange = 0f;
        private float _nextShapeChangeDelay = 0f;

        // Lightning horizontal movement helper variables.
        private float _midPointMaxHorizontalOffset = 0f;
        private float _currentFrameMidPointHorizontalOffset = 0f;

        // Randomization control variables.
        private int _currentRandomizationSeed = 0;
        private System.Random _random = null;

        // Branches visibility update.
        private float _timeFromLastVisibilityUpdate = 0f;
        private float _nextVisibilityUpdateDelay = 0f;
        private List<int> _visibleBranchesIndices = new List<int>();


        /// <summary>
        /// Property providing reference to the created collection of <see cref="LightningBranch"/>
        /// that can be used for creating the mesh for rendering the lightning.
        /// </summary>
        public List<LightningBranch> LightningBranches { get => _lightningBranches; }

        /// <summary>
        /// Method that generates the shape of the lightning. The shape is defined as a collection of 
        /// <see cref="LightningBranch"/>es, where each branch defines a shape of one lightning bolt. 
        /// </summary>
        /// <param name="originPoint">Position in the world space where the lightning should start.</param>
        /// <param name="impactPoint">Position in the world space where the lightning should end.</param>
        public void CreateLightningShape(Vector3 originPoint, Vector3 impactPoint)
        {
            // Checking if we should change the main shape of the lightning bolts or not.
            // Only changing the main shape if the enough time has passed from the last change.
            CheckForShapeUpdate();

            // Clearing old data and creating a new initial lightning bolt. This has to be done
            // each frame to support the horizontal scroll movement towards the origin point.
            _lightningBranches.Clear();
            _lightningBranches.Add(CreateInitialLightningBranch(originPoint, impactPoint));

            // For every lightning branch, run the algorithm that displaces it's points until the max generations have been reached.
            // Important note, must calculate list count every iteration since new branches can be dynamically added to the collection!
            for (int lightningBranchIndex = 0; lightningBranchIndex < _lightningBranches.Count; lightningBranchIndex++)
            {
                LightningBranch lightningBranch = _lightningBranches[lightningBranchIndex];
                GenerateLightningShape(lightningBranch);
            }

            // Updating visibility of the lightning branches. This hides/shows branches randomly to increase the dynamic of the effect.
            UpdateBranchesVisibility();
        }

        private void CheckForShapeUpdate()
        {
            _timeFromLastShapeChange += Time.deltaTime;

            if (_timeFromLastShapeChange >= _nextShapeChangeDelay)
            {
                // Update the randomization seed which will produce a different looking lightning bolt shape.
                _currentRandomizationSeed += 1;

                if (_random == null)
                {
                    _random = new System.Random(_currentRandomizationSeed);
                }

                // Cache the time when the next shape change should occur.
                _nextShapeChangeDelay = Mathf.Lerp(_minTimeBeforeShapeChange, _maxTimeBeforeShapeChange, (float)_random.NextDouble());
                _timeFromLastShapeChange = 0f;

                // How much can segment's mid point move during the update cycle. This determines the horizontal scroll speed of the lightning.
                _midPointMaxHorizontalOffset = Mathf.Lerp(_animationEndMinHorizontalOffset, _animationEndMaxHorizontalOffset, (float)_random.NextDouble());

                // Resetting the visibility update. We're forcing it to happen in this frame since the branch shape will be changed.
                _timeFromLastVisibilityUpdate = _nextVisibilityUpdateDelay;
            }

            // Resetting randomization generator to get the same looking lightning bolt every time.
            _random = new System.Random(_currentRandomizationSeed);
            float horizontalOffsetPercentage = Mathf.Clamp01((float)_timeFromLastShapeChange / _nextShapeChangeDelay);
            _currentFrameMidPointHorizontalOffset = Mathf.Lerp(0f, _midPointMaxHorizontalOffset, horizontalOffsetPercentage);
        }

        private LightningBranch CreateInitialLightningBranch(Vector3 originPosition, Vector3 impactPosition)
        {
            // Set up initial properties for the algorithm.
            Vector3 originPointLocalSpace = transform.InverseTransformPoint(originPosition);
            Vector3 impactPointLocalSpace = transform.InverseTransformPoint(impactPosition);

            // Initialize local point axis required for orientation calculation.
            Vector3 forwardAxis = (impactPointLocalSpace - originPointLocalSpace).normalized;
            (Vector3 rightAxis, Vector3 _) = CreateOrthogonalAxes(forwardAxis);

            // Create a collection for the lightning points, with capacity set to max possible elements.
            // This will only prevent list from re-allocating size until the elements count is reached.
            List<LightningPoint> points = new List<LightningPoint>((int)Mathf.Pow(2, _maxGenerationsCount) + 1);

            // Add two initial points, an origin and an impact point.
            LightningPoint originPoint = new LightningPoint
            {
                Position = originPointLocalSpace,
                PointForward = forwardAxis,
                PointRight = rightAxis,
                SupportsNextGenerations = true
            };
            LightningPoint impactPoint = new LightningPoint
            {
                Position = impactPointLocalSpace,
                PointForward = forwardAxis,
                PointRight = rightAxis,
                SupportsNextGenerations = true
            };

            points.Add(originPoint);
            points.Add(impactPoint);

            // Creating initial lightning branch for the algorithm.
            LightningBranch initialLightningBranch = new LightningBranch
            {
                IntensityPercentage = 1f,
                WidthPercentage = 1f,
                CreationGeneration = 1,
                SpawnPointIndex = 0,
                LightningPoints = points
            };

            return initialLightningBranch;
        }

        private void GenerateLightningShape(LightningBranch lightningBranch)
        {
            int initialGeneration = lightningBranch.CreationGeneration;

            // Running the algorithm for splitting the lightning into segments and creating the shape of the lightning.
            for (int currentGeneration = initialGeneration; currentGeneration <= _maxGenerationsCount; currentGeneration++)
            {
                // Every new generation should have a smaller maximum allowed offset than the previous one.
                float generationMaxPossibleOffset = _maxMiddlePointDisplacement * Mathf.Pow(_displacementDecreaseMultiplierByGeneration, currentGeneration - 1);
                // Every new should have a higher chance of spawning supporting lightning bolts.
                float newBranchCreationChance = _newLightningBirthChance * Mathf.Pow(_birthChanceMultiplierByGeneration, currentGeneration - 1);
                // Index defining the starting point of the algorithm. In most cases we start from the first point, except when continuing the algorithm
                // for the newly created supporting branches.
                int firstPointIndex = currentGeneration == initialGeneration ? lightningBranch.SpawnPointIndex : 0;

                List<LightningPoint> points = lightningBranch.LightningPoints;
                // Must calculate the points count every iteration since new points are being added to the collection.
                for (int pointIndex = firstPointIndex; pointIndex < (points.Count - 1); pointIndex++)
                {
                    LightningPoint currentPoint = points[pointIndex];
                    LightningPoint nextPoint = points[pointIndex + 1];

                    // Calculate the mid point position.
                    float midPointReducedGenerationOffset = _maxGenerationsCount != 0 ? ((float)currentGeneration + 1) / _maxGenerationsCount : 0f;
                    float midPointPercentage = 0.5f - Mathf.Lerp(_currentFrameMidPointHorizontalOffset, 0f, midPointReducedGenerationOffset * 2f);
                    Vector3 midPointPosition = Vector3.Lerp(currentPoint.Position, nextPoint.Position, midPointPercentage);

                    // If any of the parent points support next generations, we will displace the middle point.
                    bool canDisplaceMidPoint = currentPoint.SupportsNextGenerations || nextPoint.SupportsNextGenerations;
                    bool supportsNextGenerations = currentGeneration <= _minGenerationsCount ? true : (float)_random.NextDouble() <= _nextGenerationSupportPercentage;

                    // Calculate local axis for the middle point, upon which it will be displaced.
                    Vector3 pointForwardAxis = (nextPoint.Position - currentPoint.Position).normalized;
                    (Vector3 pointRightAxis, Vector3 pointUpAxis) = CreateOrthogonalAxes(pointForwardAxis);

                    // Initialize the new middle point.
                    LightningPoint newMidPoint = new LightningPoint
                    {
                        Position = midPointPosition,
                        PointForward = pointForwardAxis,
                        PointRight = pointRightAxis,
                        SupportsNextGenerations = canDisplaceMidPoint && supportsNextGenerations
                    };

                    // Displace middle point if it can be displaced.
                    if (canDisplaceMidPoint)
                    {
                        // Offset mid point by random amount, limited by maximum allowed offset.
                        float randomRightOffset = Mathf.Lerp(-generationMaxPossibleOffset, generationMaxPossibleOffset, (float)_random.NextDouble());
                        float randomUpOffset = Mathf.Lerp(-generationMaxPossibleOffset, generationMaxPossibleOffset, (float)_random.NextDouble());
                        Vector3 rightAxisOffset = pointRightAxis * randomRightOffset;
                        Vector3 upAxisOffset = pointUpAxis * randomUpOffset;

                        // Displacing the middle point.
                        midPointPosition += rightAxisOffset + upAxisOffset;
                        newMidPoint.Position = midPointPosition;

                        // Update orientations of the current point to look at the newly added mid point.
                        pointForwardAxis = (midPointPosition - currentPoint.Position).normalized;
                        (pointRightAxis, _) = CreateOrthogonalAxes(pointForwardAxis);
                        currentPoint.PointForward = pointForwardAxis;
                        currentPoint.PointRight = pointRightAxis;

                        // Update orientation of the middle point to look at the next point.
                        pointForwardAxis = (nextPoint.Position - midPointPosition).normalized;
                        (pointRightAxis, _) = CreateOrthogonalAxes(pointForwardAxis);
                        newMidPoint.PointForward = pointForwardAxis;
                        newMidPoint.PointRight = pointRightAxis;
                    }

                    // Adding middle point to the collection of points.
                    points.Insert(pointIndex + 1, newMidPoint);
                    // Skipping over the newly added middle point for the next iteration of the loop.
                    pointIndex++;

                    // Check if we can spawn new branch or not. Forbidding spawning new branches in the last generation.
                    if (currentGeneration == _maxGenerationsCount || _lightningBranches.Count == _maxBranchesCount)
                    {
                        continue;
                    }

                    bool shouldSpawnNewBranch = (float)_random.NextDouble() <= newBranchCreationChance;
                    if (shouldSpawnNewBranch)
                    {
                        // Deep copy of the whole collection so that we would create new point instances.
                        List<LightningPoint> newPoints = new List<LightningPoint>(points.Capacity);
                        for (int i = 0; i < points.Count; i++)
                        {
                            LightningPoint sourcePoint = points[i];
                            newPoints.Add(new LightningPoint
                            {
                                Position = sourcePoint.Position,
                                PointForward = sourcePoint.PointForward,
                                PointRight = sourcePoint.PointRight,
                                SupportsNextGenerations = sourcePoint.SupportsNextGenerations
                            });
                        }

                        // Create a new lightning branch and initialize it's starting properties.
                        LightningBranch newLightningBranch = new LightningBranch
                        {
                            CreationGeneration = currentGeneration,
                            SpawnPointIndex = pointIndex + 1,
                            IntensityPercentage = lightningBranch.IntensityPercentage * _newBranchIntensityDecreaseMultiplier,
                            WidthPercentage = lightningBranch.WidthPercentage * _newBranchWidthDecreaseMultiplier,
                            LightningPoints = newPoints
                        };

                        _lightningBranches.Add(newLightningBranch);
                    }
                }
            }
        }

        private void UpdateBranchesVisibility()
        {
            _timeFromLastVisibilityUpdate += Time.deltaTime;
            if (_timeFromLastVisibilityUpdate >= _nextVisibilityUpdateDelay)
            {
                _nextVisibilityUpdateDelay = Random.Range(_minTimeBeforeVisibilityUpdate, _maxTimeBeforeVisibilityUpdate);
                _timeFromLastVisibilityUpdate = 0f;
                _visibleBranchesIndices.Clear();

                // Randomly set each branch as visible or invisible.
                for (int lightningBranchIndex = 0; lightningBranchIndex < _lightningBranches.Count; lightningBranchIndex++)
                {
                    LightningBranch lightningBranch = _lightningBranches[lightningBranchIndex];
                    float branchInvisibleChance = _branchInvisibleChance * Mathf.Pow(_branchInvisibleChanceMultiplierByGeneration, lightningBranch.CreationGeneration);
                    branchInvisibleChance = Mathf.Min(_maxBranchInvisibleChance, branchInvisibleChance);
                    bool shouldBranchBeVisible = Random.Range(0f, 1f) > branchInvisibleChance;
                    if (shouldBranchBeVisible)
                    {
                        _visibleBranchesIndices.Add(lightningBranchIndex);
                    }
                }
            }

            // Remove all invisible branches from the collection that will be provided for mesh generation.
            int numberOfRemovedBranches = 0;
            for (int lightningBranchIndex = 0; lightningBranchIndex < _lightningBranches.Count; lightningBranchIndex++)
            {
                int branchOriginalIndex = lightningBranchIndex + numberOfRemovedBranches;
                if (!_visibleBranchesIndices.Contains(branchOriginalIndex))
                {
                    _lightningBranches.RemoveAt(lightningBranchIndex);
                    lightningBranchIndex--;
                    numberOfRemovedBranches++;
                }
            }
        }

        /// <summary>
        /// Function calculates orthogonal axes for the provided <paramref name="forwardAxis"/>.
        /// </summary>
        /// <param name="forwardAxis">Normalized forward direction for which the orthogonal axis should
        /// be calculated.</param>
        /// <returns>Tuple of (rightAxis, upAxis) that are orthogonal on the provided <paramref name="forwardAxis"/>.</returns>
        protected virtual (Vector3, Vector3) CreateOrthogonalAxes(Vector3 forwardAxis)
        {
            Vector3 rightAxis;
            if (Mathf.Approximately(Mathf.Abs(forwardAxis.y), 1f))
            {
                // When looking straight up/down.
                rightAxis = forwardAxis.y > 0f ? Vector3.right : Vector3.left;
            }
            else
            {
                rightAxis = Vector3.Cross(Vector3.up, forwardAxis).normalized;
            }
            Vector3 upAxis = Vector3.Cross(forwardAxis, rightAxis);
            return (rightAxis, upAxis);
        }
    }
}