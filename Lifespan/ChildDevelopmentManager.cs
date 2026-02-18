using ModAPI.Core;
using System;
using UnityEngine;

namespace Lifespan
{
    /// <summary>
    /// Manages the developmental stages of children, determining their capabilities based on age.
    /// Integrated with AgeTracker and LifespanConfig.
    /// </summary>
    public class ChildDevelopmentManager
    {
        private readonly IPluginContext _ctx;
        private LifespanConfig _config;
        private readonly AgeTracker _ageTracker;
        private readonly IModLogger _log;

        public ChildDevelopmentManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker)
        {
            _ctx = ctx;
            _config = config;
            _ageTracker = ageTracker;
            _log = ctx.Log;
        }

        public void RefreshSettings(LifespanConfig config)
        {
            _config = config;
        }

        public ChildStage GetStage(FamilyMember member)
        {
            if (member == null) return ChildStage.Adult;

            // If not enabled or config missing, default to normal behavior
            if (!_config.enableChildDevelopment)
            {
                var fallback = member.isChild ? ChildStage.Child : ChildStage.Adult;
                _log.Debug($"[MANAGER] Development Disabled. Member isChild: {member.isChild} -> {fallback}");
                return fallback;
            }

            int ageWeeks = _ageTracker.GetAgeWeeks(member);
            int ageYears = ageWeeks / 52;

            if (ageYears >= _config.adultAgeYears)
            {
                var result = ageYears >= _config.elderAgeYears ? ChildStage.Elder : ChildStage.Adult;
                _log.Debug($"[MANAGER] Adult check: AgeWeeks={ageWeeks}, Years={ageYears}, AdultThr={_config.adultAgeYears} -> {result}");
                return result;
            }
            
            _log.Debug($"[MANAGER] Child check: AgeWeeks={ageWeeks}, Years={ageYears}, MobileThr={_config.mobileAgeYears}");

            if (ageYears >= _config.expeditionMinAgeSolo)
                return ChildStage.Teen;

            if (ageYears >= _config.expeditionMinAgeAccompanied)
                return ChildStage.PreTeen;

            if (ageYears >= _config.mobileAgeYears)
                return ChildStage.Child;

            return ChildStage.Newborn;
        }

        public bool CanMove(FamilyMember member)
        {
            if (!_config.enableChildDevelopment) return true;
            ChildStage stage = GetStage(member);
            return stage != ChildStage.Newborn;
        }

        public bool CanDoJobs(FamilyMember member)
        {
            if (!_config.enableChildDevelopment) return true;
            int ageYears = _ageTracker.GetAgeYears(member);
            return ageYears >= _config.childJobAgeYears;
        }

        public bool CanGoOnExpedition(FamilyMember member, bool hasAdultAccompaniment)
        {
            if (!_config.enableChildDevelopment) return true; // Fallback to vanilla logic (which might block children anyway)

            int ageYears = _ageTracker.GetAgeYears(member);
            
            if (ageYears >= _config.expeditionMinAgeSolo) return true;
            
            if (ageYears >= _config.expeditionMinAgeAccompanied)
                return hasAdultAccompaniment;

            return false;
        }

        /// <summary>
        /// Checks if the member needs to be fed by others (is immobile/helpless).
        /// </summary>
        public bool NeedsFeeding(FamilyMember member)
        {
             if (!_config.enableChildDevelopment) return false;
             return GetStage(member) == ChildStage.Newborn;
        }

        /// <summary>
        /// Checks if the member is old enough to lead an expedition or go solo (default 13+).
        /// </summary>
        public bool IsSoloExpeditionCapable(FamilyMember member)
        {
            if (!_config.enableChildDevelopment) return true;
            int ageYears = _ageTracker.GetAgeYears(member);
            return ageYears >= _config.expeditionMinAgeSolo;
        }
    }
}
