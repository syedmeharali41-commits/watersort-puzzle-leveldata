using System;
using System.Collections.Generic;

namespace Designcoffers.WaterSort.Data
{
    [Serializable]
    public class LevelData
    {
        public int levelNumber;
        public int colorCount;
        public int tubeCount;
        public int capacity;
        /// <summary>Difficulty floor calculated from the PRD for this level.</summary>
        public int requiredMinMoves;
        /// <summary>Exact optimal move count returned by the offline validator.</summary>
        public int minMoves;
        /// <summary>Deterministic seed used to create this bundled layout.</summary>
        public int generationSeed;
        /// <summary>Search nodes expanded by the offline optimal solver.</summary>
        public int solverStatesExplored;
        /// <summary>Sub-seeds attempted before a validated layout was found.</summary>
        public int generationAttempts;
        /// <summary>
        /// True when minMoves is the exact optimal move count; false when the floor was
        /// proven by bounded BFS and minMoves is a certified lower bound (optimal >= floor).
        /// </summary>
        public bool validationExact;
        /// <summary>Tube indices frozen (unusable) for the first lockDuration moves (Expert/World-Class bands). Empty = no locks.</summary>
        public List<int> lockedTubes;
        /// <summary>Number of opening moves during which lockedTubes are unusable.</summary>
        public int lockDuration;
        /// <summary>
        /// Array of tubes. Each tube is a list of color IDs (1-based), from bottom (index 0) to top.
        /// Empty tubes are represented as empty lists.
        /// </summary>
        public List<List<int>> tubes;

        public LevelData()
        {
            tubes = new List<List<int>>();
            lockedTubes = new List<int>();
        }

        public LevelData Clone()
        {
            var clone = new LevelData
            {
                levelNumber = this.levelNumber,
                colorCount = this.colorCount,
                tubeCount = this.tubeCount,
                capacity = this.capacity,
                requiredMinMoves = this.requiredMinMoves,
                minMoves = this.minMoves,
                generationSeed = this.generationSeed,
                solverStatesExplored = this.solverStatesExplored,
                generationAttempts = this.generationAttempts,
                validationExact = this.validationExact,
                lockedTubes = new List<int>(this.lockedTubes ?? new List<int>()),
                lockDuration = this.lockDuration,
                tubes = new List<List<int>>()
            };

            foreach (var t in tubes)
            {
                clone.tubes.Add(new List<int>(t));
            }
            return clone;
        }
    }

    [Serializable]
    public class LevelBundle
    {
        public List<LevelData> levels = new List<LevelData>();
    }

    public struct MoveRecord
    {
        public int fromTubeIndex;
        public int toTubeIndex;
        public int colorId;
        public int amount;
    }
}
