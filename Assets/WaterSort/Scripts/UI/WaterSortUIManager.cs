using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Newtonsoft.Json;
using Designcoffers.Core;
using Designcoffers.WaterSort.Data;
using Designcoffers.WaterSort.Logic;
using Designcoffers.WaterSort.Visuals;

namespace Designcoffers.WaterSort.UI
{
    public class WaterSortUIManager : MonoBehaviour
    {
        [Header("Theme Reference")]
        public UIThemeSO theme;

        [Header("Top Bar")]
        public Text levelText;
        public Text coinsText;
        public Button settingsButton;

        [Header("Game Board Grid")]
        public RectTransform tubeBoardContainer;
        public GameObject tubePrefab;
        public LiquidStreamView liquidStreamView;

        [Header("Bottom Action Bar")]
        public Button undoButton;
        public Text undoBadgeText;
        public Button addTubeButton;
        public Text addTubeBadgeText;
        public Button restartButton;

        [Header("Win Screen Modal")]
        public GameObject winModal;
        public Text winLevelText;
        public Text winTierText;
        public Text winRewardText;
        public Button nextLevelButton;
        public ParticleSystem winConfettiParticleSystem;

        [Header("Settings Modal")]
        public GameObject settingsModal;
        public Toggle soundToggle;
        public Toggle hapticsToggle;
        public Button closeSettingsButton;

        private List<TubeView> spawnedTubes = new List<TubeView>();
        private static bool dotweenInitialized = false;

        private void Awake()
        {
            // DOTween ships precompiled; guarantee initialization before any tween is created.
            if (!dotweenInitialized)
            {
                dotweenInitialized = true;
                DG.Tweening.DOTween.Init(false, true, null);
            }
        }

        private void Start()
        {
            ApplyTheme();
            SubscribeEvents();
            UpdateCoinsDisplay(SaveManager.GetCoins());

            // Load level from SaveManager
            int levelNum = SaveManager.GetCurrentLevel();
            LoadLevelByNumber(levelNum);
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (WaterSortGameManager.Instance != null)
            {
                WaterSortGameManager.Instance.OnLevelLoaded += HandleLevelLoaded;
                WaterSortGameManager.Instance.OnTubeSelected += HandleTubeSelected;
                WaterSortGameManager.Instance.OnPourExecuted += HandlePourExecuted;
                WaterSortGameManager.Instance.OnInvalidPourAttempted += HandleInvalidPour;
                WaterSortGameManager.Instance.OnLevelWon += HandleLevelWon;
                WaterSortGameManager.Instance.OnUndoExecuted += HandleUndoExecuted;
                WaterSortGameManager.Instance.OnTubeAdded += HandleTubeAdded;
            }

            SaveManager.OnCoinsChanged += UpdateCoinsDisplay;

            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettingsModal);
            if (undoButton != null) undoButton.onClick.AddListener(OnUndoClicked);
            if (addTubeButton != null) addTubeButton.onClick.AddListener(OnAddTubeClicked);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
            if (nextLevelButton != null) nextLevelButton.onClick.AddListener(OnNextLevelClicked);
            if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(CloseSettingsModal);

            if (soundToggle != null)
            {
                soundToggle.isOn = SettingsManager.IsSoundEnabled();
                soundToggle.onValueChanged.AddListener(SettingsManager.SetSoundEnabled);
            }

            if (hapticsToggle != null)
            {
                hapticsToggle.isOn = SettingsManager.IsHapticsEnabled();
                hapticsToggle.onValueChanged.AddListener(SettingsManager.SetHapticsEnabled);
            }
        }

        private void UnsubscribeEvents()
        {
            if (WaterSortGameManager.Instance != null)
            {
                WaterSortGameManager.Instance.OnLevelLoaded -= HandleLevelLoaded;
                WaterSortGameManager.Instance.OnTubeSelected -= HandleTubeSelected;
                WaterSortGameManager.Instance.OnPourExecuted -= HandlePourExecuted;
                WaterSortGameManager.Instance.OnInvalidPourAttempted -= HandleInvalidPour;
                WaterSortGameManager.Instance.OnLevelWon -= HandleLevelWon;
                WaterSortGameManager.Instance.OnUndoExecuted -= HandleUndoExecuted;
                WaterSortGameManager.Instance.OnTubeAdded -= HandleTubeAdded;
            }

            SaveManager.OnCoinsChanged -= UpdateCoinsDisplay;
        }

        private void ApplyTheme()
        {
            if (theme == null) return;
            // Ensure background camera or canvas panel uses theme.backgroundColor (#0A0A0A)
            Camera.main.backgroundColor = theme.backgroundColor;
        }

        public void LoadLevelByNumber(int levelNum)
        {
            string rawJson = LevelLoader.LoadRawLevelData("levels");
            if (string.IsNullOrEmpty(rawJson))
            {
                // Fallback generated level
                var fallback = Generator.WaterSortGeneratorEngine.GenerateLevel(levelNum);
                WaterSortGameManager.Instance.LoadLevel(fallback);
                return;
            }

            LevelBundle bundle = JsonConvert.DeserializeObject<LevelBundle>(rawJson);
            if (bundle != null && bundle.levels != null && bundle.levels.Count >= levelNum)
            {
                var lvlData = bundle.levels[levelNum - 1];
                // The difficulty floor was solver-calibrated at generation time and is
                // serialized per level; fall back to the raw formula only if absent.
                int requiredFloor = lvlData.requiredMinMoves > 0
                    ? lvlData.requiredMinMoves
                    : 8 + lvlData.levelNumber / 10;
                if (lvlData.minMoves <= 0 || lvlData.minMoves < requiredFloor)
                {
                    Debug.LogError($"[WaterSortUIManager] Refusing an unvalidated level bundle (level {lvlData.levelNumber}: minMoves={lvlData.minMoves} below floor {requiredFloor}). Run Designcoffers/Water Sort/Generate & Validate Bundle before play.");
                    return;
                }
                WaterSortGameManager.Instance.LoadLevel(lvlData);
            }
            else
            {
                Debug.LogError($"[WaterSortUIManager] No bundled, validated level exists for level {levelNum}.");
            }
        }

        private void HandleLevelLoaded(LevelData level)
        {
            if (levelText != null) levelText.text = $"LEVEL {level.levelNumber}";
            if (winModal != null) winModal.SetActive(false);

            UpdateLifelineBadges();
            RebuildBoard(level);
            RefreshLockIndicators();
        }

        private void RebuildBoard(LevelData level)
        {
            if (tubeBoardContainer == null || tubePrefab == null)
            {
                Debug.LogWarning("[WaterSortUIManager] tubeBoardContainer or tubePrefab is not assigned! Use 'Designcoffers -> Auto-Setup Game Scene' from Unity top menu to set up automatically.");
                return;
            }

            // Clear existing tube views
            foreach (var t in spawnedTubes)
            {
                if (t != null) Destroy(t.gameObject);
            }
            spawnedTubes.Clear();

            int tubeCount = level.tubes.Count;
            for (int i = 0; i < tubeCount; i++)
            {
                GameObject obj = Instantiate(tubePrefab, tubeBoardContainer);
                TubeView view = obj.GetComponent<TubeView>();
                view.Initialize(i, level.capacity, level.tubes[i]);
                
                int indexClosure = i;
                Button btn = obj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => WaterSortGameManager.Instance.HandleTubeClick(indexClosure));
                }

                spawnedTubes.Add(view);
            }

            UpdateLayoutGrid(tubeCount);
        }

        private void UpdateLayoutGrid(int tubeCount)
        {
            GridLayoutGroup grid = tubeBoardContainer.GetComponent<GridLayoutGroup>();
            if (grid == null) return;

            int rows = tubeCount <= 7 ? 1 : 2;
            int columns = Mathf.CeilToInt((float)tubeCount / rows);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;

            float availableWidth = Mathf.Max(620f, tubeBoardContainer.rect.width - 32f);
            float horizontalGap = Mathf.Clamp(22f - columns, 8f, 16f);
            float tubeWidth = Mathf.Clamp((availableWidth - horizontalGap * (columns - 1)) / columns, 54f, 132f);
            float tubeHeight = Mathf.Clamp(tubeWidth * 2.45f, 170f, 330f);
            grid.cellSize = new Vector2(tubeWidth, tubeHeight);
            grid.spacing = new Vector2(horizontalGap, rows == 1 ? 0f : 24f);
        }

        private void HandleTubeSelected(int tubeIndex)
        {
            for (int i = 0; i < spawnedTubes.Count; i++)
            {
                spawnedTubes[i].SetSelected(i == tubeIndex);
            }
        }

        private void HandlePourExecuted(int srcIndex, int dstIndex, int colorId, int amount)
        {
            if (srcIndex < 0 || srcIndex >= spawnedTubes.Count || dstIndex < 0 || dstIndex >= spawnedTubes.Count) return;

            WaterSortGameManager.Instance.isAnimating = true;

            TubeView srcView = spawnedTubes[srcIndex];
            TubeView dstView = spawnedTubes[dstIndex];

            Color liquidColor = ColorPalette.GetColor(colorId);

            if (liquidStreamView != null)
            {
                Vector3 startPos = srcView.transform.position;
                Vector3 endPos = dstView.transform.position;

                liquidStreamView.PlayPourStream(startPos, endPos, liquidColor, 0.3f, () =>
                {
                    srcView.RenderSegments(WaterSortGameManager.Instance.currentLevel.tubes[srcIndex]);
                    dstView.RenderSegments(WaterSortGameManager.Instance.currentLevel.tubes[dstIndex]);
                    srcView.PlayLiquidSettle();
                    dstView.PlayLiquidSettle();
                    WaterSortGameManager.Instance.CompletePourAnimation();
                    RefreshLockIndicators();
                });
            }
            else
            {
                srcView.RenderSegments(WaterSortGameManager.Instance.currentLevel.tubes[srcIndex]);
                dstView.RenderSegments(WaterSortGameManager.Instance.currentLevel.tubes[dstIndex]);
                srcView.PlayLiquidSettle();
                dstView.PlayLiquidSettle();
                WaterSortGameManager.Instance.CompletePourAnimation();
                RefreshLockIndicators();
            }
        }

        private void RefreshLockIndicators()
        {
            var gm = WaterSortGameManager.Instance;
            if (gm == null || gm.currentLevel == null) return;
            for (int i = 0; i < spawnedTubes.Count; i++)
            {
                if (spawnedTubes[i] == null) continue;
                spawnedTubes[i].SetLocked(gm.IsTubeLocked(i), gm.LocksRemaining);
            }
        }

        private void HandleInvalidPour(int sourceIndex, int destinationIndex)
        {
            if (sourceIndex >= 0 && sourceIndex < spawnedTubes.Count)
            {
                spawnedTubes[sourceIndex].PlayShakeAnimation();
            }
            if (destinationIndex >= 0 && destinationIndex < spawnedTubes.Count && destinationIndex != sourceIndex)
            {
                spawnedTubes[destinationIndex].PlayShakeAnimation();
            }
        }

        private void HandleUndoExecuted()
        {
            var level = WaterSortGameManager.Instance.currentLevel;
            for (int i = 0; i < spawnedTubes.Count; i++)
            {
                spawnedTubes[i].RenderSegments(level.tubes[i]);
            }
            UpdateLifelineBadges();
            RefreshLockIndicators();
        }

        private void HandleTubeAdded()
        {
            RebuildBoard(WaterSortGameManager.Instance.currentLevel);
            UpdateLifelineBadges();
            RefreshLockIndicators();
        }

        private void UpdateLifelineBadges()
        {
            var gm = WaterSortGameManager.Instance;
            if (gm == null) return;

            if (undoBadgeText != null)
            {
                undoBadgeText.text = gm.FreeUndosRemaining > 0
                    ? gm.FreeUndosRemaining.ToString()
                    : WaterSortGameManager.UndoCoinCost.ToString();
            }
            if (undoButton != null) undoButton.interactable = gm.CanUndo();

            if (addTubeBadgeText != null)
            {
                addTubeBadgeText.text = gm.ExtraTubesRemaining > 0 ? gm.ExtraTubesRemaining.ToString() : string.Empty;
                addTubeBadgeText.gameObject.SetActive(gm.ExtraTubesRemaining > 0);
            }
            if (addTubeButton != null) addTubeButton.interactable = gm.ExtraTubesRemaining > 0;
        }

        private void HandleLevelWon()
        {
            if (winModal == null) return;

            winModal.SetActive(true);
            RectTransform modalTransform = winModal.transform as RectTransform;
            CanvasGroup modalCanvasGroup = winModal.GetComponent<CanvasGroup>();
            if (modalCanvasGroup == null) modalCanvasGroup = winModal.AddComponent<CanvasGroup>();
            if (modalTransform != null)
            {
                modalTransform.DOKill();
                modalTransform.localScale = new Vector3(0.88f, 0.88f, 1f);
                modalCanvasGroup.alpha = 0f;
                DOTween.Sequence()
                    .Append(modalCanvasGroup.DOFade(1f, 0.16f))
                    .Join(modalTransform.DOScale(1f, 0.28f).SetEase(Ease.OutBack))
                    .SetLink(winModal);
            }

            int lvl = WaterSortGameManager.Instance.currentLevel.levelNumber;
            if (winLevelText != null) winLevelText.text = $"LEVEL {lvl} COMPLETE!";

            string tier = GetDifficultyTierName(lvl);
            if (winTierText != null) winTierText.text = $"TIER: {tier}";

            if (winRewardText != null) winRewardText.text = "+25 COINS";

            // Restrained Orange + White confetti burst per PRD Spec
            if (winConfettiParticleSystem != null)
            {
                var main = winConfettiParticleSystem.main;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    theme != null ? theme.accentColor : new Color(0.976f, 0.451f, 0.086f, 1f),
                    Color.white
                );
                winConfettiParticleSystem.Play();
            }
        }

        private string GetDifficultyTierName(int levelNum)
        {
            // Six feel bands per the 10k HARD redesign.
            return Generator.WaterSortDifficulty.BandName(Generator.WaterSortDifficulty.ForLevel(levelNum).Band);
        }

        private void UpdateCoinsDisplay(int newCoins)
        {
            if (coinsText != null) coinsText.text = $"{newCoins} COINS";
        }

        private void OnUndoClicked()
        {
            var gm = WaterSortGameManager.Instance;
            if (gm == null || !gm.CanUndo()) return;

            if (gm.FreeUndosRemaining <= 0 && SaveManager.GetCoins() < WaterSortGameManager.UndoCoinCost)
            {
                // Not enough coins for a paid undo — subtle feedback instead of silent failure.
                if (coinsText != null)
                {
                    coinsText.transform.DOKill();
                    coinsText.transform.DOShakePosition(0.22f, new Vector2(8f, 0f), 14, 90f, false, true)
                        .SetEase(Ease.OutQuad).SetLink(coinsText.gameObject);
                }
                return;
            }

            if (gm.Undo())
            {
                UpdateLifelineBadges();
            }
        }

        private void OnAddTubeClicked()
        {
            if (WaterSortGameManager.Instance.AddExtraTube())
            {
                UpdateLifelineBadges();
            }
        }

        private void OnRestartClicked()
        {
            WaterSortGameManager.Instance.RestartLevel();
        }

        private void OnNextLevelClicked()
        {
            int current = SaveManager.GetCurrentLevel();
            LoadLevelByNumber(current);
        }

        private void OpenSettingsModal()
        {
            if (settingsModal != null) settingsModal.SetActive(true);
        }

        private void CloseSettingsModal()
        {
            if (settingsModal != null) settingsModal.SetActive(false);
        }
    }
}
