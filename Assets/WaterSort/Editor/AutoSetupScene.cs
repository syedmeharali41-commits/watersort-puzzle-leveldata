#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Designcoffers.Core;
using Designcoffers.WaterSort.Logic;
using Designcoffers.WaterSort.UI;
using Designcoffers.WaterSort.Visuals;

namespace Designcoffers.WaterSort.Editor
{
    /// <summary>
    /// One-shot scene builder for the Water Sort MVP. Produces the full PRD section-3
    /// layout: top bar (settings / level / coins), tube board, bottom action bar,
    /// win modal (orange+white confetti), settings modal, liquid stream and both
    /// prefabs — all themed by the shared UIThemeSO. Safe to re-run: it rebuilds
    /// the scene from scratch each time. Uses only flat surfaces, soft rounded
    /// corners (procedural sprites) and the dark + single-orange-accent palette.
    /// </summary>
    public static class AutoSetupScene
    {
        private const string SCENE_PATH = "Assets/MainScene.unity";
        private const string THEME_PATH = "Assets/Resources/DefaultTheme.asset";
        private const string FONT_PATH = "Assets/Core/Fonts/Outfit-Variable.ttf";
        private const string SPRITE_DIR = "Assets/Resources/Sprites";
        private const string TUBE_PREFAB_PATH = "Assets/Resources/TubePrefab.prefab";
        private const string SEGMENT_PREFAB_PATH = "Assets/Resources/SegmentPrefab.prefab";

        // Theme tokens (mirrors UIThemeSO defaults; #0A0A0A dark-first + #F97316 accent)
        private static Color BG = new Color(0.039f, 0.039f, 0.039f, 1f);
        private static Color ACCENT = new Color(0.976f, 0.451f, 0.086f, 1f);
        private static Color SURFACE = new Color(0.094f, 0.094f, 0.106f, 1f);
        private static Color BORDER = new Color(0.153f, 0.153f, 0.165f, 1f);
        private static Color TEXT_PRIMARY = Color.white;
        private static Color TEXT_SECONDARY = new Color(0.631f, 0.631f, 0.667f, 1f);

        private static UIThemeSO theme;
        private static Font font;
        private static Sprite roundedSprite;   // fully rounded rect (buttons, panels, segments, badges)
        private static Sprite circleSprite;    // circle (badges, toggle dots, settings icon)
        private static Sprite tubeSprite;      // tube body: rounded bottom corners only

        [MenuItem("Designcoffers/Auto-Setup Game Scene")]
        public static void SetupScene()
        {
            Build();
        }

        public static void Build()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

            theme = AssetDatabase.LoadAssetAtPath<UIThemeSO>(THEME_PATH);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UIThemeSO>();
                AssetDatabase.CreateAsset(theme, THEME_PATH);
            }

            // Wire the collection-standard font into the shared theme.
            font = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            theme.font = font;
            theme.fontName = "Outfit";
            theme.backgroundColor = BG;
            theme.accentColor = ACCENT;
            theme.surfaceColor = SURFACE;
            theme.surfaceBorderColor = BORDER;
            theme.textPrimaryColor = TEXT_PRIMARY;
            theme.textSecondaryColor = TEXT_SECONDARY;
            EditorUtility.SetDirty(theme);

            EnsureSprites();
            AssetDatabase.SaveAssets();

            // --- Reset scene: drop anything an earlier run created ---
            DestroyAll<Canvas>();
            DestroyAll<WaterSortGameManager>();
            DestroyAll<EventSystem>();

            // --- Camera: dark OLED backdrop ---
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BG;
            cam.orthographic = true;
            cam.orthographicSize = 5f;

            // --- Game manager ---
            GameObject managerGo = new GameObject("GameManager");
            managerGo.AddComponent<WaterSortGameManager>();

            // --- Canvas ---
            GameObject canvasGo = new GameObject("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();

            // --- Event system ---
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
            }

            WaterSortUIManager ui = canvasGo.AddComponent<WaterSortUIManager>();
            ui.theme = theme;

            // ============ TOP BAR ============
            RectTransform topBar = CreateRect("TopBar", canvasRt, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 150f));

            // Settings button (left)
            GameObject settingsGo = CreateButton("SettingsButton", topBar, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(88f, 88f), new Vector2(40f, -40f), SURFACE, null, null);
            RectTransform settingsRt = settingsGo.GetComponent<RectTransform>();
            ui.settingsButton = settingsGo.GetComponent<Button>();
            CreateMenuIcon(settingsRt);

            // Level number (center, large)
            GameObject levelTextGo = CreateText("LevelText", topBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(700f, 100f), new Vector2(0f, -25f),
                "LEVEL 1", 58, TextAnchor.MiddleCenter, TEXT_PRIMARY, FontStyle.Bold);
            ui.levelText = levelTextGo.GetComponent<Text>();
            AddSoftShadow(levelTextGo);

            // Coins (right)
            GameObject coinsGo = CreateText("CoinsText", topBar, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 0.5f), new Vector2(340f, 80f), new Vector2(-36f, -42f),
                "0 COINS", 38, TextAnchor.MiddleRight, TEXT_PRIMARY, FontStyle.Normal);
            ui.coinsText = coinsGo.GetComponent<Text>();

            // ============ GAME BOARD ============
            RectTransform board = CreateRect("TubeBoardContainer", canvasRt, new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.86f),
                Vector2.zero, Vector2.zero);
            GridLayoutGroup grid = board.gameObject.AddComponent<GridLayoutGroup>();
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.cellSize = new Vector2(150f, 380f);
            grid.spacing = new Vector2(24f, 24f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            ui.tubeBoardContainer = board;

            // ============ LIQUID STREAM (above board, below modals) ============
            GameObject streamGo = new GameObject("LiquidStream", typeof(RectTransform));
            streamGo.transform.SetParent(canvasRt, false);
            RectTransform streamRt = (RectTransform)streamGo.transform;
            streamRt.anchorMin = Vector2.zero;
            streamRt.anchorMax = Vector2.one;
            streamRt.offsetMin = Vector2.zero;
            streamRt.offsetMax = Vector2.zero;
            LiquidStreamView streamView = streamGo.AddComponent<LiquidStreamView>();
            GameObject streamImageGo = CreateImage("Stream", streamRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(20f, 20f), Vector2.zero, Color.white, roundedSprite);
            streamView.streamImage = streamImageGo.GetComponent<Image>();
            Image streamImg = streamImageGo.GetComponent<Image>();
            streamImg.raycastTarget = false;
            streamImg.enabled = false; // hidden until a pour runs
            ui.liquidStreamView = streamView;

            // ============ BOTTOM ACTION BAR ============
            RectTransform bottomBar = CreateRect("BottomActionBar", canvasRt, new Vector2(0f, 0.03f), new Vector2(1f, 0.19f),
                Vector2.zero, Vector2.zero);
            HorizontalLayoutGroup bottomLayout = bottomBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            bottomLayout.childAlignment = TextAnchor.MiddleCenter;
            bottomLayout.spacing = 64f;
            bottomLayout.childForceExpandWidth = false;
            bottomLayout.childForceExpandHeight = false;
            bottomLayout.childControlWidth = false;
            bottomLayout.childControlHeight = false;

            RectTransform undoBt = CreateActionButton("UndoButton", bottomBar, "UNDO");
            ui.undoButton = undoBt.GetComponent<Button>();
            Text undoBadge = CreateBadge(undoBt, "1");
            ui.undoBadgeText = undoBadge;

            RectTransform addBt = CreateActionButton("AddTubeButton", bottomBar, "ADD TUBE");
            ui.addTubeButton = addBt.GetComponent<Button>();
            Text addBadge = CreateBadge(addBt, "2");
            ui.addTubeBadgeText = addBadge;

            RectTransform restartBt = CreateActionButton("RestartButton", bottomBar, "RESTART");
            ui.restartButton = restartBt.GetComponent<Button>();

            // ============ TUBE PREFAB ============
            GameObject tubePrefab = BuildTubePrefab();
            GameObject segmentPrefab = BuildSegmentPrefab();
            ui.tubePrefab = tubePrefab;
            TubeView templateView = tubePrefab.GetComponent<TubeView>();
            templateView.segmentPrefab = segmentPrefab;

            // ============ WIN MODAL ============
            RectTransform winModal = CreateModalRoot("WinModal", canvasRt);
            ui.winModal = winModal.gameObject;
            RectTransform winPanel = CreatePanel(winModal, new Vector2(640f, 800f), "WinPanel");

            CreateText("WinTitle", winPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(560f, 110f), new Vector2(0f, -70f), "LEVEL 1 COMPLETE!", 56, TextAnchor.MiddleCenter, TEXT_PRIMARY, FontStyle.Bold);
            Text winTier = CreateText("WinTier", winPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(560f, 80f), new Vector2(0f, -200f), "TIER: BEGINNER", 40, TextAnchor.MiddleCenter, ACCENT, FontStyle.Bold).GetComponent<Text>();
            Text winReward = CreateText("WinReward", winPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(560f, 70f), new Vector2(0f, -290f), "+25 COINS", 38, TextAnchor.MiddleCenter, TEXT_SECONDARY, FontStyle.Normal).GetComponent<Text>();

            GameObject nextGo = CreateButton("NextLevelButton", winPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(440f, 120f), new Vector2(0f, 60f), ACCENT, "NEXT LEVEL", null);
            ui.nextLevelButton = nextGo.GetComponent<Button>();

            ParticleSystem confetti = BuildConfetti(winPanel);
            ui.winConfettiParticleSystem = confetti;
            Text winTitle = winModal.transform.Find("WinPanel/WinTitle").GetComponent<Text>();
            ui.winLevelText = winTitle;
            ui.winTierText = winTier;
            ui.winRewardText = winReward;

            // ============ SETTINGS MODAL ============
            RectTransform settingsModal = CreateModalRoot("SettingsModal", canvasRt);
            RectTransform settingsPanel = CreatePanel(settingsModal, new Vector2(620f, 680f), "SettingsPanel");
            CreateText("SettingsTitle", settingsPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(560f, 100f), new Vector2(0f, -56f), "SETTINGS", 50, TextAnchor.MiddleCenter, TEXT_PRIMARY, FontStyle.Bold);

            Toggle soundToggle = CreateToggleRow(settingsPanel, "SoundToggleRow", "SOUND", new Vector2(0f, -200f));
            Toggle hapticsToggle = CreateToggleRow(settingsPanel, "HapticsToggleRow", "HAPTICS", new Vector2(0f, -320f));
            ui.soundToggle = soundToggle;
            ui.hapticsToggle = hapticsToggle;

            GameObject closeGo = CreateButton("CloseSettingsButton", settingsPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(440f, 110f), new Vector2(0f, 54f), SURFACE, "CLOSE", null);
            ui.closeSettingsButton = closeGo.GetComponent<Button>();
            ui.settingsModal = settingsModal.gameObject;

            // ============ SAVE SCENE + BUILD SETTINGS ============
            EditorUtility.SetDirty(ui);
            EditorUtility.SetDirty(theme);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SCENE_PATH, true)
            };
            AssetDatabase.SaveAssets();

            Debug.Log("[Designcoffers] Auto-Setup Complete: full PRD Water Sort scene rebuilt. Open MainScene and press Play.");
        }

        // =====================================================================
        // Prefabs
        // =====================================================================

        private static GameObject BuildTubePrefab()
        {
            GameObject root = new GameObject("Tube", typeof(RectTransform));
            RectTransform rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(150f, 380f);

            // Transparent raycast target (the Button graphic)
            Image rootImage = root.AddComponent<Image>();
            rootImage.color = Color.clear;
            Button btn = root.AddComponent<Button>();
            btn.targetGraphic = rootImage;
            root.AddComponent<TapMotion>();

            TubeView view = root.AddComponent<TubeView>();
            view.tubeContainer = rt;
            view.capacity = 4;

            // Selection glow — THE hero accent element (orange, behind tube)
            RectTransform glowRt = CreateImageRect("SelectionGlow", rt, new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f), tubeSprite);
            Image glow = glowRt.GetComponent<Image>();
            glow.raycastTarget = false;
            glow.gameObject.SetActive(false);
            view.selectionGlow = glow;

            // Outline (border)
            RectTransform outlineRt = CreateImageRect("Outline", rt, new Vector2(0f, 0f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, BORDER, tubeSprite);
            outlineRt.GetComponent<Image>().raycastTarget = false;
            view.tubeOutline = outlineRt.GetComponent<Image>();

            // Tube body
            RectTransform bodyRt = CreateImageRect("Body", rt, new Vector2(0.05f, 0.045f), new Vector2(0.95f, 0.955f),
                Vector2.zero, Vector2.zero, SURFACE, tubeSprite);
            bodyRt.GetComponent<Image>().raycastTarget = false;
            view.tubeBackground = bodyRt.GetComponent<Image>();

            // Segments parent (inside the glass)
            RectTransform segmentsParent = CreateRect("SegmentsParent", rt, new Vector2(0.17f, 0.12f), new Vector2(0.83f, 0.95f),
                Vector2.zero, Vector2.zero);
            view.segmentsParent = segmentsParent;

            bool success;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, TUBE_PREFAB_PATH, out success);
            Object.DestroyImmediate(root);
            if (!success) Debug.LogError("[AutoSetupScene] Failed to save TubePrefab.");
            return saved;
        }

        private static GameObject BuildSegmentPrefab()
        {
            GameObject seg = new GameObject("Segment", typeof(RectTransform));
            Image img = seg.AddComponent<Image>();
            img.sprite = roundedSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            img.raycastTarget = false;

            bool success;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(seg, SEGMENT_PREFAB_PATH, out success);
            Object.DestroyImmediate(seg);
            if (!success) Debug.LogError("[AutoSetupScene] Failed to save SegmentPrefab.");
            return saved;
        }

        // =====================================================================
        // Modals, panels, particles
        // =====================================================================

        private static RectTransform CreateModalRoot(string name, RectTransform parent)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image scrim = root.AddComponent<Image>();
            scrim.color = new Color(BG.r, BG.g, BG.b, 0.74f);
            // CanvasGroup lives on the root so the UI manager can fade the whole modal in/out.
            root.AddComponent<CanvasGroup>();
            root.SetActive(false);
            return rt;
        }

        private static RectTransform CreatePanel(RectTransform modalRoot, Vector2 size, string panelName)
        {
            // Border (slightly larger) + surface panel
            RectTransform borderRt = CreateImageRect(panelName + "Border", modalRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), size + new Vector2(10f, 10f), BORDER, roundedSprite);
            borderRt.GetComponent<Image>().raycastTarget = false;

            RectTransform panelRt = CreateImageRect(panelName, modalRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), size, SURFACE, roundedSprite);
            panelRt.GetComponent<Image>().raycastTarget = false;
            return panelRt;
        }

        private static ParticleSystem BuildConfetti(RectTransform parent)
        {
            GameObject conf = new GameObject("WinConfetti");
            conf.transform.SetParent(parent, false);
            conf.transform.localPosition = new Vector3(0f, 90f, 0f);

            ParticleSystem ps = conf.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(ACCENT, Color.white);
            main.gravityModifier = 0.85f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 240;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)110, (short)20, 0.08f) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.7f;

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = UnityEngine.ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = 0.45f;
            renderer.sortingOrder = 32760;
            Material mat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            if (mat != null) renderer.sharedMaterial = mat;
            return ps;
        }

        // =====================================================================
        // Small UI factories
        // =====================================================================

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        private static RectTransform CreateImageRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 sizeDelta, Color color, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            return rt;
        }

        private static GameObject CreateImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos, Color color, Sprite sprite)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            return go;
        }

        private static GameObject CreateText(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos, string text, int fontSize,
            TextAnchor alignment, Color color, FontStyle style)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;

            Text t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = alignment;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return go;
        }

        private static GameObject CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos, Color fill, string label, System.Action onClick)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = anchoredPos;

            Image bg = go.AddComponent<Image>();
            bg.sprite = roundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = fill;

            // Border (slightly larger, rendered behind)
            Image border = CreateImage(name + "Border", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), Vector2.zero, BORDER, roundedSprite).GetComponent<Image>();
            border.rectTransform.offsetMin = new Vector2(-4f, -4f);
            border.rectTransform.offsetMax = new Vector2(4f, 4f);
            border.raycastTarget = false;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            go.AddComponent<TapMotion>();

            if (label != null)
            {
                Text txt = CreateText("Label", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero, label, 30, TextAnchor.MiddleCenter, TEXT_PRIMARY, FontStyle.Bold).GetComponent<Text>();
                txt.rectTransform.offsetMin = Vector2.zero;
                txt.rectTransform.offsetMax = Vector2.zero;
            }

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return go;
        }

        private static RectTransform CreateActionButton(string name, Transform parent, string label)
        {
            RectTransform rt = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.sizeDelta = new Vector2(170f, 170f);

            Image bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = roundedSprite;
            bg.type = Image.Type.Sliced;
            bg.color = SURFACE;

            Image border = CreateImage(name + "Border", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, BORDER, roundedSprite).GetComponent<Image>();
            border.rectTransform.offsetMin = new Vector2(-4f, -4f);
            border.rectTransform.offsetMax = new Vector2(4f, 4f);
            border.raycastTarget = false;

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            rt.gameObject.AddComponent<TapMotion>();

            Text txt = CreateText("Label", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, label, 28, TextAnchor.MiddleCenter, TEXT_PRIMARY, FontStyle.Bold).GetComponent<Text>();
            txt.rectTransform.offsetMin = Vector2.zero;
            txt.rectTransform.offsetMax = Vector2.zero;
            return rt;
        }

        private static Text CreateBadge(RectTransform buttonRt, string initial)
        {
            GameObject badge = CreateImage(buttonRt.name + "Badge", buttonRt, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(52f, 52f), new Vector2(-8f, -8f), ACCENT, circleSprite);
            badge.GetComponent<Image>().raycastTarget = false;
            Text t = CreateText("BadgeText", badge.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, initial, 30, TextAnchor.MiddleCenter, TEXT_PRIMARY, FontStyle.Bold).GetComponent<Text>();
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
            return t;
        }

        private static Toggle CreateToggleRow(RectTransform panel, string name, string label, Vector2 anchoredPos)
        {
            RectTransform row = CreateRect(name, panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            row.sizeDelta = new Vector2(520f, 110f);
            row.anchoredPosition = anchoredPos;

            Text labelText = CreateText("Label", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(360f, 70f), new Vector2(0f, 0f), label, 36, TextAnchor.MiddleLeft, TEXT_PRIMARY, FontStyle.Bold).GetComponent<Text>();

            GameObject toggleGo = CreateImage("Toggle", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(96f, 96f), Vector2.zero, SURFACE, circleSprite).gameObject;
            Image toggleBg = toggleGo.GetComponent<Image>();
            toggleBg.raycastTarget = true;

            GameObject dotGo = CreateImage("Dot", toggleGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(62f, 62f), Vector2.zero, ACCENT, circleSprite);
            Image dot = dotGo.GetComponent<Image>();
            dot.raycastTarget = false;
            dot.enabled = false;

            Toggle toggle = toggleGo.AddComponent<Toggle>();
            toggle.targetGraphic = toggleBg;
            toggle.graphic = dot;
            toggle.isOn = false;
            return toggle;
        }

        private static void CreateMenuIcon(RectTransform parent)
        {
            for (int i = 0; i < 3; i++)
            {
                Image line = CreateImage("Line" + i, parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 0.5f), new Vector2(40f, 8f), new Vector2(0f, -24f - i * 18f), TEXT_PRIMARY, roundedSprite).GetComponent<Image>();
                line.raycastTarget = false;
            }
        }

        private static void AddSoftShadow(GameObject target)
        {
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -6f);
        }

        private static void DestroyAll<T>() where T : Component
        {
            T[] all = Object.FindObjectsOfType<T>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null) Object.DestroyImmediate(all[i].gameObject);
            }
        }

        // =====================================================================
        // Procedural rounded-corner sprites (flat, soft, no textures)
        // =====================================================================

        private static void EnsureSprites()
        {
            if (!Directory.Exists(SPRITE_DIR)) AssetDatabase.CreateFolder("Assets/Resources", "Sprites");

            roundedSprite = CreateOrLoadSprite(SPRITE_DIR + "/rounded_rect.png", 128, 0.22f, false, 0.22f);
            circleSprite = CreateOrLoadSprite(SPRITE_DIR + "/circle.png", 128, 0.5f, false, 0.5f);
            tubeSprite = CreateOrLoadSprite(SPRITE_DIR + "/tube_body.png", 128, 0.14f, true, 0.14f);
        }

        private static Sprite CreateOrLoadSprite(string path, int resolution, float radiusFraction, bool roundedBottomOnly, float sliceBorder)
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing == null)
            {
                Texture2D tex = GenerateRoundedTexture(resolution, radiusFraction, roundedBottomOnly);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = 100f;
                int border = Mathf.RoundToInt(sliceBorder * resolution);
                importer.spriteBorder = new Vector4(border, border, border, border);
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Anti-aliased rounded rectangle SDF. roundedBottomOnly => test-tube body.</summary>
        private static Texture2D GenerateRoundedTexture(int size, float radiusFraction, bool roundedBottomOnly)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float hw = size / 2f - 0.5f;
            float hh = hw;
            float r = hw * radiusFraction;
            const int SS = 4; // supersampling for clean edges

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coverage = 0f;
                    for (int sy = 0; sy < SS; sy++)
                    {
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float px = x - hw + (sx + 0.5f) / SS;
                            float py = y - hh + (sy + 0.5f) / SS;
                            float localR = r;
                            if (roundedBottomOnly && py > 0f) localR = 0f;
                            float d = RoundedRectSDF(px, py, hw, hh, localR);
                            coverage += d <= 0f ? 1f : 0f;
                        }
                    }
                    float a = coverage / (SS * SS);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return tex;
        }

        private static float RoundedRectSDF(float px, float py, float hw, float hh, float radius)
        {
            float qx = Mathf.Abs(px) - (hw - radius);
            float qy = Mathf.Abs(py) - (hh - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - radius;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f);
        }
    }
}
#endif
