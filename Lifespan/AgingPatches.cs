using System;
using HarmonyLib;
using ModAPI.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    public static class AgingPatches
    {
        internal static AgeTracker Tracker;
        internal static ElderIllnessManager IllnessManager;
        internal static DeathManager DeathManager;
        internal static IIllnessState IllnessState;
        internal static Action OnNewWeekCallback;
        internal static Action OnNewDayCallback;

        private static readonly object _deathDayLock = new object();
        private static Dictionary<int, int> _pendingDeathDays = new Dictionary<int, int>();
        private sealed class IllnessModifierState
        {
            public FamilyMember Member;
            public int Intelligence;
            public int Strength;
            public int Dexterity;
        }

        private static Dictionary<int, IllnessModifierState> _appliedIllnessModifiers = new Dictionary<int, IllnessModifierState>();

        internal static void ResetIllnessStatModifiers()
        {
            foreach (IllnessModifierState applied in _appliedIllnessModifiers.Values)
            {
                if (applied == null || applied.Member == null) continue;

                try
                {
                    BaseStats stats = applied.Member.BaseStats;
                    if (stats == null) continue;

                    RemoveAppliedModifier(stats.Intelligence, applied.Intelligence);
                    RemoveAppliedModifier(stats.Strength, applied.Strength);
                    RemoveAppliedModifier(stats.Dexterity, applied.Dexterity);
                }
                catch
                {
                    // A reset can race a destroyed Unity character. The cached state
                    // must still be discarded so a later session cannot stack it.
                }
            }

            _appliedIllnessModifiers.Clear();
        }

        private static void RemoveAppliedModifier(BaseStat stat, int modifier)
        {
            if (stat == null || modifier == 0) return;
            stat.SetLevelModifier(stat.LevelModifier - modifier);
        }

        private static void DebugLog(IModLogger log, bool enabled, string message)
        {
            if (!enabled || log == null) return;

            try
            {
                log.Debug(message);
            }
            catch
            {
                // Logging must not prevent gameplay state from being applied.
            }
        }

        public static void SetDeathDayOverride(int memberId, int day)
        {
            lock (_deathDayLock)
            {
                if (_pendingDeathDays == null) _pendingDeathDays = new Dictionary<int, int>();
                _pendingDeathDays[memberId] = day;
            }
        }

        public static bool TryGetDeathDayOverride(int memberId, out int day)
        {
             day = -1;
             lock (_deathDayLock)
             {
                 if (_pendingDeathDays == null) return false;
                 return _pendingDeathDays.TryGetValue(memberId, out day);
             }
        }

        public static void ClearDeathDayOverride(int memberId)
        {
            lock (_deathDayLock)
            {
                if (_pendingDeathDays != null) _pendingDeathDays.Remove(memberId);
            }
        }

        /// <summary>
        /// GameTime wipes its delegates in Awake(). We must re-hook every time.
        /// Also use this as a trigger to clear old session data so it doesn't leak into new games.
        /// </summary>
        [HarmonyPatch(typeof(GameTime), "Awake")]
        public static class GameTime_Awake_Patch
        {
            public static void Postfix()
            {
                LifespanPlugin.Instance.Log.Debug("GameTime.Awake detected. Re-hooking events and resetting managers.");
                
                // Re-hook events
                GameTime.newWeek -= HandleNewWeek;
                GameTime.newWeek += HandleNewWeek;
                GameTime.newDay -= HandleNewDay;
                GameTime.newDay += HandleNewDay;

                // Reset Managers to purge data from previous game session
                Tracker?.Reset();
                DeathManager?.Reset();
                LifespanPlugin.Instance?.ResetAllState();
            }
        }

        private static void HandleNewWeek()
        {
            if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("GameTime.newWeek fired!");
            OnNewWeekCallback?.Invoke();
        }

        private static void HandleNewDay()
        {
            if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("GameTime.newDay fired!");
            OnNewDayCallback?.Invoke();
        }

        /// <summary>
        /// Patch to ensure m_child and mesh state are correctly restored on load.
        /// </summary>
        public static class BaseCharacter_SaveLoadCharacter_Patch
        {
            public static void Postfix(BaseCharacter __instance, SaveData data)
            {
                if (data.isLoading && __instance is FamilyMember member)
                {
                    if (Tracker == null) return;
                    if (!Tracker.IsDataHydrated) return;

                    if (!Tracker.TryGetAgeWeeks(member, out int ageWeeks)) return;
                    bool shouldBeAdult = ageWeeks >= Tracker.AdultAgeWeeks; 

                    if (shouldBeAdult && member.isChild)
                    {
                        if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Info($"Forcing adult status for {member.firstName} on load.");
                        // Force adult status
                        Traverse.Create(member).Field("m_child").SetValue(false);
                    }
                    
                    // Re-apply illness stat modifiers
                    ApplyIllnessStatModifiers(member);
                }
            }
        }

        /// <summary>
        /// Patch to apply illness-related stat modifiers when traits change.
        /// </summary>
        public static class BaseCharacter_OnTraitsChanged_Patch
        {
            public static void Postfix(BaseCharacter __instance)
            {
                if (__instance is FamilyMember member)
                {
                    ApplyIllnessStatModifiers(member);
                }
            }
        }

        public static class FamilyMember_OnFatalDamageTaken_Patch
        {
            public static bool Prefix(BaseCharacter __instance, ref bool __result)
            {
                if (!(__instance is FamilyMember member)) return true;

                string extra = member.lastDamageExtra;
                if (extra == "Old age" || extra == "Natural causes" || extra == "Heart Failure")
                {
                    LifespanPlugin plugin = LifespanPlugin.Instance;
                    if (plugin != null && plugin.Log.IsDebugEnabled) plugin.Log.Debug($"Bypassing unconscious state for natural death: {extra}");
                    __result = true; // Return true to trigger immediate death (OnDeath)
                    return false;    // Skip original method
                }
                return true; // Run original method for other damage types
            }
        }

        internal static void ApplyIllnessStatModifiers(FamilyMember member)
        {
            if (IllnessState == null) return;
            if (IllnessManager == null) return;
            if (object.ReferenceEquals(member, null)) return;

            BaseStats stats = member.BaseStats;
            if (stats == null) return;

            int memberId = member.GetId();
            var illnesses = IllnessState.GetIllnesses(memberId) ?? new List<string>();
            LifespanPlugin plugin = LifespanPlugin.Instance;
            var cfg = plugin != null ? plugin.Config : null;
            IModLogger pluginLog = null;
            if (plugin != null)
            {
                try
                {
                    pluginLog = plugin.Log;
                }
                catch
                {
                    // Tests and early lifecycle callbacks can observe the plugin before its logger is ready.
                }
            }
            bool debugLogging = false;
            if (pluginLog != null)
            {
                try
                {
                    debugLogging = pluginLog.IsDebugEnabled;
                }
                catch
                {
                    // Logging must not prevent illness state from being applied.
                }
            }
 
            DebugLog(pluginLog, debugLogging, $"ApplyIllnessStatModifiers for {member.firstName} (Illnesses: {illnesses.Count})");
            
            IllnessModifierState previous;
            if (!_appliedIllnessModifiers.TryGetValue(memberId, out previous))
            {
                previous = new IllnessModifierState();
            }

            int intMod = 0;
            int strMod = 0;
            int dexMod = 0;

            foreach (var id in illnesses)
            {
                if (string.IsNullOrEmpty(id) || id.Trim().Length == 0) continue;
                if (id == ElderIllnessManager.ILLNESS_DEMENTIA)
                {
                    float intMultiplier = (cfg != null) ? Mathf.Clamp01(cfg.dementiaIntModifier) : 0.5f;
                    int baseInt = stats.Intelligence != null
                        ? Math.Max(0, stats.Intelligence.Level - stats.Intelligence.LevelModifier)
                        : 0;
                    intMod -= Mathf.RoundToInt(baseInt * (1f - intMultiplier));
                    DebugLog(pluginLog, debugLogging, "Dementia detected, applying Intelligence modifier.");
                }
                if (id == ElderIllnessManager.ILLNESS_FRAILTY)
                {
                    float strMultiplier = (cfg != null) ? Mathf.Clamp01(cfg.frailtyStrModifier) : 0.6f;
                    int baseStr = stats.Strength != null
                        ? Math.Max(0, stats.Strength.Level - stats.Strength.LevelModifier)
                        : 0;
                    strMod -= Mathf.RoundToInt(baseStr * (1f - strMultiplier));
                    DebugLog(pluginLog, debugLogging, "Frailty detected, applying Strength modifier.");
                }
                if (id == ElderIllnessManager.ILLNESS_ARTHRITIS)
                {
                    float dexMultiplier = (cfg != null) ? Mathf.Clamp01(cfg.arthritisSpeedModifier) : 0.6f;
                    int baseDex = stats.Dexterity != null
                        ? Math.Max(0, stats.Dexterity.Level - stats.Dexterity.LevelModifier)
                        : 0;
                    dexMod -= Mathf.RoundToInt(baseDex * (1f - dexMultiplier));
                    DebugLog(pluginLog, debugLogging, "Arthritis detected, applying Dexterity modifier.");
                }
            }

            if (stats.Intelligence != null) stats.Intelligence.SetLevelModifier(stats.Intelligence.LevelModifier - previous.Intelligence + intMod);
            if (stats.Strength != null) stats.Strength.SetLevelModifier(stats.Strength.LevelModifier - previous.Strength + strMod);
            if (stats.Dexterity != null) stats.Dexterity.SetLevelModifier(stats.Dexterity.LevelModifier - previous.Dexterity + dexMod);

            if (intMod == 0 && strMod == 0 && dexMod == 0)
            {
                _appliedIllnessModifiers.Remove(memberId);
            }
            else
            {
                _appliedIllnessModifiers[memberId] = new IllnessModifierState
                {
                    Member = member,
                    Intelligence = intMod,
                    Strength = strMod,
                    Dexterity = dexMod
                };
            }
            DebugLog(pluginLog, debugLogging, $"Final modifiers applied for {member.firstName} - Int: {intMod}, Str: {strMod}, Dex: {dexMod}");
        }
    }
}
