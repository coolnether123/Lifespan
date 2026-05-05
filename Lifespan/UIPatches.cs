using HarmonyLib;
using ModAPI.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifespan
{
    internal static class TooltipAgeFormatter
    {
        private const string AgeMarker = "(Age:";

        internal static string GetBaseName(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            int markerIndex = text.LastIndexOf(AgeMarker, StringComparison.Ordinal);
            if (markerIndex <= 0) return text.Trim();

            return text.Substring(0, markerIndex).Trim();
        }

        internal static string Format(string text, int ageYears)
        {
            return GetBaseName(text) + " (Age: " + ageYears + ")";
        }
    }

    public static class TooltipCache
    {
        private static Dictionary<UI_CharacterTooltip, TooltipData> _cache 
            = new Dictionary<UI_CharacterTooltip, TooltipData>();

        private class TooltipData
        {
            public FamilyMember Member;
            public UILabel Label;
            public string BaseName;
            public int LastAgeYears = -1;
            public string LastAppliedText;
        }

        public static void OnTooltipShow(UI_CharacterTooltip tooltip)
        {
            if (!_cache.ContainsKey(tooltip))
            {
                var member = Traverse.Create(tooltip).Field("m_member").GetValue<FamilyMember>();
                var label = Traverse.Create(tooltip).Field("m_name").GetValue<UILabel>();
                _cache[tooltip] = CreateData(member, label);
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
            member = null;
            label = null;
            if (_cache.TryGetValue(tooltip, out var data))
            {
                member = data.Member;
                label = data.Label;
                return true;
            }
            
            member = Traverse.Create(tooltip).Field("m_member").GetValue<FamilyMember>();
            label = Traverse.Create(tooltip).Field("m_name").GetValue<UILabel>();
            
            if (member != null && label != null)
            {
                _cache[tooltip] = CreateData(member, label);
            }
                
            return member != null && label != null;
        }

        public static bool TryApplyAge(UI_CharacterTooltip tooltip, int ageYears)
        {
            TooltipData data = GetOrCreateData(tooltip);
            if (data == null || data.Member == null || data.Label == null) return false;

            string currentText = data.Label.text ?? string.Empty;
            if (data.LastAppliedText == null || currentText != data.LastAppliedText)
            {
                data.BaseName = TooltipAgeFormatter.GetBaseName(currentText);
                data.LastAgeYears = -1;
            }

            if (data.LastAgeYears == ageYears && currentText == data.LastAppliedText)
            {
                return true;
            }

            string newText = TooltipAgeFormatter.Format(data.BaseName, ageYears);
            if (currentText != newText)
            {
                data.Label.text = newText;
            }

            data.LastAgeYears = ageYears;
            data.LastAppliedText = newText;
            return true;
        }

        private static TooltipData GetOrCreateData(UI_CharacterTooltip tooltip)
        {
            TooltipData data;
            if (_cache.TryGetValue(tooltip, out data))
            {
                return data;
            }

            FamilyMember member;
            UILabel label;
            TryGetData(tooltip, out member, out label);
            _cache.TryGetValue(tooltip, out data);
            return data;
        }

        private static TooltipData CreateData(FamilyMember member, UILabel label)
        {
            return new TooltipData
            {
                Member = member,
                Label = label,
                BaseName = TooltipAgeFormatter.GetBaseName(label != null ? label.text : null),
                LastAgeYears = -1,
                LastAppliedText = null
            };
        }
    }

    public static class UI_CharacterTooltip_UpdateValues_Patch
    {
        public static void Postfix(UI_CharacterTooltip __instance)
        {
            try
            {
                FamilyMember member;
                UILabel nameLabel;
                
                if (!TooltipCache.TryGetData(__instance, out member, out nameLabel))
                {
                    return;
                }

                if (member == null || nameLabel == null)
                {
                    return;
                }

                if (AgingPatches.Tracker == null)
                {
                    return;
                }

                int ageWeeks = AgingPatches.Tracker.GetAgeWeeks(member);
                int ageYears = ageWeeks / LifespanConstants.WeeksPerYear;
                TooltipCache.TryApplyAge(__instance, ageYears);
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
