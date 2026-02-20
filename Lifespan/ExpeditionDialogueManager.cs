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
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return;

            foreach (var member in members)
            {
                if (member == null || member.isDead) continue;

                bool isDeparting = ExpeditionStateHelper.IsDepartingOrAway(member) && !member.isAway;
                int id = member.GetId();

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
                    
                    // Small chance for people in the shelter to talk about someone who is already AWAY
                    if (member.isAway && _random.Value() < 0.005f) // Very small chance per frame/tick
                    {
                        TriggerAwayObservation(member);
                    }
                }
            }
        }

        private void CheckAndTriggerDepartureDialogue(FamilyMember member)
        {
            if (_random.Value() > 0.4f) return; // 40% chance for departure dialogue

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
            _scheduler.Enqueue(observer, opener, false, DialogueScheduler.Priority.Routine);

            if (_random.Value() < 0.8f)
            {
                string response = _dialogueHelper.PickLine($"ExpConv_Ill_{illnessId}_Response", data.Responses, member);
                _scheduler.Enqueue(member, response, false, DialogueScheduler.Priority.Routine);

                string closer = _dialogueHelper.PickLine($"ExpConv_Ill_{illnessId}_Closer", data.Closers, observer);
                _scheduler.Enqueue(observer, closer, false, DialogueScheduler.Priority.Routine);
            }
        }

        private void TriggerAgingExpeditionConversation(FamilyMember observer, FamilyMember member, int ageYears)
        {
            var data = ExpeditionDialogue.GetAgingConversation(ageYears);
            string opener = _dialogueHelper.PickLine($"ExpConv_Age_{ageYears}_Opener", data.Openers, observer);
            _scheduler.Enqueue(observer, opener, false, DialogueScheduler.Priority.Routine);

            if (_random.Value() < 0.8f)
            {
                string response = _dialogueHelper.PickLine($"ExpConv_Age_{ageYears}_Response", data.Responses, member);
                _scheduler.Enqueue(member, response, false, DialogueScheduler.Priority.Routine);

                string closer = _dialogueHelper.PickLine($"ExpConv_Age_{ageYears}_Closer", data.Closers, observer);
                _scheduler.Enqueue(observer, closer, false, DialogueScheduler.Priority.Routine);
            }
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

        private void TriggerAwayObservation(FamilyMember awayMember)
        {
            var observer = GetObserver(awayMember);
            if (observer == null) return;

            int ageYears = _ageTracker.GetAgeWeeks(awayMember) / LifespanConstants.WeeksPerYear;
            var illnesses = _illnessManager.GetActiveIllnesses(awayMember);
            
            var options = ExpeditionDialogue.GetAwayObservationOptions(awayMember.firstName, ageYears, illnesses);
            if (options.Count > 0)
            {
                string line = _dialogueHelper.PickLine($"AwayObs_{awayMember.GetId()}", options, observer);
                _scheduler.Enqueue(observer, line, false, DialogueScheduler.Priority.Routine);
            }
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
        }
    }
}
