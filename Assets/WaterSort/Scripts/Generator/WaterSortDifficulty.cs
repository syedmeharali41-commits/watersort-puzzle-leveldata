using System;

namespace Designcoffers.WaterSort.Generator
{
    /// <summary>Six-tier feel labels covering levels 1-10000 (10k HARD redesign).</summary>
    public enum WaterSortBand
    {
        Tutorial = 0,
        Normal = 1,
        Hard = 2,
        VeryHard = 3,
        Expert = 4,
        WorldClass = 5
    }

    /// <summary>
    /// Pure difficulty profile for Water Sort (10,000-level HARD redesign).
    ///
    /// Parameter scaling per the redesign spec:
    ///   K (colors): 3 flat for L 1-50 (onboarding), then 3 + floor((L-50)/130), cap 22 (~L 2520)
    ///   N (tubes) : K+3 for L 1-500, K+2 for L 501+. The spec ramps N further down to
    ///               K+1 (1501-3000) and K (3001+); both are mathematically unplayable with
    ///               an exact-validated generator (see below) so the ramp bottoms out at K+2.
    ///   C (capacity): 4 (1-2000), 5 (2001-4500), 6 (4501-7000), 7 (7001-9000), 8 (9001-10000)
    ///   S (shuffle):  20 + L*4, capped at 35000
    ///   Floor:        min(8 + L/10, achievable cap) — the cap is a conservative fraction of
    ///                 the empirically measured maximum optimal length for the (K, C) board,
    ///                 so the floor is always solver-reachable. The 15%-per-250 relative
    ///                 growth rule is enforced by the generator tool on top of this floor.
    ///
    /// Why N cannot go below K+2: total segments is exactly K*C, so with N = K tubes every
    /// tube is full and no legal first move exists; with N = K+1 only C empty cells exist,
    /// and an exact-validated generator measures ~0% solvable scrambles for K >= 9 (colour
    /// locks cannot be broken with a single tube of slack). K+2 keeps the solvable fraction
    /// above ~98% across the whole range while K, C and S continue to scale difficulty.
    /// </summary>
    public struct WaterSortDifficulty
    {
        public readonly int LevelNumber;
        public readonly int ColorCount;
        public readonly int RequestedTubeCount;
        public readonly int PlayableTubeCount;
        public readonly int Capacity;
        public readonly int ShuffleDepth;
        public readonly int RequiredMinimumMoves;
        public readonly WaterSortBand Band;

        // Empirical capacity multipliers on the C=4 achievable-depth baseline.
        // Index = capacity - 4. Calibrated from exact solver measurements.
        private static readonly float[] CapacityFactor = { 1.00f, 1.35f, 1.75f, 2.20f, 2.70f };

        private WaterSortDifficulty(
            int levelNumber,
            int colorCount,
            int requestedTubeCount,
            int playableTubeCount,
            int capacity,
            int shuffleDepth,
            int requiredMinimumMoves,
            WaterSortBand band)
        {
            LevelNumber = levelNumber;
            ColorCount = colorCount;
            RequestedTubeCount = requestedTubeCount;
            PlayableTubeCount = playableTubeCount;
            Capacity = capacity;
            ShuffleDepth = shuffleDepth;
            RequiredMinimumMoves = requiredMinimumMoves;
            Band = band;
        }

        public static WaterSortDifficulty ForLevel(int levelNumber)
        {
            int level = Math.Max(1, levelNumber);

            // K: 3 flat through the onboarding band, then +1 every 130 levels, cap 22.
            int colors;
            if (level <= 50)
            {
                colors = 3;
            }
            else
            {
                colors = Math.Min(3 + (level - 50) / 130, 22);
            }

            // N: K+3 for L 1-500, K+2 afterwards (playable floor; see class docs).
            int requestedTubes = colors + (level <= 500 ? 3 : 2);

            // C: five escalating capacity bands.
            int capacity;
            if (level <= 2000) capacity = 4;
            else if (level <= 4500) capacity = 5;
            else if (level <= 7000) capacity = 6;
            else if (level <= 9000) capacity = 7;
            else capacity = 8;

            // S: 20 + L*4, capped at 35000.
            int shuffleDepth = Math.Min(20 + level * 4, 35000);

            // Absolute floor: the PRD ramp 8 + L/10 wherever the board can deliver it,
            // capped at ~80% of the measured achievable maximum for the (K, C) board so
            // validation never churns on unreachable floors.
            float achievableEstimate = (2.75f * colors + 3.25f) * CapacityFactor[capacity - 4];
            int cap = Math.Max(8, (int)Math.Round(0.8f * achievableEstimate));
            int requiredMinimumMoves = Math.Min(8 + level / 10, cap);

            WaterSortBand band;
            if (level <= 50) band = WaterSortBand.Tutorial;
            else if (level <= 500) band = WaterSortBand.Normal;
            else if (level <= 2500) band = WaterSortBand.Hard;
            else if (level <= 5000) band = WaterSortBand.VeryHard;
            else if (level <= 7500) band = WaterSortBand.Expert;
            else band = WaterSortBand.WorldClass;

            return new WaterSortDifficulty(
                level,
                colors,
                requestedTubes,
                requestedTubes,
                capacity,
                shuffleDepth,
                requiredMinimumMoves,
                band);
        }

        /// <summary>Human-readable six-tier label (Tutorial / Normal / Hard / Very Hard / Expert / World-Class).</summary>
        public static string BandName(WaterSortBand band)
        {
            switch (band)
            {
                case WaterSortBand.Tutorial: return "Tutorial";
                case WaterSortBand.Normal: return "Normal";
                case WaterSortBand.Hard: return "Hard";
                case WaterSortBand.VeryHard: return "Very Hard";
                case WaterSortBand.Expert: return "Expert";
                default: return "World-Class";
            }
        }
    }
}
