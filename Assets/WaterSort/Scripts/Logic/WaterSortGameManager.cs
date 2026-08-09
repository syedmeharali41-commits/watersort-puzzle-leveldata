using System;
using System.Collections.Generic;
using UnityEngine;
using Designcoffers.Core;
using Designcoffers.WaterSort.Data;
using Designcoffers.WaterSort.Generator;

namespace Designcoffers.WaterSort.Logic
{
    public class WaterSortGameManager : MonoBehaviour
    {
        public static WaterSortGameManager Instance { get; private set; }

        [Header("State")]
        public LevelData currentLevel;
        public int selectedTubeIndex = -1;
        public bool isAnimating = false;
        public bool isGameWon = false;

        private Stack<MoveRecord> undoStack = new Stack<MoveRecord>();
        private LevelData initialLevel;
        private int freeUndosRemaining = 1;
        private int extraTubesRemaining = 2;
        private int moveCount = 0;

        /// <summary>True while the locked-tube lever is active (first lockDuration moves).</summary>
        public bool LocksActive { get { return currentLevel != null && currentLevel.lockDuration > 0 && moveCount < currentLevel.lockDuration; } }
        public int LocksRemaining { get { return LocksActive ? currentLevel.lockDuration - moveCount : 0; } }

        /// <summary>Undo cost in coins once the single free undo is spent.</summary>
        public const int UndoCoinCost = 50;
        public int FreeUndosRemaining { get { return freeUndosRemaining; } }
        public int ExtraTubesRemaining { get { return extraTubesRemaining; } }

        public event Action<LevelData> OnLevelLoaded;
        public event Action<int> OnTubeSelected; // -1 for deselect
        public event Action<int, int, int, int> OnPourExecuted; // src, dst, color, amount
        public event Action<int, int> OnInvalidPourAttempted;
        public event Action OnLevelWon;
        public event Action OnUndoExecuted;
        public event Action OnTubeAdded;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void LoadLevel(LevelData level)
        {
            if (level == null) return;
            initialLevel = level.Clone();
            currentLevel = level.Clone();
            selectedTubeIndex = -1;
            isAnimating = false;
            isGameWon = false;
            undoStack.Clear();
            freeUndosRemaining = 1;
            extraTubesRemaining = 2;
            moveCount = 0;

            OnLevelLoaded?.Invoke(currentLevel);
        }

        public void HandleTubeClick(int tubeIndex)
        {
            if (isGameWon || isAnimating) return;
            if (tubeIndex < 0 || tubeIndex >= currentLevel.tubes.Count) return;

            var tube = currentLevel.tubes[tubeIndex];

            // Locked tubes are unusable while the lock lever is active.
            if (LocksActive && IsTubeLocked(tubeIndex))
            {
                OnInvalidPourAttempted?.Invoke(-1, tubeIndex);
                return;
            }

            // Case 1: No tube currently selected
            if (selectedTubeIndex == -1)
            {
                if (tube.Count > 0)
                {
                    selectedTubeIndex = tubeIndex;
                    OnTubeSelected?.Invoke(selectedTubeIndex);
                }
                return;
            }

            // Case 2: Tapped same tube -> Deselect
            if (selectedTubeIndex == tubeIndex)
            {
                selectedTubeIndex = -1;
                OnTubeSelected?.Invoke(-1);
                return;
            }

            // Case 3: Tapped different tube -> Attempt Pour
            AttemptPour(selectedTubeIndex, tubeIndex);
        }

        /// <summary>True when the given tube is frozen by the locked-tube lever.</summary>
        public bool IsTubeLocked(int tubeIndex)
        {
            if (!LocksActive || currentLevel == null || currentLevel.lockedTubes == null || currentLevel.lockedTubes.Count == 0) return false;
            return currentLevel.lockedTubes.Contains(tubeIndex);
        }

        private void AttemptPour(int srcIndex, int dstIndex)
        {
            // Locked tubes cannot be sources or destinations while the lever is active.
            if (LocksActive && (IsTubeLocked(srcIndex) || IsTubeLocked(dstIndex)))
            {
                TriggerInvalidMove(srcIndex, dstIndex);
                return;
            }

            var src = currentLevel.tubes[srcIndex];
            var dst = currentLevel.tubes[dstIndex];
            int cap = currentLevel.capacity;

            if (src.Count == 0 || dst.Count >= cap)
            {
                TriggerInvalidMove(srcIndex, dstIndex);
                return;
            }

            int topColor = src[src.Count - 1];

            if (dst.Count > 0 && dst[dst.Count - 1] != topColor)
            {
                TriggerInvalidMove(srcIndex, dstIndex);
                return;
            }

            // Count top contiguous block of topColor in src
            int topBlockSize = 0;
            for (int i = src.Count - 1; i >= 0; i--)
            {
                if (src[i] == topColor) topBlockSize++;
                else break;
            }

            int spaceLeft = cap - dst.Count;
            int amountToPour = Mathf.Min(topBlockSize, spaceLeft);

            if (amountToPour <= 0)
            {
                TriggerInvalidMove(srcIndex, dstIndex);
                return;
            }

            // Execute pour state change
            for (int p = 0; p < amountToPour; p++)
            {
                src.RemoveAt(src.Count - 1);
                dst.Add(topColor);
            }

            // Record move for Undo
            undoStack.Push(new MoveRecord
            {
                fromTubeIndex = srcIndex,
                toTubeIndex = dstIndex,
                colorId = topColor,
                amount = amountToPour
            });

            selectedTubeIndex = -1;
            OnTubeSelected?.Invoke(-1);

            moveCount++;

            // Lock input before UI is notified. The UI calls CompletePourAnimation
            // after the visual stream lands, so a win can never cut that animation off.
            isAnimating = true;
            OnPourExecuted?.Invoke(srcIndex, dstIndex, topColor, amountToPour);
            TriggerHaptic();
        }

        private void TriggerInvalidMove(int sourceIndex, int destinationIndex)
        {
            selectedTubeIndex = -1;
            OnTubeSelected?.Invoke(-1);
            OnInvalidPourAttempted?.Invoke(sourceIndex, destinationIndex);
        }

        /// <summary>Called by the view when the pour animation has visually landed.</summary>
        public void CompletePourAnimation()
        {
            if (!isAnimating) return;
            isAnimating = false;
            ReleaseLocksIfDeadlocked();
            CheckWinCondition();
        }

        /// <summary>
        /// Graceful fallback: if locked tubes ever leave the board with no legal
        /// unlocked move, release the locks immediately so the level can never soft-lock.
        /// </summary>
        private void ReleaseLocksIfDeadlocked()
        {
            if (!LocksActive) return;
            for (int s = 0; s < currentLevel.tubes.Count; s++)
            {
                if (IsTubeLocked(s)) continue;
                for (int d = 0; d < currentLevel.tubes.Count; d++)
                {
                    if (IsTubeLocked(d) || s == d) continue;
                    WaterSortMove probe;
                    if (WaterSortSolver.IsLegalMove(currentLevel.tubes, s, d, currentLevel.capacity, out probe))
                    {
                        return; // an unlocked legal move still exists
                    }
                }
            }
            moveCount = currentLevel.lockDuration; // force-release
        }

        public bool CanUndo()
        {
            return undoStack.Count > 0;
        }

        public bool Undo()
        {
            if (isAnimating || isGameWon || !CanUndo()) return false;

            if (freeUndosRemaining <= 0)
            {
                if (!SaveManager.TryUseCoins(UndoCoinCost))
                {
                    return false; // Not enough coins
                }
            }
            else
            {
                freeUndosRemaining--;
            }

            var lastMove = undoStack.Pop();
            var src = currentLevel.tubes[lastMove.fromTubeIndex];
            var dst = currentLevel.tubes[lastMove.toTubeIndex];

            // Revert move
            for (int i = 0; i < lastMove.amount; i++)
            {
                if (dst.Count > 0)
                {
                    dst.RemoveAt(dst.Count - 1);
                    src.Add(lastMove.colorId);
                }
            }

            OnUndoExecuted?.Invoke();
            TriggerHaptic();
            return true;
        }

        public bool AddExtraTube()
        {
            if (isGameWon || isAnimating || currentLevel == null || extraTubesRemaining <= 0) return false;
            
            extraTubesRemaining--;
            currentLevel.tubeCount++;
            currentLevel.tubes.Add(new List<int>());
            OnTubeAdded?.Invoke();
            TriggerHaptic();
            return true;
        }

        public void RestartLevel()
        {
            if (initialLevel != null)
            {
                LoadLevel(initialLevel);
            }
        }

        public void CheckWinCondition()
        {
            int cap = currentLevel.capacity;
            foreach (var tube in currentLevel.tubes)
            {
                if (tube.Count == 0) continue;
                if (tube.Count != cap) return; // Must be full
                
                int first = tube[0];
                for (int i = 1; i < tube.Count; i++)
                {
                    if (tube[i] != first) return; // Must be pure
                }
            }

            // All non-empty tubes are pure and full -> WIN!
            isGameWon = true;
            SaveManager.AddCoins(25);
            SaveManager.SetCurrentLevel(currentLevel.levelNumber + 1);
            OnLevelWon?.Invoke();
        }

        private static void TriggerHaptic()
        {
            if (SettingsManager.IsHapticsEnabled()) Handheld.Vibrate();
        }
    }
}
