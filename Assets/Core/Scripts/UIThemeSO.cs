using UnityEngine;

namespace Designcoffers.Core
{
    /// <summary>
    /// ScriptableObject design system token repository for Designcoffers puzzle game collection.
    /// Standardized across all 6 planned games.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "Designcoffers/UI Theme", order = 1)]
    public class UIThemeSO : ScriptableObject
    {
        [Header("Color Palette (Dark-First + Orange Accent)")]
        [Tooltip("#0A0A0A — Near-black OLED background")]
        public Color backgroundColor = new Color(0.039f, 0.039f, 0.039f, 1f);

        [Tooltip("#F97316 — Orange accent reserved for CTAs, active selection, win states")]
        public Color accentColor = new Color(0.976f, 0.451f, 0.086f, 1f);

        [Tooltip("#18181B — Dark card surface background")]
        public Color surfaceColor = new Color(0.094f, 0.094f, 0.106f, 1f);

        [Tooltip("#27272A — Subtle surface border")]
        public Color surfaceBorderColor = new Color(0.153f, 0.153f, 0.165f, 1f);

        [Tooltip("#FFFFFF — Primary text color")]
        public Color textPrimaryColor = Color.white;

        [Tooltip("#A1A1AA — Secondary muted text color")]
        public Color textSecondaryColor = new Color(0.631f, 0.631f, 0.667f, 1f);

        [Header("Typography & Styling")]
        public string fontName = "Outfit";
        [Tooltip("The one collection-wide geometric sans-serif font asset.")]
        public Font font;
        public float buttonCornerRadius = 16f;
        public float cardCornerRadius = 20f;

        [Header("Micro-Animations")]
        public float defaultAnimDuration = 0.25f; // 250ms
        public float pourAnimDuration = 0.35f;    // 350ms
        public float scalePressAmount = 0.94f;
    }
}
