using HarmonyLib;
using ModAPI.Core;
using ModAPI.Core;
using System;
using System.Reflection;
using UnityEngine;

namespace Lifespan
{
    public static class GameOverPatches
    {
        // -------------
        // 1. Data Patch: Capture correct death date AND ID
        // -------------
        public static class FamilyManager_CreateObituaryInfo_Patch
        {
            public static void Postfix(BaseCharacter character, ref FamilyManager.DeadCharacterInfo __result)
            {
                if (__result == null || character == null) return;

                // Ensure ID is set on the info object, so we can look up age by ID later
                if (character is FamilyMember fm)
                {
                    int id = fm.GetId();
                    __result.id = id;

                    // Check for persistent death info in AgeTracker (Primary Source)
                    if (AgingPatches.Tracker != null && AgingPatches.Tracker.TryGetDeathInfo(id, out int day, out int ageWeeks))
                    {
                        if (day > 0)
                        {
                             __result.death_date = day;
                             return;
                        }
                    }
                }

                // fallback override from short-lived field
                if (AgingPatches.TryGetDeathDayOverride(__result.id, out int overrideDay) && overrideDay > 0)
                {
                    __result.death_date = overrideDay;
                }
            }
        }

        // -------------
        // 2. UI Patch: Display correct Date & Age
        // -------------
        public static class ObituaryInfo_SetupObituary_Patch
        {
            public static void Postfix(ObituaryInfo __instance, FamilyManager.DeadCharacterInfo info)
            {
                if (__instance == null || info == null) return;

                try
                {
                    // 1. Find the Labels safely
                    UILabel dayLabel = null;
                    
                    // Use Traverse since Safe.GetField doesn't exist
                    dayLabel = Traverse.Create(__instance).Field("dayLabel").GetValue<UILabel>();
                    
                    // Fallback: Scan children if reflection failed
                    if (dayLabel == null)
                    {
                        var labels = __instance.GetComponentsInChildren<UILabel>(true);
                        foreach (var l in labels)
                        {
                            if (l.text != null && l.text.Contains("Day"))
                            {
                                dayLabel = l;
                                break;
                            }
                        }
                    }

                    if (dayLabel != null)
                    {
                        // 2. Determine correct day and age
                        int finalDay = info.death_date;
                        int ageWeeks = 0;

                        if (AgingPatches.Tracker != null && info.id > -1)
                        {
                            if (AgingPatches.Tracker.TryGetDeathInfo(info.id, out int savedDay, out int savedAgeWeeks))
                            {
                                if (savedDay > 0) finalDay = savedDay;
                                ageWeeks = savedAgeWeeks;
                            }
                            
                            // Fallback to active age if not found (catatonic/missing but not dead)
                            if (ageWeeks == 0)
                            {
                                ageWeeks = AgingPatches.Tracker.GetAgeWeeks(info.id);
                            }
                        }

                        // Fallback to name if ID failed
                        if (ageWeeks == 0 && !string.IsNullOrEmpty(info.first_name) && AgingPatches.Tracker != null)
                        {
                            ageWeeks = AgingPatches.Tracker.GetAgeWeeksByName(info.first_name);
                        }

                        // 3. Format the Date
                        string dateText = "";
                        string unformatted = Localization.Get("Text.UI.Day"); // "Day $day$"
                        if (!string.IsNullOrEmpty(unformatted))
                        {
                            dateText = info.catatonic ? string.Empty : unformatted.Replace("$day$", finalDay.ToString());
                        }
                        else
                        {
                             dateText = "Day " + finalDay.ToString();
                        }

                        // 4. Append Age
                        if (ageWeeks > 0)
                        {
                            int years = ageWeeks / 52;
                            dateText += $" (Age: {years})";
                        }

                        dayLabel.text = dateText;
                    }
                }
                catch (Exception ex)
                {
                    LifespanPlugin.Instance.Log.Error($"Obituary UI Error: {ex.Message}");
                }
            }
        }
        // -------------
        // 3. UI Patch: Top survival counter sync
        // -------------
        public static class GameOverPanel_OnShow_Patch
        {
            public static void Postfix(GameOverPanel __instance)
            {
                if (__instance == null) return;

                try
                {
                    // Find the label safely
                    UILabel daysLastedLabel = Traverse.Create(__instance).Field("daysLastedLabel").GetValue<UILabel>();
                    if (daysLastedLabel != null && AgingPatches.Tracker != null)
                    {
                        // Get the highest death day from our records
                        int maxDay = AgingPatches.Tracker.GetMaxDeathDay();
                        
                        // If we have no records (e.g. they died of hunger on day 1), use vanilla value
                        if (maxDay <= 0) maxDay = GameTime.Day;

                        string key = "Text.GameOver.DaysLasted";
                        string str = Localization.Get(key);
                        if (!string.IsNullOrEmpty(str) && key != str)
                        {
                            daysLastedLabel.text = str.Replace("$day$", maxDay.ToString());
                        }
                        else
                        {
                            daysLastedLabel.text = "They survived until day " + maxDay.ToString();
                        }
                        
                        LifespanPlugin.Instance.Log.Info($"Synced GameOver Survival Counter to Day {maxDay}.");
                    }
                }
                catch (Exception ex)
                {
                    LifespanPlugin.Instance.Log.Error($"GameOver UI sync error: {ex.Message}");
                }
            }
        }
    }
}
