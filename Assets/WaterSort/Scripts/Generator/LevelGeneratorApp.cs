using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Designcoffers.WaterSort.Data;
using Designcoffers.WaterSort.Generator;

namespace Designcoffers.WaterSort.GeneratorTool
{
    class Program
    {
        static void Main(string[] args)
        {
            // Make redirected output stream to the log file as it is produced.
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

            Console.WriteLine("=========================================================");
            Console.WriteLine(" Water Sort Puzzle — 10,000 Level Generator & Solver (HARD)");
            Console.WriteLine(" Self-enforcing relative validation: checkpoints every 250,");
            Console.WriteLine(" each must be >= 15% harder than the checkpoint before it.");
            Console.WriteLine("=========================================================");

            int totalLevels = 10000;
            int parallelWorkers = Math.Max(1, Environment.ProcessorCount - 1);
            string outputPath = null;
            bool resume = false;
            int startLevel = 1;
            int endLevel = -1;
            string mergeDir = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--out" && i + 1 < args.Length) { outputPath = args[i + 1]; i++; }
                else if (args[i] == "--parallel" && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out parallelWorkers);
                    parallelWorkers = Math.Max(1, parallelWorkers);
                    i++;
                }
                else if (args[i] == "--start" && i + 1 < args.Length) { int.TryParse(args[i + 1], out startLevel); i++; }
                else if (args[i] == "--end" && i + 1 < args.Length) { int.TryParse(args[i + 1], out endLevel); i++; }
                else if (args[i] == "--merge" && i + 1 < args.Length) { mergeDir = args[i + 1]; i++; }
                else if (args[i] == "--resume") { resume = true; }
                else if (int.TryParse(args[i], out int customCount)) { totalLevels = customCount; }
            }

            if (endLevel <= 0) endLevel = totalLevels;

            Console.WriteLine($"Workers: {parallelWorkers} | Total Levels: {totalLevels} | Start: {startLevel} | End: {endLevel} | Resume: {resume} | MergeDir: {mergeDir ?? "None"}");
            Console.WriteLine();

            // ---- Merge Mode Handler ------------------------------------------------------
            if (!string.IsNullOrEmpty(mergeDir))
            {
                RunMergeMode(mergeDir, totalLevels, outputPath);
                return;
            }

            // ---- Phase 0: resume support -------------------------------------------------
            LevelData[] slot = new LevelData[totalLevels];
            int failures = 0;
            if (resume)
            {
                string partialPath = GetPartialPath(outputPath);
                if (File.Exists(partialPath))
                {
                    List<LevelData> partial = ParseLevels(File.ReadAllText(partialPath));
                    int loaded = 0;
                    foreach (LevelData lvl in partial)
                    {
                        if (lvl.levelNumber >= 1 && lvl.levelNumber <= totalLevels && slot[lvl.levelNumber - 1] == null)
                        {
                            slot[lvl.levelNumber - 1] = lvl;
                            loaded++;
                        }
                    }
                    Console.WriteLine($"Resumed: {loaded} previously-generated levels loaded from partial bundle.");
                }
            }

            // ---- Phase A: generate everything (parallel) at the absolute floor -----------
            DateTime startTime = DateTime.Now;
            int[] checkpoints = BuildCheckpoints(totalLevels);
            HashSet<int> checkpointSet = new HashSet<int>(checkpoints);
            Dictionary<int, int> checkpointMoves = new Dictionary<int, int>();
            int completed = 0;
            int lastSaved = 0;
            object gate = new object();

            System.Threading.Tasks.ParallelOptions options = new System.Threading.Tasks.ParallelOptions
            {
                MaxDegreeOfParallelism = parallelWorkers
            };

            System.Threading.Tasks.Parallel.For(startLevel, endLevel + 1, options, L =>
            {
                if (slot[L - 1] != null) // resumed
                {
                    lock (gate)
                    {
                        if (checkpointSet.Contains(L)) checkpointMoves[L] = slot[L - 1].minMoves;
                        completed++;
                    }
                    return;
                }

                try
                {
                    WaterSortDifficulty d = WaterSortDifficulty.ForLevel(L);
                    int attempts, budget;
                    GetComputeBudget(d, out attempts, out budget);
                    // Exact-first everywhere except huge capacity-7/8 fill levels, where the
                    // floor-proof BFS is the primary validator (far cheaper).
                    bool requireExact = d.Capacity < 7;
                    LevelData level = GenerateWithBudget(L, d.RequiredMinimumMoves, requireExact, attempts, budget);
                    slot[L - 1] = level;

                    lock (gate)
                    {
                        if (checkpointSet.Contains(L)) checkpointMoves[L] = level.minMoves;
                        completed++;
                        if (completed - lastSaved >= 250 || completed == totalLevels)
                        {
                            lastSaved = completed;
                            SavePartial(slot, totalLevels, GetPartialPath(outputPath), completed);
                        }
                    }
                    if (L % 100 == 0 || L == totalLevels)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Generated & Validated {Math.Min(completed, L)}/{totalLevels} levels...");
                    }
                }
                catch (Exception ex)
                {
                    lock (gate)
                    {
                        failures++;
                        Console.Error.WriteLine($"[FAILURE] Level {L}: {ex.Message}");
                    }
                }
            });

            // ---- Phase B: 15% relative-growth checkpoint chain (sequential fix-up) -------
            Console.WriteLine();
            Console.WriteLine("=== 15% RELATIVE-GROWTH CHECKPOINT VALIDATION ===");
            int prevMoves = 0;
            List<CheckpointRow> rows = new List<CheckpointRow>();
            for (int c = 0; c < checkpoints.Length; c++)
            {
                int L = checkpoints[c];
                WaterSortDifficulty d = WaterSortDifficulty.ForLevel(L);
                int absFloor = d.RequiredMinimumMoves;
                int relTarget = c == 0 ? absFloor : (int)Math.Round(prevMoves * 1.15);
                int effTarget = Math.Max(absFloor, relTarget);

                LevelData current = slot[L - 1];
                bool regenerated = false;
                bool warned = false;

                if (current == null || current.minMoves < effTarget)
                {
                    // Escalate: try harder to reach the relative target.
                    int attempts, budget;
                    GetComputeBudget(d, out attempts, out budget);
                    attempts = Math.Max(attempts * 2, 160);
                    LevelData raised = null;
                    for (int r = 0; r < 3 && raised == null; r++)
                    {
                        try { raised = GenerateWithBudget(L, effTarget, true, attempts, budget); } // checkpoints: exact-first
                        catch { raised = null; }
                        attempts = (int)(attempts * 1.5);
                    }
                    if (raised != null && raised.minMoves >= effTarget)
                    {
                        slot[L - 1] = raised;
                        current = raised;
                        regenerated = true;
                    }
                    else if (raised != null)
                    {
                        // Best effort: keep whichever of the two is harder so the chain never dips.
                        if (current == null || raised.minMoves > current.minMoves)
                        {
                            slot[L - 1] = raised;
                            current = raised;
                        }
                        warned = true;
                    }
                    else if (current == null)
                    {
                        Console.Error.WriteLine($"[FAILURE] Checkpoint {L} could not be generated at target {effTarget}.");
                        failures++;
                        continue;
                    }
                    else
                    {
                        warned = true; // kept the lower original
                    }
                }

                int moves = current.minMoves;
                double growthPct = prevMoves > 0 ? ((moves - prevMoves) * 100.0 / prevMoves) : 0.0;
                bool met = moves >= effTarget;
                if (!met) warned = true;
                if (warned)
                {
                    Console.WriteLine($"[WARN ] Checkpoint {L,5}: target={effTarget}, achieved={moves} — below 15% relative growth or relative target; physical cap reached or escalation exhausted.");
                }
                Console.WriteLine($"[Checkpoint] L={L,5}  K={d.ColorCount,2}  N={d.PlayableTubeCount,2}  C={d.Capacity}  S={d.ShuffleDepth,5}  Min={moves,3}  AbsFloor={absFloor,3}  RelTarget={relTarget,3}  GrowthVsPrev={growthPct,5:F0}%  Exact={current.validationExact}  Locked={current.lockedTubes.Count}  Attempts={current.generationAttempts}");
                prevMoves = moves;
                rows.Add(new CheckpointRow { Level = L, MinMoves = moves, GrowthPct = growthPct, Met = met, AbsFloor = absFloor, RelTarget = relTarget, Exact = current.validationExact });
                checkpointMoves[L] = moves;

                if (c % 5 == 0)
                {
                    SavePartial(slot, totalLevels, GetPartialPath(outputPath), completed);
                }
            }

            // ---- Phase C: assemble + serialize the final bundle --------------------------
            LevelBundle bundle = new LevelBundle();
            bundle.levels = new List<LevelData>(totalLevels);
            for (int i = 0; i < totalLevels; i++)
            {
                if (slot[i] == null)
                {
                    failures++;
                    Console.Error.WriteLine($"[FAILURE] Level {i + 1} was not validated.");
                }
                else
                {
                    bundle.levels.Add(slot[i]);
                }
            }

            TimeSpan elapsed = DateTime.Now - startTime;
            Console.WriteLine();
            Console.WriteLine($"Completed {totalLevels - failures}/{totalLevels} level generation in {elapsed.TotalMinutes:F1} minutes (failures: {failures}).");

            string json = SimpleJsonSerializer.SerializeBundle(bundle);
            if (outputPath == null)
            {
                string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Assets", "Resources");
                if (!Directory.Exists(targetDir)) targetDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Resources");
                Directory.CreateDirectory(targetDir);
                outputPath = Path.Combine(targetDir, "levels.json");
            }
            string outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
            File.WriteAllText(outputPath, json, new UTF8Encoding(true));
            File.Delete(GetPartialPath(outputPath));

            Console.WriteLine($"Successfully saved {bundle.levels.Count} levels to: {outputPath}");
            Console.WriteLine();
            Console.WriteLine("=== CHECKPOINT TABLE (every 250 levels) ===");
            Console.WriteLine("  Level   MinMoves   AbsFloor   RelTarget   Growth%   Met15%   Exact");
            foreach (CheckpointRow row in rows)
            {
                Console.WriteLine($"{row.Level,7} {row.MinMoves,9} {row.AbsFloor,9} {row.RelTarget,10} {row.GrowthPct,8:F0}   {(row.Met ? "YES" : "NO ")}     {row.Exact}");
            }
            Console.WriteLine("=========================================================");
        }

        private static void RunMergeMode(string mergeDir, int totalLevels, string outputPath)
        {
            Console.WriteLine($"=== MERGE MODE: Loading chunk files from {mergeDir} ===");
            LevelData[] slot = new LevelData[totalLevels];
            string[] files = Directory.GetFiles(mergeDir, "*.json", SearchOption.AllDirectories);
            int totalLoaded = 0;
            foreach (string file in files)
            {
                string filename = Path.GetFileName(file);
                if (filename.Equals("levels.json", StringComparison.OrdinalIgnoreCase) || filename.Equals("levels.partial.json", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    List<LevelData> chunk = ParseLevels(File.ReadAllText(file));
                    foreach (LevelData lvl in chunk)
                    {
                        if (lvl.levelNumber >= 1 && lvl.levelNumber <= totalLevels)
                        {
                            if (slot[lvl.levelNumber - 1] == null)
                            {
                                slot[lvl.levelNumber - 1] = lvl;
                                totalLoaded++;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARN] Could not parse chunk {file}: {ex.Message}");
                }
            }
            Console.WriteLine($"Loaded {totalLoaded}/{totalLevels} levels across chunk files.");

            Console.WriteLine();
            Console.WriteLine("=== 15% RELATIVE-GROWTH CHECKPOINT VALIDATION ===");
            int[] checkpoints = BuildCheckpoints(totalLevels);
            int prevMoves = 0;
            int failures = 0;
            List<CheckpointRow> rows = new List<CheckpointRow>();
            for (int c = 0; c < checkpoints.Length; c++)
            {
                int L = checkpoints[c];
                WaterSortDifficulty d = WaterSortDifficulty.ForLevel(L);
                int absFloor = d.RequiredMinimumMoves;
                int relTarget = c == 0 ? absFloor : (int)Math.Round(prevMoves * 1.15);
                int effTarget = Math.Max(absFloor, relTarget);

                LevelData current = slot[L - 1];
                bool warned = false;

                if (current == null || current.minMoves < effTarget)
                {
                    int attempts, budget;
                    GetComputeBudget(d, out attempts, out budget);
                    attempts = Math.Max(attempts * 2, 160);
                    LevelData raised = null;
                    for (int r = 0; r < 3 && raised == null; r++)
                    {
                        try { raised = GenerateWithBudget(L, effTarget, true, attempts, budget); }
                        catch { raised = null; }
                        attempts = (int)(attempts * 1.5);
                    }
                    if (raised != null && raised.minMoves >= effTarget)
                    {
                        slot[L - 1] = raised;
                        current = raised;
                    }
                    else if (raised != null)
                    {
                        if (current == null || raised.minMoves > current.minMoves)
                        {
                            slot[L - 1] = raised;
                            current = raised;
                        }
                        warned = true;
                    }
                    else if (current == null)
                    {
                        Console.Error.WriteLine($"[FAILURE] Checkpoint {L} could not be generated at target {effTarget}.");
                        failures++;
                        continue;
                    }
                    else warned = true;
                }

                int moves = current.minMoves;
                double growthPct = prevMoves > 0 ? ((moves - prevMoves) * 100.0 / prevMoves) : 0.0;
                bool met = moves >= effTarget;
                if (!met) warned = true;
                if (warned)
                {
                    Console.WriteLine($"[WARN ] Checkpoint {L,5}: target={effTarget}, achieved={moves} — physical cap reached or escalation exhausted.");
                }
                Console.WriteLine($"[Checkpoint] L={L,5}  K={d.ColorCount,2}  N={d.PlayableTubeCount,2}  C={d.Capacity}  S={d.ShuffleDepth,5}  Min={moves,3}  AbsFloor={absFloor,3}  RelTarget={relTarget,3}  GrowthVsPrev={growthPct,5:F0}%  Exact={current.validationExact}");
                prevMoves = moves;
                rows.Add(new CheckpointRow { Level = L, MinMoves = moves, GrowthPct = growthPct, Met = met, AbsFloor = absFloor, RelTarget = relTarget, Exact = current.validationExact });
            }

            LevelBundle bundle = new LevelBundle();
            bundle.levels = new List<LevelData>(totalLevels);
            for (int i = 0; i < totalLevels; i++)
            {
                if (slot[i] == null)
                {
                    failures++;
                    Console.Error.WriteLine($"[FAILURE] Level {i + 1} was missing.");
                }
                else bundle.levels.Add(slot[i]);
            }

            string json = SimpleJsonSerializer.SerializeBundle(bundle);
            if (outputPath == null)
            {
                string targetDir = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Resources");
                Directory.CreateDirectory(targetDir);
                outputPath = Path.Combine(targetDir, "levels.json");
            }
            string outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
            File.WriteAllText(outputPath, json, new UTF8Encoding(true));
            Console.WriteLine($"Successfully saved merged bundle ({bundle.levels.Count} levels) to: {outputPath}");
        }

        private class CheckpointRow
        {
            public int Level;
            public int MinMoves;
            public int AbsFloor;
            public int RelTarget;
            public double GrowthPct;
            public bool Met;
            public bool Exact;
        }

        private static int[] BuildCheckpoints(int totalLevels)
        {
            List<int> cps = new List<int>();
            int L = 1;
            while (L <= totalLevels)
            {
                cps.Add(L);
                if (L == 1) L = 250;
                else L += 250;
            }
            if (cps[cps.Count - 1] != totalLevels && totalLevels > 250) cps.Add(totalLevels);
            return cps.ToArray();
        }

        private static void GetComputeBudget(WaterSortDifficulty d, out int attempts, out int maxSolverStates)
        {
            // Budgets sized for deep, fully-scrambled boards (S = 20 + 4L). Exact A* on a
            // K=14+ board can exceed a few hundred thousand states; too small a budget makes
            // solvable deep boards look unreachable and churns regeneration forever.
            switch (d.Capacity)
            {
                case 4: attempts = 48; maxSolverStates = 1500000; break;
                case 5: attempts = 40; maxSolverStates = 2000000; break;
                case 6: attempts = 32; maxSolverStates = 2500000; break;
                case 7: attempts = 28; maxSolverStates = 2000000; break;
                default: attempts = 24; maxSolverStates = 2500000; break;
            }
        }

        private static LevelData GenerateWithBudget(int levelNumber, int requiredMoves, bool requireExact, int attempts, int budget)
        {
            WaterSortGenerationResult result;
            if (!WaterSortGeneratorEngine.TryGenerateValidatedLevel(levelNumber, requiredMoves, requireExact, out result, attempts, budget))
            {
                throw new InvalidOperationException(result.FailureReason);
            }
            return result.Level;
        }

        private static string GetPartialPath(string outputPath)
        {
            if (string.IsNullOrEmpty(outputPath) || outputPath.EndsWith("levels.json", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(Directory.GetCurrentDirectory(), "levels.partial.json");
            }
            return outputPath + ".partial";
        }

        private static void SavePartial(LevelData[] slot, int totalLevels, string partialPath, int completed)
        {
            try
            {
                LevelBundle pb = new LevelBundle();
                pb.levels = new List<LevelData>();
                for (int i = 0; i < totalLevels; i++)
                {
                    if (slot[i] != null) pb.levels.Add(slot[i]);
                }
                File.WriteAllText(partialPath, SimpleJsonSerializer.SerializeBundle(pb), new UTF8Encoding(true));
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Partial save: {pb.levels.Count} levels -> {partialPath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WARN] Partial save failed: {ex.Message}");
            }
        }

        /// <summary>Minimal JSON parser used only to reload the partial bundle on --resume.</summary>
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
                int veIdx = json.IndexOf("\"validationExact\":", pos);
                if (veIdx > 0 && veIdx < json.IndexOf("\"tubes\":", pos))
                {
                    lvl.validationExact = json.IndexOf("true", veIdx) >= 0 && (json.IndexOf("false", veIdx) < 0 || json.IndexOf("true", veIdx) < json.IndexOf("false", veIdx));
                }

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

    /// <summary>Lightweight JSON serializer for LevelBundle without external assembly dependencies.</summary>
    public static class SimpleJsonSerializer
    {
        public static string SerializeBundle(LevelBundle bundle)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n  \"levels\": [\n");

            for (int i = 0; i < bundle.levels.Count; i++)
            {
                var lvl = bundle.levels[i];
                sb.Append("    {\n");
                sb.Append($"      \"levelNumber\": {lvl.levelNumber},\n");
                sb.Append($"      \"colorCount\": {lvl.colorCount},\n");
                sb.Append($"      \"tubeCount\": {lvl.tubeCount},\n");
                sb.Append($"      \"capacity\": {lvl.capacity},\n");
                sb.Append($"      \"minMoves\": {lvl.minMoves},\n");
                sb.Append($"      \"requiredMinMoves\": {lvl.requiredMinMoves},\n");
                sb.Append($"      \"validationExact\": {(lvl.validationExact ? "true" : "false")},\n");
                sb.Append($"      \"generationSeed\": {lvl.generationSeed},\n");
                sb.Append($"      \"solverStatesExplored\": {lvl.solverStatesExplored},\n");
                sb.Append($"      \"generationAttempts\": {lvl.generationAttempts},\n");
                sb.Append($"      \"lockDuration\": {lvl.lockDuration},\n");
                sb.Append("      \"lockedTubes\": [");
                if (lvl.lockedTubes != null && lvl.lockedTubes.Count > 0)
                {
                    sb.Append(string.Join(", ", lvl.lockedTubes));
                }
                sb.Append("],\n");
                sb.Append("      \"tubes\": [\n");

                for (int t = 0; t < lvl.tubes.Count; t++)
                {
                    var tube = lvl.tubes[t];
                    sb.Append("        [");
                    sb.Append(string.Join(", ", tube));
                    sb.Append("]");
                    if (t < lvl.tubes.Count - 1) sb.Append(",");
                    sb.Append("\n");
                }

                sb.Append("      ]\n");
                sb.Append("    }");
                if (i < bundle.levels.Count - 1) sb.Append(",");
                sb.Append("\n");
            }

            sb.Append("  ]\n}");
            return sb.ToString();
        }
    }
}
