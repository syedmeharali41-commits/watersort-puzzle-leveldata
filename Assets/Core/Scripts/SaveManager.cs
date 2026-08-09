using System;
using UnityEngine;

namespace Designcoffers.Core
{
    /// <summary>
    /// Generic, reusable save system for key-value storage and player progress tracking.
    /// Decoupled from specific game mechanics.
    /// </summary>
    public static class SaveManager
    {
        private const string KEY_CURRENT_LEVEL = "core_current_level";
        private const string KEY_HIGHEST_LEVEL = "core_highest_level";
        private const string KEY_COINS = "core_coins";

        public static event Action<int> OnLevelProgressChanged;
        public static event Action<int> OnCoinsChanged;

        public static int GetCurrentLevel()
        {
            return PlayerPrefs.GetInt(KEY_CURRENT_LEVEL, 1);
        }

        public static void SetCurrentLevel(int level)
        {
            int clampedLevel = Mathf.Max(1, level);
            PlayerPrefs.SetInt(KEY_CURRENT_LEVEL, clampedLevel);
            
            int highest = GetHighestLevelReached();
            if (clampedLevel > highest)
            {
                PlayerPrefs.SetInt(KEY_HIGHEST_LEVEL, clampedLevel);
            }
            
            PlayerPrefs.Save();
            OnLevelProgressChanged?.Invoke(clampedLevel);
        }

        public static int GetHighestLevelReached()
        {
            return PlayerPrefs.GetInt(KEY_HIGHEST_LEVEL, 1);
        }

        public static int GetCoins()
        {
            return PlayerPrefs.GetInt(KEY_COINS, 0);
        }

        public static void AddCoins(int amount)
        {
            if (amount <= 0) return;
            int newTotal = GetCoins() + amount;
            PlayerPrefs.SetInt(KEY_COINS, newTotal);
            PlayerPrefs.Save();
            OnCoinsChanged?.Invoke(newTotal);
        }

        public static bool TryUseCoins(int amount)
        {
            if (amount <= 0) return true;
            int current = GetCoins();
            if (current >= amount)
            {
                int newTotal = current - amount;
                PlayerPrefs.SetInt(KEY_COINS, newTotal);
                PlayerPrefs.Save();
                OnCoinsChanged?.Invoke(newTotal);
                return true;
            }
            return false;
        }

        public static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            return PlayerPrefs.GetInt(key, defaultValue);
        }

        public static void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public static string GetString(string key, string defaultValue = "")
        {
            return PlayerPrefs.GetString(key, defaultValue);
        }

        public static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static bool GetBool(string key, bool defaultValue = false)
        {
            return PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) == 1;
        }

        public static void ClearAllData()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }
}
