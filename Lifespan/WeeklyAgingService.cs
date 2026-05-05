using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Events;
using ModAPI.Util;

namespace Lifespan
{
    internal sealed class WeeklyAgingService
    {
        private readonly IModLogger _log;
        private readonly LifespanConfig _config;
        private readonly AgeTracker _ageTracker;
        private readonly WeeklyAgingOperations _operations;

        internal WeeklyAgingService(
            IModLogger log,
            LifespanConfig config,
            AgeTracker ageTracker,
            ChildTransitionManager childManager,
            ElderIllnessManager illnessManager,
            DeathManager deathManager,
            DevelopmentGeneManager devGeneManager,
            MilestoneManager milestoneManager,
            Func<LifespanAPIImpl> apiProvider,
            Action<FamilyMember, int> processHairGreying)
            : this(
                log,
                config,
                ageTracker,
                new WeeklyAgingOperations
                {
                    GetCurrentDay = () => GameTime.Day,
                    GetCurrentWeek = () => GameTime.Week,
                    HasFamilyManager = () => FamilyManager.Instance != null,
                    GetFamilyMembers = () => FamilyManager.Instance.GetAllFamilyMembers(),
                    ShouldCancelAging = character =>
                    {
                        LifespanAPIImpl api = apiProvider != null ? apiProvider() : null;
                        return api != null && api.ShouldCancelAging(character);
                    },
                    TransitionToAdult = member => childManager.TransitionToAdult(member),
                    ProcessElderIllnessRoll = (member, ageWeeks) => illnessManager.ProcessElderIllnessRoll(member, ageWeeks),
                    ProcessHairGreying = processHairGreying,
                    ProcessDevelopment = (member, ageWeeks, weeksAged) => devGeneManager.ProcessDevelopment(member, ageWeeks, weeksAged),
                    ProcessMilestones = (member, ageWeeks) => milestoneManager.ProcessMilestones(member, ageWeeks),
                    ProcessDeathRoll = (member, ageWeeks, elapsedWeeks) => deathManager.ProcessDeathRoll(member, ageWeeks, elapsedWeeks),
                    CleanupMissingMembers = () => ageTracker.CleanupMissingMembers(),
                    HasExternalCharacterAging = () =>
                    {
                        LifespanAPIImpl api = apiProvider != null ? apiProvider() : null;
                        return api != null;
                    },
                    UpdateExternalCharacters = weeks =>
                    {
                        LifespanAPIImpl api = apiProvider != null ? apiProvider() : null;
                        if (api != null) api.UpdateExternalCharacters(weeks);
                    },
                    ForceAvatarUpdate = () =>
                    {
                        if (InteractionManager.Instance != null)
                        {
                            InteractionManager.Instance.m_forceAvatarUpdate = true;
                        }
                    },
                    PublishCharacterAged = (member, ageWeeks) =>
                        ModEventBus.Publish("Lifespan.CharacterAgedUp", new CharacterAgedUpArgs(member, ageWeeks))
                })
        {
        }

        internal WeeklyAgingService(
            IModLogger log,
            LifespanConfig config,
            AgeTracker ageTracker,
            WeeklyAgingOperations operations)
        {
            _log = log;
            _config = config;
            _ageTracker = ageTracker;
            _operations = operations ?? new WeeklyAgingOperations();
        }

        public void ProcessNewWeek()
        {
            int currentDay = _operations.GetCurrentDay();
            int currentWeek = _operations.GetCurrentWeek();

            if (_log.IsDebugEnabled) _log.Debug($"[WeeklyAging] New week event received. Day={currentDay}, Week={currentWeek}.");
            if (!_operations.HasFamilyManager())
            {
                _log.Warn("FamilyManager.Instance is null. Skipping aging cycle.");
                return;
            }

            if (_ageTracker == null)
            {
                _log.Warn("AgeTracker is null. Skipping this weekly aging cycle.");
                return;
            }

            if (!_ageTracker.IsDataHydrated)
            {
                _log.Warn("AgeTracker is not hydrated yet. Skipping this weekly aging cycle.");
                return;
            }

            _config.ValidateAndClamp();

            if (currentWeek % _config.agingIntervalWeeks != 0)
            {
                if (_log.IsDebugEnabled) _log.Debug($"Skipping aging this week (Interval: {_config.agingIntervalWeeks}).");
                return;
            }

            int lastProcessedWeek = _ageTracker.LastProcessedAgingWeek;
            if (!WeeklyAgingPolicy.ShouldProcessAgingWeek(currentWeek, lastProcessedWeek, out string weekSkipReason))
            {
                _log.Warn($"[WeeklyAging] Skipping weekly aging because {weekSkipReason}. Day={currentDay}, Week={currentWeek}, LastProcessedWeek={lastProcessedWeek}.");
                return;
            }

            if (_log.IsDebugEnabled) _log.Debug($"[WeeklyAging] Processing aging for members (+{_config.weeksAgedPerInterval} weeks).");

            List<FamilyMember> members = _operations.GetFamilyMembers();
            if (members == null)
            {
                _log.Warn("FamilyManager returned a null list of members. Skipping cycle.");
                return;
            }

            _ageTracker.MarkAgingWeekProcessed(currentWeek);

            if (_log.IsDebugEnabled)
            {
                _log.Debug($"[WeeklyAging] Starting pass. Day={currentDay}, Week={currentWeek}, MemberCount={members.Count}, WeeksToAdd={_config.weeksAgedPerInterval}.");
            }

            foreach (FamilyMember member in members)
            {
                if (_log.IsDebugEnabled) _log.Debug($"[WeeklyAging] Evaluating member: {(!_operations.IsNullMember(member) ? member.firstName : "NULL MEMBER")}.");
                if (_operations.IsNullMember(member))
                {
                    _log.Warn("Member in list is null. Skipping.");
                    continue;
                }

                if (WeeklyAgingPolicy.ShouldSkipWeeklyAging(member, _operations.ShouldCancelAging, out string memberSkipReason))
                {
                    LogWeeklyAgingSkip(member, memberSkipReason);
                    continue;
                }

                int previousAgeWeeks = _ageTracker.GetAgeWeeks(member);
                int newAgeWeeks = _ageTracker.IncrementAge(member, _config.weeksAgedPerInterval);
                int weeksAdded = newAgeWeeks - previousAgeWeeks;
                int elapsedBiologicalWeeks = Math.Max(1, weeksAdded);

                LogWeeklyAgingIncrement(member, previousAgeWeeks, newAgeWeeks, weeksAdded);

                if (_log.IsDebugEnabled) _log.Debug($"Checking child transition for '{member.firstName}'. (IsChild: {member.isChild}, Age: {newAgeWeeks} weeks, Threshold: {_config.adultAgeYears * LifespanConstants.WeeksPerYear} weeks)");
                int adultWeeks = _config.adultAgeYears * LifespanConstants.WeeksPerYear;
                if (member.isChild && newAgeWeeks >= adultWeeks)
                {
                    _log.Info($"{member.firstName} has reached adulthood.");
                    _operations.TransitionToAdult(member);
                }

                if (_log.IsDebugEnabled) _log.Debug($"Checking elder illness for '{member.firstName}'. (Age: {newAgeWeeks} weeks, Threshold: {_config.elderAgeYears * LifespanConstants.WeeksPerYear} weeks)");
                int elderWeeks = _config.elderAgeYears * LifespanConstants.WeeksPerYear;
                if (newAgeWeeks >= elderWeeks)
                {
                    if (_log.IsDebugEnabled) _log.Debug($"{member.firstName} is an elder. Processing illness roll...");
                    _operations.ProcessElderIllnessRoll(member, newAgeWeeks);
                }

                if (_log.IsDebugEnabled) _log.Debug($"Processing hair greying, development, and milestones for '{member.firstName}'.");
                _operations.ProcessHairGreying(member, newAgeWeeks);
                _operations.ProcessDevelopment(member, newAgeWeeks, _config.weeksAgedPerInterval);
                _operations.ProcessMilestones(member, newAgeWeeks);

                if (member.isDead)
                {
                    _log.Debug($"Member '{member.firstName}' died during an illness or development step.");
                    continue;
                }

                if (_log.IsDebugEnabled) _log.Debug($"Processing death roll for '{member.firstName}'.");

                if (_config.enableNaturalDeath)
                {
                    _operations.ProcessDeathRoll(member, newAgeWeeks, elapsedBiologicalWeeks);
                }

                if (!member.isDead)
                {
                    if (_log.IsDebugEnabled) _log.Debug($"Member '{member.firstName}' survived. Publishing event.");
                    _operations.PublishCharacterAged(member, newAgeWeeks);
                }
                else
                {
                    _log.Info($"{member.firstName} died of old age.");
                }

                if (_log.IsDebugEnabled) _log.Debug($"[WeeklyAging] Finished processing member: {member.firstName}.");
            }

            if (_log.IsDebugEnabled) _log.Debug("[WeeklyAging] Cycle complete. Cleaning up missing members from tracker.");
            _operations.CleanupMissingMembers();

            if (_operations.HasExternalCharacterAging())
            {
                if (_log.IsDebugEnabled) _log.Debug("[WeeklyAging] Processing aging for external characters (NPCs).");
                _operations.UpdateExternalCharacters(_config.weeksAgedPerInterval);
            }

            _operations.ForceAvatarUpdate();
        }

        private void LogWeeklyAgingSkip(FamilyMember member, string reason)
        {
            if (!_log.IsDebugEnabled) return;

            int previousAgeWeeks = -1;
            _ageTracker?.TryGetAgeWeeks(member, out previousAgeWeeks);

            _log.Debug(
                $"[WeeklyAging] Skipped member. Reason={reason}, Name={member.firstName}, Id={member.GetId()}, " +
                $"isAway={member.isAway}, finishedLeavingShelter={member.finishedLeavingShelter}, " +
                $"previousAgeWeeks={previousAgeWeeks}, newAgeWeeks={previousAgeWeeks}, weeksAdded=0.");
        }

        private void LogWeeklyAgingIncrement(FamilyMember member, int previousAgeWeeks, int newAgeWeeks, int weeksAdded)
        {
            if (!_log.IsDebugEnabled) return;

            _log.Debug(
                $"[WeeklyAging] Aged member. Name={member.firstName}, Id={member.GetId()}, " +
                $"isAway={member.isAway}, finishedLeavingShelter={member.finishedLeavingShelter}, " +
                $"previousAgeWeeks={previousAgeWeeks}, newAgeWeeks={newAgeWeeks}, weeksAdded={weeksAdded}.");
        }
    }

    internal sealed class WeeklyAgingOperations
    {
        public Func<int> GetCurrentDay = () => 0;
        public Func<int> GetCurrentWeek = () => 0;
        public Func<bool> HasFamilyManager = () => false;
        public Func<List<FamilyMember>> GetFamilyMembers = () => null;
        public Func<FamilyMember, bool> IsNullMember = member => member == null;
        public Func<BaseCharacter, bool> ShouldCancelAging = character => false;
        public Action<FamilyMember> TransitionToAdult = member => { };
        public Action<FamilyMember, int> ProcessElderIllnessRoll = (member, ageWeeks) => { };
        public Action<FamilyMember, int> ProcessHairGreying = (member, ageWeeks) => { };
        public Action<FamilyMember, int, int> ProcessDevelopment = (member, ageWeeks, weeksAged) => { };
        public Action<FamilyMember, int> ProcessMilestones = (member, ageWeeks) => { };
        public Action<FamilyMember, int, int> ProcessDeathRoll = (member, ageWeeks, elapsedWeeks) => { };
        public Action CleanupMissingMembers = () => { };
        public Func<bool> HasExternalCharacterAging = () => false;
        public Action<int> UpdateExternalCharacters = weeks => { };
        public Action ForceAvatarUpdate = () => { };
        public Action<FamilyMember, int> PublishCharacterAged = (member, ageWeeks) => { };
    }
}
