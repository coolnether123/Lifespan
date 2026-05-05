using ModAPI.Core;
using System.Collections.Generic;
using Lifespan.Dialogue.Content;

namespace Lifespan
{
    /// <summary>
    /// Manages dialogue related to expeditions, including departures and shelter-dwellers
    /// reflecting on those who are currently away on missions.
    /// </summary>
    public class ExpeditionDialogueManager
    {
        private readonly LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly AgeTracker _ageTracker;
        private readonly ElderIllnessManager _illnessManager;
        private readonly DialogueScheduler _scheduler;
        private readonly DialogueHelper _dialogueHelper;
        private readonly ModRandomStream _random;

        private readonly HashSet<int> _departingProcessed = new HashSet<int>();
        private readonly Dictionary<int, float> _awaySince = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _nextAwayObservationCheck = new Dictionary<int, float>();
        private float _nextFamilyScanAt;

        private const float DepartureDialogueChance = 0.25f;
        private const float FamilyScanIntervalSeconds = 2f;
        private const float AwayObservationMinDelaySeconds = 360f;
        private const float AwayObservationSoftDurationSeconds = 1200f;
        private const float AwayObservationChancePerCheck = 0.08f;

        public ExpeditionDialogueManager(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ElderIllnessManager illnessManager, DialogueScheduler scheduler, DialogueHelper dialogueHelper, ModRandomStream random)
        {
            _config = config;
            _log = ctx.Log;
            _ageTracker = ageTracker;
            _illnessManager = illnessManager;
            _scheduler = scheduler;
            _dialogueHelper = dialogueHelper;
            _random = random;
        }

        public void Update()
        {
            if (FamilyManager.Instance == null) return;
            float now = UnityEngine.Time.time;
            if (now < _nextFamilyScanAt) return;
            _nextFamilyScanAt = now + FamilyScanIntervalSeconds;

            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return;
            HashSet<int> activeIds = new HashSet<int>();

            foreach (var member in members)
            {
                if (member == null || member.isDead) continue;

                bool isDeparting = ExpeditionStateHelper.IsDepartingOrAway(member) && !member.isAway;
                int id = member.GetId();
                activeIds.Add(id);

                if (isDeparting)
                {
                    if (!_departingProcessed.Contains(id))
                    {
                        _departingProcessed.Add(id);
                        CheckAndTriggerDepartureDialogue(member);
                    }
                }
                else
                {
                    _departingProcessed.Remove(id);

                    if (member.isAway)
                    {
                        if (!_awaySince.ContainsKey(id))
                        {
                            _awaySince[id] = now;
                        }

                        // Rate-limit checks per away member to avoid frame-based spam.
                        if (!_nextAwayObservationCheck.ContainsKey(id) || now >= _nextAwayObservationCheck[id])
                        {
                            _nextAwayObservationCheck[id] = now + _random.Range(120f, 210f);
                            float awayDuration = now - _awaySince[id];

                            // Avoid "lost/danger" chatter immediately after departure.
                            if (awayDuration >= AwayObservationMinDelaySeconds && _random.Value() < AwayObservationChancePerCheck)
                            {
                                TriggerAwayObservation(member, awayDuration);
                            }
                        }
                    }
                    else
                    {
                        _awaySince.Remove(id);
                        _nextAwayObservationCheck.Remove(id);
                    }
                }
            }

            PruneMissingMembers(activeIds);
        }

        private void PruneMissingMembers(HashSet<int> activeIds)
        {
            PruneIds(_departingProcessed, activeIds);
            PruneIds(_awaySince, activeIds);
            PruneIds(_nextAwayObservationCheck, activeIds);
        }

        private void PruneIds(HashSet<int> ids, HashSet<int> activeIds)
        {
            if (ids == null || ids.Count == 0) return;

            List<int> stale = null;
            foreach (int id in ids)
            {
                if (!activeIds.Contains(id))
                {
                    if (stale == null) stale = new List<int>();
                    stale.Add(id);
                }
            }

            if (stale == null) return;
            foreach (int id in stale)
            {
                ids.Remove(id);
            }
        }

        private void PruneIds(Dictionary<int, float> ids, HashSet<int> activeIds)
        {
            if (ids == null || ids.Count == 0) return;

            List<int> stale = null;
            foreach (var kvp in ids)
            {
                if (!activeIds.Contains(kvp.Key))
                {
                    if (stale == null) stale = new List<int>();
                    stale.Add(kvp.Key);
                }
            }

            if (stale == null) return;
            foreach (int id in stale)
            {
                ids.Remove(id);
            }
        }

        private void CheckAndTriggerDepartureDialogue(FamilyMember member)
        {
            if (_random.Value() > DepartureDialogueChance) return;

            int ageWeeks = _ageTracker.GetAgeWeeks(member);
            int ageYears = ageWeeks / LifespanConstants.WeeksPerYear;
            var illnesses = _illnessManager.GetActiveIllnesses(member);
            bool isElder = ageYears >= _config.elderAgeYears;
            
            var observer = GetObserver(member);
            if (observer == null) return;

            // Priority: Illness Conversation > Aging Conversation > One-off
            if (illnesses.Count > 0 && _random.Value() < 0.6f)
            {
                string illId = illnesses[_random.Range(0, illnesses.Count)];
                TriggerIllnessExpeditionConversation(observer, member, illId);
            }
            else if (isElder && _random.Value() < 0.5f)
            {
                TriggerAgingExpeditionConversation(observer, member, ageYears);
            }
            else
            {
                TriggerOneOffDeparture(observer, member, ageYears, isElder, illnesses);
            }
        }

        private void TriggerIllnessExpeditionConversation(FamilyMember observer, FamilyMember member, string illnessId)
        {
            var data = ExpeditionDialogue.GetIllnessConversation(member.firstName, illnessId);
            string opener = _dialogueHelper.PickLine($"ExpConv_Ill_{illnessId}_Opener", data.Openers, observer);
            var turns = new List<DialogueScheduler.ConversationTurn>();
            turns.Add(new DialogueScheduler.ConversationTurn(observer, opener));

            if (_random.Value() < 0.8f)
            {
                string response = _dialogueHelper.PickLine($"ExpConv_Ill_{illnessId}_Response", data.Responses, member);
                turns.Add(new DialogueScheduler.ConversationTurn(member, response));

                string closer = _dialogueHelper.PickLine($"ExpConv_Ill_{illnessId}_Closer", data.Closers, observer);
                turns.Add(new DialogueScheduler.ConversationTurn(observer, closer));
            }

            _scheduler.EnqueueConversation(turns, DialogueScheduler.Priority.Routine, 1.2f, 2.4f);
        }

        private void TriggerAgingExpeditionConversation(FamilyMember observer, FamilyMember member, int ageYears)
        {
            var data = ExpeditionDialogue.GetAgingConversation(ageYears);
            string opener = _dialogueHelper.PickLine($"ExpConv_Age_{ageYears}_Opener", data.Openers, observer);
            var turns = new List<DialogueScheduler.ConversationTurn>();
            turns.Add(new DialogueScheduler.ConversationTurn(observer, opener));

            if (_random.Value() < 0.8f)
            {
                string response = _dialogueHelper.PickLine($"ExpConv_Age_{ageYears}_Response", data.Responses, member);
                turns.Add(new DialogueScheduler.ConversationTurn(member, response));

                string closer = _dialogueHelper.PickLine($"ExpConv_Age_{ageYears}_Closer", data.Closers, observer);
                turns.Add(new DialogueScheduler.ConversationTurn(observer, closer));
            }

            _scheduler.EnqueueConversation(turns, DialogueScheduler.Priority.Routine, 1.2f, 2.4f);
        }

        private void TriggerOneOffDeparture(FamilyMember observer, FamilyMember member, int ageYears, bool isElder, List<string> illnesses)
        {
            string illName = illnesses.Count > 0 ? GetIllnessSimpleName(illnesses[0]) : "";
            var options = ExpeditionDialogue.GetOneOffOptions(ageYears, illName, illnesses.Count > 0);
            
            if (options.Count > 0)
            {
                string line = _dialogueHelper.PickLine("ExpOneOff", options, observer);
                _scheduler.Enqueue(observer, line, false, DialogueScheduler.Priority.Routine);
            }
        }

        private FamilyMember GetObserver(FamilyMember excluded)
        {
            if (FamilyManager.Instance == null) return null;
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return null;
            var candidates = new List<FamilyMember>();
            foreach (var m in members) if (m != null && !m.isDead && m.GetId() != excluded.GetId() && !m.isChild && !ExpeditionStateHelper.IsDepartingOrAway(m)) candidates.Add(m);
            if (candidates.Count == 0) return null;
            return candidates[_random.Range(0, candidates.Count)];
        }

        private void TriggerAwayObservation(FamilyMember awayMember, float awayDurationSeconds)
        {
            var observer = GetObserver(awayMember);
            if (observer == null) return;

            int ageYears = _ageTracker.GetAgeWeeks(awayMember) / LifespanConstants.WeeksPerYear;
            var illnesses = _illnessManager.GetActiveIllnesses(awayMember);
            
            var options = ExpeditionDialogue.GetAwayObservationOptions(awayMember.firstName, ageYears, illnesses);
            if (awayDurationSeconds < AwayObservationSoftDurationSeconds)
            {
                options = FilterLowAnxietyAwayOptions(options);
            }
            if (options.Count > 0)
            {
                string line = _dialogueHelper.PickLine($"AwayObs_{awayMember.GetId()}", options, observer);
                _scheduler.Enqueue(observer, line, false, DialogueScheduler.Priority.Routine);
            }
        }

        private List<DialogueLine> FilterLowAnxietyAwayOptions(List<DialogueLine> options)
        {
            var filtered = new List<DialogueLine>();
            foreach (var option in options)
            {
                string text = option != null ? option.Text : null;
                if (string.IsNullOrEmpty(text)) continue;
                string lower = text.ToLowerInvariant();

                if (lower.Contains("finally giving out")) continue;
                if (lower.Contains("miracle")) continue;
                if (lower.Contains("can't handle")) continue;
                if (lower.Contains("afraid")) continue;
                if (lower.Contains("getting lost")) continue;
                if (lower.Contains("if the map fails")) continue;
                if (lower.Contains("can't afford")) continue;

                filtered.Add(option);
            }
            return filtered.Count > 0 ? filtered : options;
        }

        private string GetIllnessSimpleName(string id)
        {
            if (id.Contains("dementia")) return "memory trouble";
            if (id.Contains("arthritis")) return "joint pain";
            if (id.Contains("heart")) return "heart condition";
            if (id.Contains("frailty")) return "weakness";
            if (id.Contains("respiratory")) return "cough";
            return "condition";
        }
        
        public void Reset()
        {
            _departingProcessed.Clear();
            _awaySince.Clear();
            _nextAwayObservationCheck.Clear();
            _nextFamilyScanAt = 0f;
        }
    }
}
