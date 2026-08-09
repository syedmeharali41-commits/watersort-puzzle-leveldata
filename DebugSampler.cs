using System;
using System.Collections.Generic;
using System.IO;
using Designcoffers.WaterSort.Data;
using Designcoffers.WaterSort.Generator;

namespace Designcoffers.WaterSort.Sampler
{
    /// <summary>
    /// Lock-constraint verifier: loads the generated bundle (or partial), picks locked
    /// Expert/World-Class levels across capacities, and solves each WITH the locked-tube
    /// lever active to prove the level stays solvable and harder than its stored floor.
    /// </summary>
    class DebugSampler
    {
        static void Main(string[] args)
        {
            Console.WriteLine("==== LOCKED-TUBE LEVER VERIFICATION ====");
            string path = "levels.partial.json";
            if (args.Length > 0) path = args[0];
            if (!File.Exists(path))
            {
                Console.WriteLine($"No bundle at {path}");
                return;
            }

            List<LevelData> levels = ParseLevels(File.ReadAllText(path));
            Console.WriteLine($"Loaded {levels.Count} levels.");

            List<LevelData> locked = new List<LevelData>();
            foreach (LevelData lvl in levels)
            {
                if (lvl.lockedTubes != null && lvl.lockedTubes.Count > 0) locked.Add(lvl);
            }
            Console.WriteLine($"Locked levels found: {locked.Count}\n");

            // Sample one locked level per capacity (6, 7, 8) across the band.
            int[] targets = { 5001, 7501, 9001 };
            for (int t = 0; t < targets.Length; t++)
            {
                int want = targets[t];
                LevelData pick = null;
                int bestDelta = int.MaxValue;
                foreach (LevelData lvl in locked)
                {
                    int delta = Math.Abs(lvl.levelNumber - want);
                    if (delta < bestDelta) { bestDelta = delta; pick = lvl; }
                }
                if (pick == null) continue;

                Console.WriteLine($"--- L{pick.levelNumber} K={pick.colorCount} N={pick.tubeCount} C={pick.capacity} locked={string.Join(",", pick.lockedTubes)} dur={pick.lockDuration} storedMin={pick.minMoves} floor={pick.requiredMinMoves} ---");
                DateTime start = DateTime.Now;
                WaterSortSolveResult res;
                bool solved = WaterSortSolver.TrySolveOptimalLocked(pick, 800000, out res);
                double secs = (DateTime.Now - start).TotalSeconds;
                if (solved && !res.ReachedSearchLimit)
                {
                    bool harderThanUnlocked = res.OptimalMoveCount >= pick.minMoves;
                    bool meetsFloor = res.OptimalMoveCount >= pick.requiredMinMoves;
                    Console.WriteLine($"  LOCKED-OPTIMAL={res.OptimalMoveCount} (states={res.StatesExplored}, {secs:F1}s)  >= storedMin={pick.minMoves}: {harderThanUnlocked}  >= floor: {meetsFloor}");
                }
                else if (res.ReachedSearchLimit)
                {
                    Console.WriteLine($"  SEARCH LIMIT at 800k ({secs:F1}s) - cannot confirm under budget");
                }
                else
                {
                    Console.WriteLine($"  UNSOLVABLE under lock constraint ({secs:F1}s)!!");
                }
            }
            Console.WriteLine("\n==== DONE ====");
        }

        /// <summary>Minimal parser for the generator's JSON output (levels array).</summary>
        public static List<LevelData> ParseLevels(string json)
        {
            List<LevelData> list = new List<LevelData>();
            string marker = "\"levelNumber\":";
            int pos = json.IndexOf(marker);
            while (pos >= 0)
            {
                LevelData lvl = new LevelData();
                lvl.levelNumber = NextFieldInt(json, "levelNumber", ref pos);
                lvl.colorCount = NextFieldInt(json, "colorCount", ref pos);
                lvl.tubeCount = NextFieldInt(json, "tubeCount", ref pos);
                lvl.capacity = NextFieldInt(json, "capacity", ref pos);
                lvl.minMoves = NextFieldInt(json, "minMoves", ref pos);
                lvl.requiredMinMoves = NextFieldInt(json, "requiredMinMoves", ref pos);
                lvl.lockDuration = NextFieldInt(json, "lockDuration", ref pos);

                lvl.tubes = new List<List<int>>();
                int tubesStart = json.IndexOf("\"tubes\": [", pos);
                if (tubesStart > 0)
                {
                    int cursor = tubesStart + "\"tubes\": [".Length;
                    while (true)
                    {
                        int open = json.IndexOf('[', cursor);
                        if (open < 0) break;
                        int close = json.IndexOf(']', open);
                        if (close < 0) break;
                        string inner = json.Substring(open + 1, close - open - 1).Trim();
                        List<int> tube = new List<int>();
                        if (inner.Length > 0)
                        {
                            string[] parts = inner.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < parts.Length; i++)
                            {
                                int colorVal;
                                if (int.TryParse(parts[i].Trim(), out colorVal)) tube.Add(colorVal);
                            }
                        }
                        lvl.tubes.Add(tube);
                        cursor = close + 1;
                        int nextOpen = json.IndexOf('[', cursor);
                        int nextLevel = json.IndexOf(marker, close);
                        if (nextLevel >= 0 && (nextOpen < 0 || nextLevel < nextOpen)) break;
                    }
                }

                // lockedTubes field
                int ltIdx = json.IndexOf("\"lockedTubes\": [", pos);
                if (ltIdx > 0 && ltIdx < json.IndexOf("\"tubes\": [", pos))
                {
                    int ltStart = ltIdx + "\"lockedTubes\": [".Length;
                    int ltEnd = json.IndexOf(']', ltStart);
                    if (ltEnd > ltStart)
                    {
                        string inner = json.Substring(ltStart, ltEnd - ltStart).Trim();
                        if (inner.Length > 0)
                        {
                            string[] parts = inner.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string p in parts)
                            {
                                int v;
                                if (int.TryParse(p.Trim(), out v)) lvl.lockedTubes.Add(v);
                            }
                        }
                    }
                }

                list.Add(lvl);
                pos = json.IndexOf(marker, pos + marker.Length);
            }
            return list;
        }

        private static int NextFieldInt(string json, string field, ref int pos)
        {
            int idx = json.IndexOf("\"" + field + "\":", pos);
            if (idx < 0) return 0;
            int start = idx + field.Length + 3;
            int end = start;
            while (end < json.Length && json[end] != ',' && json[end] != '\n' && json[end] != '\r') end++;
            int value;
            int.TryParse(json.Substring(start, end - start).Trim(), out value);
            pos = end;
            return value;
        }
    }
}
