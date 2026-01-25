using HarmonyLib;
using ModAPI.Core;
using ModAPI.Reflection;
using System;
using UnityEngine;

namespace Lifespan
{
    public static class TooltipCache
    {
        private static System.Collections.Generic.Dictionary<UI_CharacterTooltip, TooltipData> _cache 
            = new System.Collections.Generic.Dictionary<UI_CharacterTooltip, TooltipData>();

        private class TooltipData
        {
            public FamilyMember Member;
            public UILabel Label;
        }

        public static void OnTooltipShow(UI_CharacterTooltip tooltip)
        {
            if (!_cache.ContainsKey(tooltip))
            {
                var member = ReflectionHelper.GetField<FamilyMember>(tooltip, "m_member");
                var label = ReflectionHelper.GetField<UILabel>(tooltip, "m_name");
                _cache[tooltip] = new TooltipData { Member = member, Label = label };
            }
        }

        public static void OnTooltipHide(UI_CharacterTooltip tooltip)
        {
            if (_cache.ContainsKey(tooltip))
                _cache.Remove(tooltip);
        }

        public static bool TryGetData(UI_CharacterTooltip tooltip, out FamilyMember member, out UILabel label)
        {
            if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] [TooltipCache] TryGetData called.");
            member = null;
            label = null;
            if (_cache.TryGetValue(tooltip, out var data))
            {
                member = data.Member;
                label = data.Label;
                if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] [TooltipCache] Found data in cache.");
                return true;
            }
            
            if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] [TooltipCache] No cache entry found. Using reflection fallback.");
            member = ReflectionHelper.GetField<FamilyMember>(tooltip, "m_member");
            label = ReflectionHelper.GetField<UILabel>(tooltip, "m_name");
            if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write($"[DEBUG] [TooltipCache] Reflection result: member is {(member == null ? "null" : "found")}, label is {(label == null ? "null" : "found")}");
            
            if (member != null && label != null)
            {
                if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] [TooltipCache] Caching new data.");
                _cache[tooltip] = new TooltipData { Member = member, Label = label };
            }
                
            return member != null && label != null;
        }
    }

    public static class UI_CharacterTooltip_UpdateValues_Patch
    {
        public static void Postfix(UI_CharacterTooltip __instance)
        {
            if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] [UIPatch] Postfix entered.");
            try
            {
                FamilyMember member;
                UILabel nameLabel;
                
                if (!TooltipCache.TryGetData(__instance, out member, out nameLabel))
                {
                    if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] [UIPatch] TryGetData returned false. Exiting.");
                    return;
                }

                if (member == null || nameLabel == null)
                {
                    if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] [UIPatch] Member or Label is null after TryGetData. Exiting.");
                    return;
                }

                if (AgingPatches.Tracker == null)
                {
                    if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] [UIPatch] AgingPatches.Tracker is null. Exiting.");
                    return;
                }

                int ageWeeks = AgingPatches.Tracker.GetAgeWeeks(member);
                int ageYears = ageWeeks / 52;
                if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write($"[DEBUG] [UIPatch] Calculated age for {member.firstName}: {ageYears} years ({ageWeeks} weeks).");

                string currentText = nameLabel.text;
                string ageSuffix = $" (Age: {ageYears})";
                if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write($"[DEBUG] [UIPatch] Current label text: '{currentText}'. Desired suffix: '{ageSuffix}'.");

                if (currentText.EndsWith(ageSuffix)) 
                {
                    if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] [UIPatch] Text already correct. Exiting.");
                    return;
                }

                string newText = currentText;
                if (currentText.Contains("(Age:")) 
                {
                    int parenIndex = currentText.LastIndexOf("(Age:");
                    if(parenIndex > 0)
                    {
                        string baseName = currentText.Substring(0, parenIndex).Trim();
                        newText = baseName + ageSuffix;
                    }
                }
                else
                {
                    newText = currentText + ageSuffix;
                }

                if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write($"[DEBUG] [UIPatch] Setting new text: '{newText}'.");
                nameLabel.text = newText;
            }
            catch (Exception ex)
            {
                if (LifespanLoggerExtensions.VerboseEnabled) MMLog.Write("[DEBUG] UIPatch Error: " + ex.ToString());
            }
        }
    }
}