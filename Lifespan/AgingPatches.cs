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
            public static bool Prefix(FamilyMember __instance, ref bool __result)
            {
                string extra = __instance.lastDamageExtra;
                if (extra == "Old age" || extra == "Natural causes" || extra == "Heart Failure")
                {
                    if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug($"Bypassing unconscious state for natural death: {extra}");
                    __result = true; // Return true to trigger immediate death (OnDeath)
                    return false;    // Skip original method
                }
                return true; // Run original method for other damage types
            }
        }

        internal static void ApplyIllnessStatModifiers(FamilyMember member)
        {
            if (IllnessState == null || IllnessManager == null || member?.BaseStats == null) return;

            var illnesses = IllnessState.GetIllnesses(member.GetId());
            if (illnesses == null || illnesses.Count == 0) return;
            var cfg = LifespanPlugin.Instance?.Config;
 
            if (LifespanPlugin.Instance != null && LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug($"ApplyIllnessStatModifiers for {member.firstName} (Illnesses: {illnesses.Count})");
            
            int intMod = 0;
            int strMod = 0;
            int dexMod = 0;

            foreach (var id in illnesses)
            {
                if (id == ElderIllnessManager.ILLNESS_DEMENTIA)
                {
                    float intMultiplier = (cfg != null) ? Mathf.Clamp01(cfg.dementiaIntModifier) : 0.5f;
                    int baseInt = member.BaseStats.Intelligence != null ? member.BaseStats.Intelligence.Level : 0;
                    intMod -= Mathf.RoundToInt(baseInt * (1f - intMultiplier));
                    if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("Dementia detected, applying Intelligence modifier.");
                }
                if (id == ElderIllnessManager.ILLNESS_FRAILTY)
                {
                    float strMultiplier = (cfg != null) ? Mathf.Clamp01(cfg.frailtyStrModifier) : 0.6f;
                    int baseStr = member.BaseStats.Strength != null ? member.BaseStats.Strength.Level : 0;
                    strMod -= Mathf.RoundToInt(baseStr * (1f - strMultiplier));
                    if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("Frailty detected, applying Strength modifier.");
                }
                if (id == ElderIllnessManager.ILLNESS_ARTHRITIS)
                {
                    float dexMultiplier = (cfg != null) ? Mathf.Clamp01(cfg.arthritisSpeedModifier) : 0.6f;
                    int baseDex = member.BaseStats.Dexterity != null ? member.BaseStats.Dexterity.Level : 0;
                    dexMod -= Mathf.RoundToInt(baseDex * (1f - dexMultiplier));
                    if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("Arthritis detected, applying Dexterity modifier.");
                }
            }

            if (member.BaseStats.Intelligence != null) member.BaseStats.Intelligence.SetLevelModifier(intMod);
            if (member.BaseStats.Strength != null) member.BaseStats.Strength.SetLevelModifier(strMod);
            if (member.BaseStats.Dexterity != null) member.BaseStats.Dexterity.SetLevelModifier(dexMod);
            if (LifespanPlugin.Instance != null && LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug($"Final modifiers applied for {member.firstName} - Int: {intMod}, Str: {strMod}, Dex: {dexMod}");
        }
    }
}
