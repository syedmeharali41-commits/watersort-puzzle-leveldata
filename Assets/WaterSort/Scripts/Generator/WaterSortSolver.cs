using System;
using System.Collections.Generic;
using System.Text;
using Designcoffers.WaterSort.Data;

namespace Designcoffers.WaterSort.Generator
{
    /// <summary>One legal full-block pour. Tube values are bottom-to-top.</summary>
    public struct WaterSortMove
    {
        public int Source;
        public int Destination;
        public int Color;
        public int Amount;
    }

    /// <summary>Result of an exact offline search. A capped search is never reported as validated.</summary>
    public struct WaterSortSolveResult
    {
        public bool IsSolved;
        public bool ReachedSearchLimit;
        public int OptimalMoveCount;
        public int StatesExplored;
    }

    /// <summary>
    /// Exact A* validator for bundled levels. The heuristic is deliberately
    /// admissible (colour-block excess), so the first solution dequeued is optimal.
    /// This class owns game-rule semantics used by both generator and runtime tests.
    /// </summary>
    public static class WaterSortSolver
    {
        private sealed class SearchNode
        {
            public readonly List<List<int>> Tubes;
            public readonly int Depth;
            public readonly int Heuristic;

            public int Cost { get { return Depth + Heuristic; } }

            public SearchNode(List<List<int>> tubes, int depth, int capacity)
            {
                Tubes = tubes;
                Depth = depth;
                Heuristic = CalculateAdmissibleHeuristic(tubes, capacity);
            }
        }

        private sealed class MinHeap
        {
            private readonly List<SearchNode> nodes = new List<SearchNode>();

            public int Count { get { return nodes.Count; } }

            public void Push(SearchNode node)
            {
                nodes.Add(node);
                int child = nodes.Count - 1;
                while (child > 0)
                {
                    int parent = (child - 1) / 2;
                    if (Compare(nodes[parent], node) <= 0) break;
                    nodes[child] = nodes[parent];
                    child = parent;
                }
                nodes[child] = node;
            }

            public SearchNode Pop()
            {
                SearchNode result = nodes[0];
                SearchNode tail = nodes[nodes.Count - 1];
                nodes.RemoveAt(nodes.Count - 1);
                if (nodes.Count == 0) return result;

                int parent = 0;
                while (true)
                {
                    int left = parent * 2 + 1;
                    if (left >= nodes.Count) break;
                    int right = left + 1;
                    int bestChild = right < nodes.Count && Compare(nodes[right], nodes[left]) < 0 ? right : left;
                    if (Compare(tail, nodes[bestChild]) <= 0) break;
                    nodes[parent] = nodes[bestChild];
                    parent = bestChild;
                }
                nodes[parent] = tail;
                return result;
            }

            private static int Compare(SearchNode a, SearchNode b)
            {
                int result = a.Cost.CompareTo(b.Cost);
                if (result != 0) return result;
                return a.Heuristic.CompareTo(b.Heuristic);
            }
        }

        public static int Solve(LevelData level, int maxStates = 50000)
        {
            WaterSortSolveResult result;
            return TrySolveOptimal(level, maxStates, out result) ? result.OptimalMoveCount : -1;
        }

        public static bool TrySolveOptimal(LevelData level, int maxStates, out WaterSortSolveResult result)
        {
            result = new WaterSortSolveResult
            {
                IsSolved = false,
                ReachedSearchLimit = false,
                OptimalMoveCount = -1,
                StatesExplored = 0
            };

            if (level == null || level.tubes == null || level.capacity <= 0 || maxStates <= 0)
            {
                return false;
            }

            if (IsSolved(level.tubes, level.capacity))
            {
                result.IsSolved = true;
                result.OptimalMoveCount = 0;
                return true;
            }

            MinHeap open = new MinHeap();
            Dictionary<string, int> bestDepthByState = new Dictionary<string, int>();
            List<List<int>> start = CloneTubes(level.tubes);
            string startKey = GetCanonicalKey(start);
            bestDepthByState.Add(startKey, 0);
            open.Push(new SearchNode(start, 0, level.capacity));

            while (open.Count > 0)
            {
                SearchNode current = open.Pop();
                string currentKey = GetCanonicalKey(current.Tubes);
                int knownDepth;
                if (!bestDepthByState.TryGetValue(currentKey, out knownDepth) || knownDepth != current.Depth)
                {
                    continue;
                }

                result.StatesExplored++;
                if (result.StatesExplored > maxStates)
                {
                    result.ReachedSearchLimit = true;
                    return false;
                }

                if (IsSolved(current.Tubes, level.capacity))
                {
                    result.IsSolved = true;
                    result.OptimalMoveCount = current.Depth;
                    return true;
                }

                List<WaterSortMove> moves = GetLegalMoves(current.Tubes, level.capacity);
                for (int i = 0; i < moves.Count; i++)
                {
                    List<List<int>> next = ApplyMove(current.Tubes, moves[i], level.capacity);
                    string nextKey = GetCanonicalKey(next);
                    int nextDepth = current.Depth + 1;
                    int previousDepth;
                    if (bestDepthByState.TryGetValue(nextKey, out previousDepth) && previousDepth <= nextDepth)
                    {
                        continue;
                    }

                    bestDepthByState[nextKey] = nextDepth;
                    open.Push(new SearchNode(next, nextDepth, level.capacity));
                }
            }

            return false;
        }

        /// <summary>
        /// Exact A* that also honours the locked-tube lever: while the move count is below
        /// lockDuration, pours touching a locked tube are illegal. Used to verify that
        /// bundled Expert/World-Class levels stay solvable under their own lock constraint.
        /// State keys are phase-tagged so the same board in the locked and free phase is
        /// treated as two distinct search states. The admissible heuristic stays admissible
        /// (locks only ever remove moves).
        /// </summary>
        public static bool TrySolveOptimalLocked(LevelData level, int maxStates, out WaterSortSolveResult result)
        {
            result = new WaterSortSolveResult { IsSolved = false, ReachedSearchLimit = false, OptimalMoveCount = -1, StatesExplored = 0 };
            if (level == null || level.tubes == null || level.capacity <= 0 || maxStates <= 0) return false;

            bool hasLocks = level.lockedTubes != null && level.lockedTubes.Count > 0 && level.lockDuration > 0;
            if (!hasLocks) return TrySolveOptimal(level, maxStates, out result);

            if (IsSolved(level.tubes, level.capacity))
            {
                result.IsSolved = true;
                result.OptimalMoveCount = 0;
                return true;
            }

            int lockDuration = level.lockDuration;
            MinHeap open = new MinHeap();
            Dictionary<string, int> bestDepthByState = new Dictionary<string, int>();
            List<List<int>> start = CloneTubes(level.tubes);
            string startKey = KeyWithPhase(start, 0, lockDuration);
            bestDepthByState.Add(startKey, 0);
            open.Push(new SearchNode(start, 0, level.capacity));

            while (open.Count > 0)
            {
                SearchNode current = open.Pop();
                bool lockedPhase = current.Depth < lockDuration;
                string currentKey = KeyWithPhase(current.Tubes, current.Depth, lockDuration);
                int knownDepth;
                if (!bestDepthByState.TryGetValue(currentKey, out knownDepth) || knownDepth != current.Depth) continue;

                result.StatesExplored++;
                if (result.StatesExplored > maxStates)
                {
                    result.ReachedSearchLimit = true;
                    return false;
                }

                if (IsSolved(current.Tubes, level.capacity))
                {
                    result.IsSolved = true;
                    result.OptimalMoveCount = current.Depth;
                    return true;
                }

                List<WaterSortMove> moves = GetLegalMoves(current.Tubes, level.capacity);
                for (int i = 0; i < moves.Count; i++)
                {
                    if (lockedPhase)
                    {
                        if (level.lockedTubes.Contains(moves[i].Source) || level.lockedTubes.Contains(moves[i].Destination)) continue;
                    }
                    List<List<int>> next = ApplyMove(current.Tubes, moves[i], level.capacity);
                    int nextDepth = current.Depth + 1;
                    string nextKey = KeyWithPhase(next, nextDepth, lockDuration);
                    int previousDepth;
                    if (bestDepthByState.TryGetValue(nextKey, out previousDepth) && previousDepth <= nextDepth) continue;
                    bestDepthByState[nextKey] = nextDepth;
                    open.Push(new SearchNode(next, nextDepth, level.capacity));
                }
            }

            return false;
        }

        private static string KeyWithPhase(List<List<int>> tubes, int depth, int lockDuration)
        {
            return GetCanonicalKey(tubes) + (depth < lockDuration ? "|L" : "|F");
        }

        public static bool IsSolved(List<List<int>> tubes, int capacity)
        {
            if (tubes == null) return false;
            for (int i = 0; i < tubes.Count; i++)
            {
                List<int> tube = tubes[i];
                if (tube.Count == 0) continue;
                if (tube.Count != capacity) return false;
                int colour = tube[0];
                for (int segment = 1; segment < tube.Count; segment++)
                {
                    if (tube[segment] != colour) return false;
                }
            }
            return true;
        }

        public static List<WaterSortMove> GetLegalMoves(List<List<int>> tubes, int capacity)
        {
            List<WaterSortMove> moves = new List<WaterSortMove>();
            if (tubes == null) return moves;

            for (int sourceIndex = 0; sourceIndex < tubes.Count; sourceIndex++)
            {
                List<int> source = tubes[sourceIndex];
                if (source.Count == 0) continue;

                int colour = source[source.Count - 1];
                int blockSize = CountTopBlock(source);
                bool sourceIsUniform = blockSize == source.Count;
                bool emptyDestinationHandled = false;

                for (int destinationIndex = 0; destinationIndex < tubes.Count; destinationIndex++)
                {
                    if (sourceIndex == destinationIndex) continue;
                    List<int> destination = tubes[destinationIndex];
                    if (destination.Count >= capacity) continue;

                    if (destination.Count == 0)
                    {
                        // Moving a pure tube to an empty tube is only a permutation
                        // of unlabeled tubes, never a useful search branch.
                        if (sourceIsUniform || emptyDestinationHandled) continue;
                        emptyDestinationHandled = true;
                    }
                    else if (destination[destination.Count - 1] != colour)
                    {
                        continue;
                    }

                    int amount = Math.Min(blockSize, capacity - destination.Count);
                    if (amount <= 0) continue;
                    moves.Add(new WaterSortMove
                    {
                        Source = sourceIndex,
                        Destination = destinationIndex,
                        Color = colour,
                        Amount = amount
                    });
                }
            }

            return moves;
        }

        // Retained for the pre-existing test tooling and callers that only need states.
        public static List<List<List<int>>> GetPossibleMoves(List<List<int>> tubes, int capacity)
        {
            List<WaterSortMove> legalMoves = GetLegalMoves(tubes, capacity);
            List<List<List<int>>> states = new List<List<List<int>>>(legalMoves.Count);
            for (int i = 0; i < legalMoves.Count; i++)
            {
                states.Add(ApplyMove(tubes, legalMoves[i], capacity));
            }
            return states;
        }

        public static bool IsLegalMove(List<List<int>> tubes, int sourceIndex, int destinationIndex, int capacity, out WaterSortMove move)
        {
            move = new WaterSortMove();
            if (tubes == null || sourceIndex < 0 || destinationIndex < 0 || sourceIndex >= tubes.Count || destinationIndex >= tubes.Count || sourceIndex == destinationIndex)
            {
                return false;
            }

            List<int> source = tubes[sourceIndex];
            List<int> destination = tubes[destinationIndex];
            if (source.Count == 0 || destination.Count >= capacity) return false;

            int colour = source[source.Count - 1];
            if (destination.Count > 0 && destination[destination.Count - 1] != colour) return false;

            int amount = Math.Min(CountTopBlock(source), capacity - destination.Count);
            if (amount <= 0) return false;

            move = new WaterSortMove
            {
                Source = sourceIndex,
                Destination = destinationIndex,
                Color = colour,
                Amount = amount
            };
            return true;
        }

        public static List<List<int>> ApplyMove(List<List<int>> sourceState, WaterSortMove move, int capacity)
        {
            List<List<int>> next = CloneTubes(sourceState);
            List<int> source = next[move.Source];
            List<int> destination = next[move.Destination];
            for (int i = 0; i < move.Amount; i++)
            {
                source.RemoveAt(source.Count - 1);
                destination.Add(move.Color);
            }
            return next;
        }

        public static int CountTopBlock(List<int> tube)
        {
            if (tube == null || tube.Count == 0) return 0;
            int colour = tube[tube.Count - 1];
            int count = 0;
            for (int i = tube.Count - 1; i >= 0 && tube[i] == colour; i--) count++;
            return count;
        }

        /// <summary>
        /// h = total colour blocks - distinct colours. One legal move can merge at
        /// most one block, so h never overestimates remaining moves.
        /// </summary>
        public static int CalculateAdmissibleHeuristic(List<List<int>> tubes, int capacity)
        {
            int blocks = 0;
            HashSet<int> colours = new HashSet<int>();
            for (int tubeIndex = 0; tubeIndex < tubes.Count; tubeIndex++)
            {
                List<int> tube = tubes[tubeIndex];
                int previous = Int32.MinValue;
                for (int segment = 0; segment < tube.Count; segment++)
                {
                    int colour = tube[segment];
                    colours.Add(colour);
                    if (segment == 0 || colour != previous) blocks++;
                    previous = colour;
                }
            }
            return Math.Max(0, blocks - colours.Count);
        }

        /// <summary>
        /// Canonical representation makes permutations of physically identical tubes
        /// one state, dramatically reducing BFS/A* work without changing solutions.
        /// </summary>
        public static string GetCanonicalKey(List<List<int>> tubes)
        {
            List<string> tubeKeys = new List<string>(tubes.Count);
            for (int tubeIndex = 0; tubeIndex < tubes.Count; tubeIndex++)
            {
                List<int> tube = tubes[tubeIndex];
                StringBuilder builder = new StringBuilder(tube.Count * 3 + 1);
                builder.Append('[');
                for (int segment = 0; segment < tube.Count; segment++)
                {
                    if (segment > 0) builder.Append(',');
                    builder.Append(tube[segment]);
                }
                builder.Append(']');
                tubeKeys.Add(builder.ToString());
            }
            tubeKeys.Sort(StringComparer.Ordinal);
            return string.Join("|", tubeKeys.ToArray());
        }

        public static List<List<int>> CloneTubes(List<List<int>> tubes)
        {
            List<List<int>> clone = new List<List<int>>(tubes.Count);
            for (int i = 0; i < tubes.Count; i++) clone.Add(new List<int>(tubes[i]));
            return clone;
        }
    }
}
