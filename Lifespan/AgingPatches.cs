using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    public static class AgingPatches
    {
        internal static AgeTracker Tracker;
        internal static ElderIllnessManager IllnessManager;
        internal static DeathManager DeathManager;
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
                MMLog.Write("[DEBUG] AgingPatches: GameTime.Awake detected. Re-hooking events and resetting managers.");
                
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
            if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] AgingPatches: GameTime.newWeek fired!");
            OnNewWeekCallback?.Invoke();
        }

        private static void HandleNewDay()
        {
            if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] AgingPatches: GameTime.newDay fired!");
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

                    int ageWeeks = Tracker.GetAgeWeeks(member);
                    bool shouldBeAdult = ageWeeks >= Tracker.AdultAgeWeeks; 

                    if (shouldBeAdult && member.isChild)
                    {
                        if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] AgingPatches: Postfix_SaveLoadCharacter - Forcing adult status for " + member.firstName);
                        // Force adult status
                        Safe.SetField(member, "m_child", false);
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
                    if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] AgingPatches: OnFatalDamageTaken - Bypassing unconscious state for natural death: " + extra);
                    __result = true; // Return true to trigger immediate death (OnDeath)
                    return false;    // Skip original method
                }
                return true; // Run original method for other damage types
            }
        }

        private static void ApplyIllnessStatModifiers(FamilyMember member)
        {
            if (Tracker == null || IllnessManager == null) return;

            var illnesses = Tracker.GetIllnesses(member);
            if (illnesses == null || illnesses.Count == 0) return;

            if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] AgingPatches: ApplyIllnessStatModifiers for " + member.firstName + " (Illnesses: " + illnesses.Count + ")");
            
            int intMod = 0;
            int strMod = 0;

            foreach (var id in illnesses)
            {
                if (id == ElderIllnessManager.ILLNESS_DEMENTIA)
                {
                    intMod -= 3;
                    if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] AgingPatches: Dementia detected, applying int modifier.");
                }
                if (id == ElderIllnessManager.ILLNESS_FRAILTY)
                {
                    strMod -= 2;
                    if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] AgingPatches: Frailty detected, applying str modifier.");
                }
            }

            member.BaseStats.Intelligence.SetLevelModifier(intMod);
            member.BaseStats.Strength.SetLevelModifier(strMod);
            if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] AgingPatches: Final modifiers applied - Int: " + intMod + ", Str: " + strMod);
        }
    }
}
