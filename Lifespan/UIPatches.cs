using HarmonyLib;
using ModAPI.Core;
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
                var member = Traverse.Create(tooltip).Field("m_member").GetValue<FamilyMember>();
                var label = Traverse.Create(tooltip).Field("m_name").GetValue<UILabel>();
                _cache[tooltip] = new TooltipData { Member = member, Label = label };
            }
        }

        public static void OnTooltipHide(UI_CharacterTooltip tooltip)
        {
            if (_cache.ContainsKey(tooltip))
                _cache.Remove(tooltip);
        }

        public static void Clear()
        {
            _cache.Clear();
        }

        public static bool TryGetData(UI_CharacterTooltip tooltip, out FamilyMember member, out UILabel label)
        {
            if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("[TooltipCache] TryGetData called.");
            member = null;
            label = null;
            if (_cache.TryGetValue(tooltip, out var data))
            {
                member = data.Member;
                label = data.Label;
                if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("[TooltipCache] Found data in cache.");
                return true;
            }
            
            if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("[TooltipCache] No cache entry found. Using reflection fallback.");
            member = Traverse.Create(tooltip).Field("m_member").GetValue<FamilyMember>();
            label = Traverse.Create(tooltip).Field("m_name").GetValue<UILabel>();
            if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug($"[TooltipCache] Reflection result: member is {(member == null ? "null" : "found")}, label is {(label == null ? "null" : "found")}");
            
            if (member != null && label != null)
            {
                if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("[TooltipCache] Caching new data.");
                _cache[tooltip] = new TooltipData { Member = member, Label = label };
            }
                
            return member != null && label != null;
        }
    }

    public static class UI_CharacterTooltip_UpdateValues_Patch
    {
        public static void Postfix(UI_CharacterTooltip __instance)
        {
            if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("[UIPatch] Postfix entered.");
            try
            {
                FamilyMember member;
                UILabel nameLabel;
                
                if (!TooltipCache.TryGetData(__instance, out member, out nameLabel))
                {
                    if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("[UIPatch] TryGetData returned false. Exiting.");
                    return;
                }

                if (member == null || nameLabel == null)
                {
                    if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("[UIPatch] Member or Label is null after TryGetData. Exiting.");
                    return;
                }

                if (AgingPatches.Tracker == null)
                {
                    if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("[UIPatch] AgingPatches.Tracker is null. Exiting.");
                    return;
                }

                int ageWeeks = AgingPatches.Tracker.GetAgeWeeks(member);
                int ageYears = ageWeeks / LifespanConstants.WeeksPerYear;
                if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug($"[UIPatch] Calculated age for {member.firstName}: {ageYears} years ({ageWeeks} weeks).");

                string currentText = nameLabel.text;
                string ageSuffix = $" (Age: {ageYears})";
                if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug($"[UIPatch] Current label text: '{currentText}'. Desired suffix: '{ageSuffix}'.");

                if (currentText.EndsWith(ageSuffix)) 
                {
                    if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug("[UIPatch] Text already correct. Exiting.");
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

                if (LifespanPlugin.Instance.Log.IsDebugEnabled) LifespanPlugin.Instance.Log.Debug($"[UIPatch] Setting new text: '{newText}'.");
                nameLabel.text = newText;
            }
            catch (Exception ex)
            {
                // Note: Don't check for DebugEnabled for Errors, usually
                LifespanPlugin.Instance.Log.Error("UIPatch Error: " + ex.ToString());
            }
        }
    }

    public static class UI_CharacterTooltip_HideTooltip_Patch
    {
        public static void Postfix(UI_CharacterTooltip __instance)
        {
            TooltipCache.OnTooltipHide(__instance);
        }
    }
}
