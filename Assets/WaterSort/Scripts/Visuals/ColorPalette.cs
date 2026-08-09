using System.Collections.Generic;
using UnityEngine;

namespace Designcoffers.WaterSort.Visuals
{
    public static class ColorPalette
    {
        // 16 curated, highly distinguishable HSL-tuned liquid colors
        private static readonly Color[] Palette = new Color[]
        {
            new Color(0.937f, 0.267f, 0.267f, 1f), // 1: Red
            new Color(0.235f, 0.510f, 0.965f, 1f), // 2: Royal Blue
            new Color(0.133f, 0.773f, 0.369f, 1f), // 3: Emerald Green
            new Color(0.961f, 0.722f, 0.125f, 1f), // 4: Warm Yellow
            new Color(0.659f, 0.333f, 0.961f, 1f), // 5: Purple
            new Color(0.925f, 0.345f, 0.608f, 1f), // 6: Hot Pink
            new Color(0.086f, 0.718f, 0.824f, 1f), // 7: Cyan
            new Color(0.976f, 0.451f, 0.086f, 1f), // 8: Vibrant Orange
            new Color(0.404f, 0.227f, 0.718f, 1f), // 9: Indigo
            new Color(0.533f, 0.776f, 0.161f, 1f), // 10: Lime
            new Color(0.855f, 0.439f, 0.839f, 1f), // 11: Orchid
            new Color(0.180f, 0.800f, 0.686f, 1f), // 12: Turquoise
            new Color(0.910f, 0.537f, 0.376f, 1f), // 13: Coral
            new Color(0.392f, 0.710f, 0.965f, 1f), // 14: Sky Blue
            new Color(0.820f, 0.627f, 0.471f, 1f), // 15: Sand Gold
            new Color(0.557f, 0.600f, 0.682f, 1f)  // 16: Slate Grey
        };

        public static Color GetColor(int colorId)
        {
            if (colorId <= 0) return Color.clear;
            int index = (colorId - 1) % Palette.Length;
            return Palette[index];
        }
    }
}
