using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace Lifespan
{
    internal interface IAgeRegistry
    {
        int LastProcessedAgingWeek { get; set; }
        int FamilyCount { get; }
        int ExternalCount { get; }
        bool TryGetAgeWeeks(int id, out int ageWeeks);
        bool TryGetFamilyAgeWeeks(int id, out int ageWeeks);
        bool TryGetExternalAgeWeeks(int id, out int ageWeeks);
        int GetAgeWeeksOrDefault(int id);
        void SetFamilyAgeWeeks(int id, int ageWeeks);
        void SetExternalAgeWeeks(int id, int ageWeeks);
        void RemoveExternalCharacter(int id);
        List<int> GetAllExternalIds();
        List<int> GetFamilyIds();
        void RemoveFamilyMember(int id);
    }

    internal interface IIllnessState
    {
        List<string> GetIllnesses(int id);
        void AddIllness(int id, string illnessId);
        void RemoveIllness(int id, string illnessId);
        void RemoveMember(int id);
        void SetOnsetWeek(int id, string illnessId, int targetWeek);
        int GetOnsetWeek(int id, string illnessId);
    }

    internal interface IGeneticState
    {
        bool TryGetGreyProfile(int id, out GreyProfile profile);
        void SetGreyProfile(int id, GreyProfile profile);
        bool TryGetDevelopmentGene(int id, out DevelopmentGene gene);
        void SetDevelopmentGene(int id, DevelopmentGene gene);
    }

    internal interface IDeathRecordState
    {
        void RecordDeath(int id, int day, int ageWeeks);
        bool TryGetDeathInfo(int id, out int day, out int ageWeeks);
        int GetMaxDeathDay();
    }

    internal interface IMilestoneProgressState
    {
        Dictionary<int, HashSet<string>> TriggeredMilestones { get; }
        Dictionary<int, int> LastBirthdayYears { get; }
    }

    internal interface IDialogueHistoryState
    {
        Dictionary<string, HashSet<int>> History { get; }
    }

    /// <summary>
    /// Owns the hydrated runtime save state and exposes system-specific accessors.
    /// AgeData remains the serialized model; these stores keep domain logic out of that DTO.
    /// </summary>
    internal sealed class LifespanState
    {
        public LifespanState(AgeData data = null)
        {
            Ages = new AgeRegistry(this);
            Illnesses = new IllnessState(this);
            Genetics = new GeneticState(this);
            Deaths = new DeathRecordState(this);
            Milestones = new MilestoneProgressState(this);
            Dialogue = new DialogueHistoryState(this);
            Replace(data);
        }

        public AgeData Data { get; private set; }
        public IAgeRegistry Ages { get; private set; }
        public IIllnessState Illnesses { get; private set; }
        public IGeneticState Genetics { get; private set; }
        public IDeathRecordState Deaths { get; private set; }
        public IMilestoneProgressState Milestones { get; private set; }
        public IDialogueHistoryState Dialogue { get; private set; }

        public void Replace(AgeData data)
        {
            Data = data ?? new AgeData();
        }
    }

    internal sealed class AgeRegistry : IAgeRegistry
    {
        private readonly LifespanState _state;

        public AgeRegistry(LifespanState state)
        {
            _state = state;
        }

        public int LastProcessedAgingWeek
        {
            get { return _state.Data.lastProcessedAgingWeek; }
            set { _state.Data.lastProcessedAgingWeek = value; }
        }

        public int FamilyCount { get { return _state.Data.familyMemberAges.Count; } }
        public int ExternalCount { get { return _state.Data.externalCharacterAges.Count; } }

        public bool TryGetAgeWeeks(int id, out int ageWeeks)
        {
            if (_state.Data.familyMemberAges.TryGetValue(id, out ageWeeks))
            {
                return true;
            }

            if (_state.Data.externalCharacterAges.TryGetValue(id, out ageWeeks))
            {
                return true;
            }

            ageWeeks = 0;
            return false;
        }

        public bool TryGetFamilyAgeWeeks(int id, out int ageWeeks)
        {
            return _state.Data.familyMemberAges.TryGetValue(id, out ageWeeks);
        }

        public bool TryGetExternalAgeWeeks(int id, out int ageWeeks)
        {
            return _state.Data.externalCharacterAges.TryGetValue(id, out ageWeeks);
        }

        public int GetAgeWeeksOrDefault(int id)
        {
            int ageWeeks;
            return TryGetAgeWeeks(id, out ageWeeks) ? ageWeeks : 0;
        }

        public void SetFamilyAgeWeeks(int id, int ageWeeks)
        {
            _state.Data.familyMemberAges[id] = Math.Max(0, ageWeeks);
            _state.Data.externalCharacterAges.Remove(id);
        }

        public void SetExternalAgeWeeks(int id, int ageWeeks)
        {
            _state.Data.externalCharacterAges[id] = Math.Max(0, ageWeeks);
        }

        public void RemoveExternalCharacter(int id)
        {
            _state.Data.externalCharacterAges.Remove(id);
        }

        public List<int> GetAllExternalIds()
        {
            return new List<int>(_state.Data.externalCharacterAges.Keys);
        }

        public List<int> GetFamilyIds()
        {
            return new List<int>(_state.Data.familyMemberAges.Keys);
        }

        public void RemoveFamilyMember(int id)
        {
            _state.Data.familyMemberAges.Remove(id);
        }
    }

    internal sealed class IllnessState : IIllnessState
    {
        private readonly LifespanState _state;

        public IllnessState(LifespanState state)
        {
            _state = state;
        }

        public List<string> GetIllnesses(int id)
        {
            List<string> illnesses;
            if (!_state.Data.elderIllnesses.TryGetValue(id, out illnesses))
            {
                illnesses = new List<string>();
                _state.Data.elderIllnesses[id] = illnesses;
            }

            return illnesses;
        }

        public void AddIllness(int id, string illnessId)
        {
            var illnesses = GetIllnesses(id);
            if (!illnesses.Contains(illnessId))
            {
                illnesses.Add(illnessId);
            }
        }

        public void RemoveIllness(int id, string illnessId)
        {
            List<string> illnesses;
            if (!_state.Data.elderIllnesses.TryGetValue(id, out illnesses))
            {
                return;
            }

            illnesses.Remove(illnessId);

            Dictionary<string, int> timing;
            if (_state.Data.onsetTiming.TryGetValue(id, out timing))
            {
                timing.Remove(illnessId);
            }
        }

        public void RemoveMember(int id)
        {
            _state.Data.elderIllnesses.Remove(id);
            _state.Data.onsetTiming.Remove(id);
        }

        public void SetOnsetWeek(int id, string illnessId, int targetWeek)
        {
            Dictionary<string, int> timing;
            if (!_state.Data.onsetTiming.TryGetValue(id, out timing))
            {
                timing = new Dictionary<string, int>();
                _state.Data.onsetTiming[id] = timing;
            }

            timing[illnessId] = targetWeek;
        }

        public int GetOnsetWeek(int id, string illnessId)
        {
            Dictionary<string, int> timing;
            int week;
            if (_state.Data.onsetTiming.TryGetValue(id, out timing) && timing.TryGetValue(illnessId, out week))
            {
                return week;
            }

            return -1;
        }
    }

    internal sealed class GeneticState : IGeneticState
    {
        private readonly LifespanState _state;

        public GeneticState(LifespanState state)
        {
            _state = state;
        }

        public bool TryGetGreyProfile(int id, out GreyProfile profile)
        {
            return _state.Data.greyProfiles.TryGetValue(id, out profile);
        }

        public void SetGreyProfile(int id, GreyProfile profile)
        {
            _state.Data.greyProfiles[id] = profile;
        }

        public bool TryGetDevelopmentGene(int id, out DevelopmentGene gene)
        {
            return _state.Data.developmentGenes.TryGetValue(id, out gene);
        }

        public void SetDevelopmentGene(int id, DevelopmentGene gene)
        {
            _state.Data.developmentGenes[id] = gene;
        }
    }

    internal static class GeneticStateExtensions
    {
        public static DevelopmentGene GetOrGenerateDevelopmentGene(this IGeneticState geneticState, int id, ModRandomStream random)
        {
            if (geneticState == null || random == null) return null;

            DevelopmentGene existing;
            if (geneticState.TryGetDevelopmentGene(id, out existing))
            {
                return existing;
            }

            var gene = DevelopmentGene.GenerateRandom(random);
            geneticState.SetDevelopmentGene(id, gene);
            return gene;
        }
    }

    internal sealed class DeathRecordState : IDeathRecordState
    {
        private readonly LifespanState _state;

        public DeathRecordState(LifespanState state)
        {
            _state = state;
        }

        public void RecordDeath(int id, int day, int ageWeeks)
        {
            _state.Data.deceasedDeathDays[id] = day;
            _state.Data.deceasedDeathAges[id] = ageWeeks;
        }

        public bool TryGetDeathInfo(int id, out int day, out int ageWeeks)
        {
            bool foundDay = _state.Data.deceasedDeathDays.TryGetValue(id, out day);
            bool foundAge = _state.Data.deceasedDeathAges.TryGetValue(id, out ageWeeks);
            return foundDay || foundAge;
        }

        public int GetMaxDeathDay()
        {
            int max = 0;
            foreach (var day in _state.Data.deceasedDeathDays.Values)
            {
                if (day > max) max = day;
            }

            return max;
        }
    }

    internal sealed class MilestoneProgressState : IMilestoneProgressState
    {
        private readonly LifespanState _state;

        public MilestoneProgressState(LifespanState state)
        {
            _state = state;
        }

        public Dictionary<int, HashSet<string>> TriggeredMilestones
        {
            get { return _state.Data.triggeredMilestones; }
        }

        public Dictionary<int, int> LastBirthdayYears
        {
            get { return _state.Data.lastBirthdayYear; }
        }
    }

    internal sealed class DialogueHistoryState : IDialogueHistoryState
    {
        private readonly LifespanState _state;

        public DialogueHistoryState(LifespanState state)
        {
            _state = state;
        }

        public Dictionary<string, HashSet<int>> History
        {
            get { return _state.Data.dialogueHistory; }
        }
    }
}
