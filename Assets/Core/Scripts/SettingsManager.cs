using System;
using UnityEngine;

namespace Designcoffers.Core
{
    /// <summary>
    /// Generic settings manager for sound and haptics preferences across all puzzle collection games.
    /// </summary>
    public static class SettingsManager
    {
        private const string KEY_SOUND_ENABLED = "core_setting_sound";
        private const string KEY_HAPTICS_ENABLED = "core_setting_haptics";

        public static event Action<bool> OnSoundSettingChanged;
        public static event Action<bool> OnHapticsSettingChanged;

        public static bool IsSoundEnabled()
        {
            return PlayerPrefs.GetInt(KEY_SOUND_ENABLED, 1) == 1;
        }

        public static void SetSoundEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(KEY_SOUND_ENABLED, enabled ? 1 : 0);
            PlayerPrefs.Save();
            OnSoundSettingChanged?.Invoke(enabled);
        }

        public static bool IsHapticsEnabled()
        {
            return PlayerPrefs.GetInt(KEY_HAPTICS_ENABLED, 1) == 1;
        }

        public static void SetHapticsEnabled(bool enabled)
        {
            PlayerPrefs.SetInt(KEY_HAPTICS_ENABLED, enabled ? 1 : 0);
            PlayerPrefs.Save();
            OnHapticsSettingChanged?.Invoke(enabled);
        }

        public static void ToggleSound()
        {
            SetSoundEnabled(!IsSoundEnabled());
        }

        public static void ToggleHaptics()
        {
            SetHapticsEnabled(!IsHapticsEnabled());
        }
    }
}
