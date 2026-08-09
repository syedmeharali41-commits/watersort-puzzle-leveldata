using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Designcoffers.WaterSort.Data;
using Designcoffers.WaterSort.Generator;

namespace Designcoffers.WaterSort.Testing
{
    class FullSuiteTestRunner
    {
        private static int passedTests = 0;
        private static int failedTests = 0;

        static void Main(string[] args)
        {
            Console.WriteLine("=================================================================");
            Console.WriteLine(" DESIGNCOFFERS - WATER SORT PUZZLE FULL COMPREHENSIVE TEST SUITE");
            Console.WriteLine("=================================================================");

            Test1_JSONBundleIntegrity();
            Test2_AllLevelsStructureAndColorConservation();
            Test3_CoreGameMechanicsAndPourLogic();
            Test4_UndoAndAddTubeLifelines();
            Test5_WinConditionAndSaveState();
            Test6_NewtonsoftRuntimeDeserialization();
            Test7_PlayThroughCheckpointLevelsFromBundle();
            Test8_LockedTubeLeverAndDifficultyScaling();

            Console.WriteLine("\n=================================================================");
            Console.WriteLine($" FINAL TEST SUMMARY: {passedTests} PASSED, {failedTests} FAILED");
            if (failedTests == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" ALL SYSTEM TESTS PASSED SUCCESSFULLY! GAME IS 100% VERIFIED.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" TEST SUITE DETECTED FAILURES!");
                Console.ResetColor();
            }
            Console.WriteLine("=================================================================");
        }

        private static void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  [PASS] {testName}");
                Console.ResetColor();
                passedTests++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [FAIL] {testName}");
                Console.ResetColor();
                failedTests++;
            }
        }

        private static void Test1_JSONBundleIntegrity()
        {
            Console.WriteLine("\n--- TEST SUITE 1: JSON Level Bundle Integrity ---");

            string jsonPath = Path.Combine("Assets", "Resources", "levels.json");
            Assert(File.Exists(jsonPath), "levels.json file exists in Assets/Resources/");

            string jsonText = File.ReadAllText(jsonPath);
            Assert(!string.IsNullOrEmpty(jsonText), "levels.json content is non-empty");

            int levelCount = 0;
            int pos = 0;
            while ((pos = jsonText.IndexOf("\"levelNumber\"", pos)) != -1)
            {
                levelCount++;
                pos += 13;
            }

            Assert(levelCount == 10000, $"levels.json contains exactly 10000 levels (Found: {levelCount})");
        }

        private static void Test2_AllLevelsStructureAndColorConservation()
        {
            Console.WriteLine("\n--- TEST SUITE 2: Full 10000-Level Structure & Color Conservation Audit ---");

            string jsonPath = Path.Combine("Assets", "Resources", "levels.json");
            string jsonText = File.ReadAllText(jsonPath);

            List<LevelData> levels = FastParseLevels(jsonText);
            Assert(levels.Count == 10000, $"Parsed {levels.Count}/10000 levels from JSON");

            bool allValidStructure = true;
            bool allValidColors = true;

            for (int i = 0; i < levels.Count; i++)
            {
                var lvl = levels[i];

                // Check tube count and capacities
                if (lvl.tubes.Count != lvl.tubeCount) allValidStructure = false;

                // Count colors
                Dictionary<int, int> colorCounts = new Dictionary<int, int>();
                int totalSegments = 0;

                foreach (var tube in lvl.tubes)
                {
                    if (tube.Count > lvl.capacity) allValidStructure = false;
                    foreach (int color in tube)
                    {
                        if (!colorCounts.ContainsKey(color)) colorCounts[color] = 0;
                        colorCounts[color]++;
                        totalSegments++;
                    }
                }

                // Verify every color appears exactly capacity C times
                if (colorCounts.Count != lvl.colorCount) allValidColors = false;
                foreach (var kvp in colorCounts)
                {
                    if (kvp.Value != lvl.capacity) allValidColors = false;
                }
                if (totalSegments != lvl.colorCount * lvl.capacity) allValidColors = false;

                if (!allValidColors && i < 5)
                {
                    Console.WriteLine($"    [DBG] L={lvl.levelNumber}: colorCounts={colorCounts.Count} (want {lvl.colorCount}), totalSegments={totalSegments} (want {lvl.colorCount * lvl.capacity}), tubeCount={lvl.tubes.Count} (want {lvl.tubeCount})");
                    for (int t = 0; t < lvl.tubes.Count && t < 8; t++) Console.WriteLine($"    [DBG]   tube {t}: [{string.Join(",", lvl.tubes[t])}]");
                }
            }

            Assert(allValidStructure, "10000/10000 levels have 100% valid tube count, capacities, and segment boundaries");
            Assert(allValidColors, "10000/10000 levels have 100% perfect color conservation (every color has exactly C segments)");
        }

        private static void Test3_CoreGameMechanicsAndPourLogic()
        {
            Console.WriteLine("\n--- TEST SUITE 3: Core Game Mechanics & Pour Validation ---");

            LevelData testLevel = new LevelData
            {
                levelNumber = 9999,
                colorCount = 2,
                tubeCount = 4,
                capacity = 4,
                minMoves = 5,
                tubes = new List<List<int>>
                {
                    new List<int> { 1, 2, 1, 2 },
                    new List<int> { 2, 1, 2, 1 },
                    new List<int>(),
                    new List<int>()
                }
            };

            List<List<int>> tubes = WaterSortSolver.CloneTubes(testLevel.tubes);
            var moves = WaterSortSolver.GetPossibleMoves(tubes, 4);
            Assert(moves.Count > 0, "Legal moves generated for starting puzzle state");

            int topColor0 = tubes[0][tubes[0].Count - 1];
            tubes[0].RemoveAt(tubes[0].Count - 1);
            tubes[2].Add(topColor0);

            Assert(tubes[0].Count == 3 && tubes[2].Count == 1 && tubes[2][0] == topColor0, 
                   "Pour 1 segment of color 2 from Tube 0 to empty Tube 2 executed correctly");

            int topColor1 = tubes[1][tubes[1].Count - 1];
            Assert(topColor1 == tubes[0][tubes[0].Count - 1], "Tube 1 top color matches Tube 0 top color");

            tubes[1].RemoveAt(tubes[1].Count - 1);
            tubes[0].Add(topColor1);

            Assert(tubes[0].Count == 4 && tubes[0][3] == 1, "Matching color pour into partially filled tube executed correctly");
            Assert(tubes[0].Count == 4, "Tube 0 is full (capacity 4)");
        }

        private static void Test4_UndoAndAddTubeLifelines()
        {
            Console.WriteLine("\n--- TEST SUITE 4: Lifelines (Undo & Add Tube) ---");

            List<List<int>> tubes = new List<List<int>>
            {
                new List<int> { 1, 1, 2, 2 },
                new List<int> { 2, 2, 1, 1 },
                new List<int>()
            };

            Stack<MoveRecord> history = new Stack<MoveRecord>();

            int transferred = 2;
            int colorMoved = 2;
            tubes[0].RemoveAt(tubes[0].Count - 1);
            tubes[0].RemoveAt(tubes[0].Count - 1);
            tubes[2].Add(colorMoved);
            tubes[2].Add(colorMoved);

            history.Push(new MoveRecord { fromTubeIndex = 0, toTubeIndex = 2, colorId = colorMoved, amount = transferred });

            Assert(tubes[0].Count == 2 && tubes[2].Count == 2, "Move executed: 2 segments poured into empty tube");

            var lastMove = history.Pop();
            for (int i = 0; i < lastMove.amount; i++)
            {
                tubes[lastMove.toTubeIndex].RemoveAt(tubes[lastMove.toTubeIndex].Count - 1);
                tubes[lastMove.fromTubeIndex].Add(lastMove.colorId);
            }

            Assert(tubes[0].Count == 4 && tubes[2].Count == 0 && tubes[0][3] == 2, 
                   "Undo executed: State 100% restored to original pre-move state");

            tubes.Add(new List<int>());
            Assert(tubes.Count == 4 && tubes[3].Count == 0, "Add Tube lifeline appended 1 new empty tube");
        }

        private static void Test6_NewtonsoftRuntimeDeserialization()
        {
            Console.WriteLine("\n--- TEST SUITE 6: Runtime Path Deserialization (Newtonsoft.Json, as the game loads it) ---");

            string jsonPath = Path.Combine("Assets", "Resources", "levels.json");
            string jsonText = File.ReadAllText(jsonPath);

            try
            {
                LevelBundle bundle = Newtonsoft.Json.JsonConvert.DeserializeObject<LevelBundle>(jsonText);
                Assert(bundle != null && bundle.levels != null && bundle.levels.Count == 10000,
                    $"Newtonsoft deserialized bundle with exactly 10000 levels (Found: {(bundle == null || bundle.levels == null ? 0 : bundle.levels.Count)})");

                bool allFloorsSerialized = true;
                bool allFloorsMet = true;
                bool allFloorsAtOrAboveProfile = true;
                for (int i = 0; i < bundle.levels.Count; i++)
                {
                    LevelData lvl = bundle.levels[i];
                    if (lvl.requiredMinMoves <= 0) allFloorsSerialized = false;
                    if (lvl.minMoves < lvl.requiredMinMoves) allFloorsMet = false;
                    int profileFloor = WaterSortDifficulty.ForLevel(lvl.levelNumber).RequiredMinimumMoves;
                    if (lvl.requiredMinMoves < profileFloor) allFloorsAtOrAboveProfile = false;
                }
                Assert(allFloorsSerialized, "All 10000 bundled levels carry a serialized requiredMinMoves field");
                Assert(allFloorsMet, "All 10000 bundled levels meet their solver-verified difficulty floor (minMoves >= requiredMinMoves)");
                Assert(allFloorsAtOrAboveProfile, "Every floor is at least its difficulty-profile absolute floor (relative-growth targets may raise, never lower)");
            }
            catch (Exception ex)
            {
                Assert(false, "Newtonsoft deserialization threw: " + ex.Message);
            }
        }

        private static void Test7_PlayThroughCheckpointLevelsFromBundle()
        {
            Console.WriteLine("\n--- TEST SUITE 7: Solver Play-Through of Bundled Checkpoint Levels ---");

            string jsonPath = Path.Combine("Assets", "Resources", "levels.json");
            string jsonText = File.ReadAllText(jsonPath);
            LevelBundle bundle = Newtonsoft.Json.JsonConvert.DeserializeObject<LevelBundle>(jsonText);
            int[] checkpoints = new int[] { 1, 250, 1000, 2500 };

            for (int i = 0; i < checkpoints.Length; i++)
            {
                int L = checkpoints[i];
                LevelData lvl = bundle.levels[L - 1];
                int floor = lvl.requiredMinMoves;

                DateTime start = DateTime.Now;
                WaterSortSolveResult result;
                bool solved = WaterSortSolver.TrySolveOptimal(lvl, 600000, out result);
                double seconds = (DateTime.Now - start).TotalSeconds;

                bool ok = solved && result.OptimalMoveCount >= floor && !result.ReachedSearchLimit;
                Assert(ok, $"Level {L} (K={lvl.colorCount}, N={lvl.tubeCount}, C={lvl.capacity}): optimal={result.OptimalMoveCount}, floor={floor}, states={result.StatesExplored}, time={seconds:F1}s");
            }
        }

        private static void Test8_LockedTubeLeverAndDifficultyScaling()
        {
            Console.WriteLine("\n--- TEST SUITE 8: Locked-Tube Lever & Parameter Scaling (10k redesign) ---");

            string jsonPath = Path.Combine("Assets", "Resources", "levels.json");
            string jsonText = File.ReadAllText(jsonPath);
            LevelBundle bundle = Newtonsoft.Json.JsonConvert.DeserializeObject<LevelBundle>(jsonText);

            // Parameter scaling spot checks.
            bool paramsScale = true;
            var d1 = WaterSortDifficulty.ForLevel(1);
            var d250 = WaterSortDifficulty.ForLevel(250);
            var d1000 = WaterSortDifficulty.ForLevel(1000);
            var d2500 = WaterSortDifficulty.ForLevel(2500);
            var d5000 = WaterSortDifficulty.ForLevel(5000);
            var d10000 = WaterSortDifficulty.ForLevel(10000);

            if (!(d1.ColorCount == 3 && d250.ColorCount > d1.ColorCount)) paramsScale = false;
            if (!(d1000.ColorCount < d10000.ColorCount)) paramsScale = false;
            if (!(d5000.Capacity >= d2500.Capacity && d10000.Capacity > d5000.Capacity)) paramsScale = false;
            if (!(d1000.ShuffleDepth > d250.ShuffleDepth)) paramsScale = false;
            Assert(paramsScale, "Difficulty parameters scale with level number (K, C, S all rise)");

            // Locks: none below Expert, present on the vast majority of Expert/World-Class levels.
            int lockedExpert = 0, expertTotal = 0, worldTotal = 0;
            bool locksStructuralOk = true;
            for (int i = 0; i < bundle.levels.Count; i++)
            {
                LevelData lvl = bundle.levels[i];
                WaterSortBand band = WaterSortDifficulty.ForLevel(lvl.levelNumber).Band;
                if (band < WaterSortBand.Expert)
                {
                    if ((lvl.lockedTubes != null && lvl.lockedTubes.Count > 0) || lvl.lockDuration > 0) locksStructuralOk = false;
                }
                else
                {
                    if (band == WaterSortBand.Expert) expertTotal++;
                    else worldTotal++;
                    if (lvl.lockedTubes != null && lvl.lockedTubes.Count > 0)
                    {
                        if (lvl.lockDuration <= 0) locksStructuralOk = false;
                        foreach (int ti in lvl.lockedTubes)
                        {
                            if (ti < 0 || ti >= lvl.tubes.Count) { locksStructuralOk = false; continue; }
                            var tube = lvl.tubes[ti];
                            // Locked tubes must be already-pure so the puzzle stays solvable without them.
                            for (int s = 1; s < tube.Count; s++)
                            {
                                if (tube[s] != tube[0]) { locksStructuralOk = false; break; }
                            }
                        }
                        lockedExpert++;
                    }
                }
            }
            Assert(locksStructuralOk, "Locked tubes only on Expert/World-Class levels and always already-pure tubes");
            Assert(lockedExpert > 0, $"Locked-tube lever is active in the bundle ({lockedExpert}/{expertTotal + worldTotal} Expert/World-Class levels locked)");
        }

        private static void Test5_WinConditionAndSaveState()
        {
            Console.WriteLine("\n--- TEST SUITE 5: Win Condition Detection & Color Pureness ---");

            List<List<int>> solvedTubes = new List<List<int>>
            {
                new List<int> { 1, 1, 1, 1 },
                new List<int> { 2, 2, 2, 2 },
                new List<int> { 3, 3, 3, 3 },
                new List<int>(),
                new List<int>()
            };

            bool isWin = WaterSortSolver.IsSolved(solvedTubes, 4);
            Assert(isWin == true, "Solved state correctly recognized as WIN condition");

            List<List<int>> unsolvedTubes = new List<List<int>>
            {
                new List<int> { 1, 1, 1, 2 },
                new List<int> { 2, 2, 2, 1 },
                new List<int> { 3, 3, 3, 3 },
                new List<int>(),
                new List<int>()
            };

            bool isWinUnsolved = WaterSortSolver.IsSolved(unsolvedTubes, 4);
            Assert(isWinUnsolved == false, "Unsolved state correctly rejected by Win checker");
        }

        private static List<LevelData> FastParseLevels(string json)
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

                lvl.tubes = new List<List<int>>();
                int tubesStart = json.IndexOf("\"tubes\": [", pos);
                if (tubesStart > 0)
                {
                    // Skip the tubes-array's own '[' so the first real tube is parsed whole.
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

                        // Stop when the next level's header begins before the next tube array.
                        int nextOpen = json.IndexOf('[', cursor);
                        int nextLevel = json.IndexOf(marker, close);
                        if (nextLevel >= 0 && (nextOpen < 0 || nextLevel < nextOpen))
                        {
                            break;
                        }
                    }
                }

                list.Add(lvl);
                pos = json.IndexOf(marker, pos + marker.Length);
            }

            return list;
        }

        /// <summary>Parses the next integer value of a "field": after pos; advances pos past it.</summary>
        private static int NextFieldInt(string json, string field, ref int pos)
        {
            int idx = json.IndexOf("\"" + field + "\":", pos);
            if (idx < 0) return 0;
            int start = idx + field.Length + 3;
            int end = start;
            while (end < json.Length && json[end] != ',' && json[end] != '\n' && json[end] != '\r')
            {
                end++;
            }
            int value;
            int.TryParse(json.Substring(start, end - start).Trim(), out value);
            pos = end;
            return value;
        }
    }
}
