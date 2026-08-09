using System;

namespace Designcoffers.Core
{
    /// <summary>
    /// Generic interface and utility for loading bundled puzzle level data from Resources.
    /// </summary>
    public static class LevelLoader
    {
        private const string DEFAULT_RESOURCE_PATH = "levels";

        public static string LoadRawLevelData(string resourcePath = DEFAULT_RESOURCE_PATH)
        {
            #if UNITY_5_3_OR_NEWER
            UnityEngine.TextAsset textAsset = UnityEngine.Resources.Load<UnityEngine.TextAsset>(resourcePath);
            if (textAsset != null)
            {
                return textAsset.text;
            }
            UnityEngine.Debug.LogError($"[LevelLoader] Failed to load level data at Resources/{resourcePath}");
            #endif
            return null;
        }
    }
}
