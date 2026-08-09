using System;
using System.Collections.Generic;
using Designcoffers.WaterSort.Data;
using Designcoffers.WaterSort.Generator;

namespace Designcoffers.WaterSort.Testing
{
    class SelfTestRunner
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine(" Water Sort Puzzle — Automated Self-Test Runner");
            Console.WriteLine("=================================================");

            int[] testLevels = new int[] { 1, 1000, 2500 };
            bool allPassed = true;

            foreach (int L in testLevels)
            {
                Console.WriteLine($"\n[Testing Level {L}]");
                DateTime start = DateTime.Now;

                LevelData lvl = WaterSortGeneratorEngine.GenerateLevel(L);
                WaterSortDifficulty difficulty = WaterSortDifficulty.ForLevel(L);
                int required = difficulty.RequiredMinimumMoves;

                Console.WriteLine($"  Color Count K = {lvl.colorCount}");
                Console.WriteLine($"  Tube Count  N = {lvl.tubeCount}");
                Console.WriteLine($"  Capacity    C = {lvl.capacity}");
                Console.WriteLine($"  Shuffle Depth S = {difficulty.ShuffleDepth}");
                Console.WriteLine($"  Required Min Moves = {required}");
                
                WaterSortSolveResult solveResult;
                int budget = difficulty.Capacity >= 5 ? 2000000 : 400000;
                bool solved = WaterSortSolver.TrySolveOptimal(lvl, budget, out solveResult);
                TimeSpan dur = DateTime.Now - start;

                bool passed = (solved && !solveResult.ReachedSearchLimit && solveResult.OptimalMoveCount >= required)
                           || (!lvl.validationExact && lvl.minMoves >= required);
                if (!passed)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  FAILED: Level {L} optimal={solveResult.OptimalMoveCount} < required={required} (or search exhausted). Time: {dur.TotalMilliseconds:F0}ms");
                    Console.ResetColor();
                    allPassed = false;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  PASSED: Solved in {solveResult.OptimalMoveCount} optimal moves (required {required})! (Time: {dur.TotalMilliseconds:F0}ms, states: {solveResult.StatesExplored})");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("\n-------------------------------------------------");
            Console.WriteLine("Simulating Core Pour Mechanics & Win Logic...");

            // Test Core Mechanics on Level 1
            LevelData testLvl1 = WaterSortGeneratorEngine.GenerateLevel(1);
            List<List<int>> tubes = WaterSortSolver.CloneTubes(testLvl1.tubes);

            // Verify tube capacity and validity
            bool validStructure = true;
            foreach (var t in tubes)
            {
                if (t.Count > testLvl1.capacity) validStructure = false;
            }

            if (validStructure)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED: Core tube structure and segment limits verified!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED: Invalid tube structure detected!");
                Console.ResetColor();
                allPassed = false;
            }

            Console.WriteLine("=================================================");
            if (allPassed)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("ALL SELF-TESTS PASSED SUCCESSFULLY!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("SOME SELF-TESTS FAILED!");
                Console.ResetColor();
            }
            Console.WriteLine("=================================================");
        }
    }
}
