using ModAPI.Core;
using UnityEngine;
using System.Collections.Generic;

namespace Lifespan
{
    public class NurseJobGiver
    {
        private readonly IPluginContext _ctx;
        private readonly ChildDevelopmentManager _devManager;
        private readonly AgeTracker _ageTracker;
        private readonly DialogueScheduler _dialogue;
        private float _timer;
        private const float CHECK_INTERVAL = 2.0f; // Seconds
        private readonly Dictionary<int, float> _lastDialogueTime = new Dictionary<int, float>();

        public NurseJobGiver(IPluginContext ctx, ChildDevelopmentManager devManager, AgeTracker ageTracker, DialogueScheduler dialogue)
        {
            _ctx = ctx;
            _devManager = devManager;
            _ageTracker = ageTracker;
            _dialogue = dialogue;
        }

        public void Update()
        {
            if (GameModeManager.instance == null || FamilyManager.Instance == null) return;
            
            _timer += Time.deltaTime;
            if (_timer >= CHECK_INTERVAL)
            {
                _timer = 0f;
                CheckAndAssignFeeding();
            }
        }

        private void CheckAndAssignFeeding()
        {
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            if (members == null) return;

            HashSet<int> childrenAlreadyBeingFed = FindChildrenAlreadyBeingFed(members);
            HashSet<int> activeMemberIds = new HashSet<int>();

            foreach (var child in members)
            {
                if (child == null || child.isDead || ExpeditionStateHelper.IsDepartingOrAway(child)) continue;
                activeMemberIds.Add(child.GetId());

                if (_devManager.NeedsFeeding(child) && child.stats != null && child.stats.hunger != null)
                {
                    // Check hunger (0..100)
                    float hunger = child.stats.hunger.Value;
                    if (hunger >= 40f) // Threshold to start caring
                    {
                        // Handle Dialogue / "Baby Talk"
                        if (IsBabyTalkAge(child))
                        {
                            ProcessBabyDialogue(child, hunger);
                        }

                        // Check if already being fed
                        if (childrenAlreadyBeingFed.Contains(child.GetId())) continue;

                        // Find nurse
                        FamilyMember nurse = FindNurse(members);
                        if (nurse != null)
                        {
                            Job_FeedChild job = new Job_FeedChild(nurse, child);
                            nurse.job_queue.AddJob(job);
                            childrenAlreadyBeingFed.Add(child.GetId());
                            _ctx.Log.Info($"[Nurse] Assigned {nurse.firstName} to feed {child.firstName} (Hunger: {hunger:F0}).");
                        }
                    }
                }
            }

            CleanupDialogueTimes(activeMemberIds);
        }

        private void ProcessBabyDialogue(FamilyMember child, float hunger)
        {
            if (_dialogue == null) return;

            int id = child.GetId();
            float now = Time.time;

            // Only talk every 30-60 seconds to avoid spam
            if (!_lastDialogueTime.ContainsKey(id) || now - _lastDialogueTime[id] > 45f)
            {
                string text = "";
                if (hunger > 80f) text = "WAAAAAA! *sob*";
                else if (hunger > 60f) text = "Waaaa... hungwy...";
                else text = "Goo goo... *rumble*";

                _dialogue.Enqueue(child, text, false, DialogueScheduler.Priority.Reactive);
                _lastDialogueTime[id] = now;
            }
        }

        private bool IsBabyTalkAge(FamilyMember child)
        {
            if (child == null || _ageTracker == null) return false;

            int ageYears = _ageTracker.GetAgeYears(child);
            return ageYears >= 0 && ageYears <= 2;
        }

        private HashSet<int> FindChildrenAlreadyBeingFed(List<FamilyMember> members)
        {
            HashSet<int> result = new HashSet<int>();
            if (members == null) return result;

            foreach (var m in members)
            {
                if (m == null || m.job_queue == null) continue;

                for (int i = 0; i < m.job_queue.size; i++)
                {
                    Job job = m.job_queue.GetAt(i);
                    if (job is Job_FeedChild feedJob)
                    {
                        FamilyMember target = HarmonyLib.Traverse.Create(feedJob).Field("_child").GetValue<FamilyMember>();
                        if (target != null)
                        {
                            result.Add(target.GetId());
                        }
                    }
                }
            }
            return result;
        }

        private FamilyMember FindNurse(List<FamilyMember> members)
        {
            // Find an adult who is idle (no current job or idle job)
            if (members == null) return null;
            foreach (var m in members)
            {
                if (!IsValidNursingCandidate(m)) continue;
                if (_devManager.GetStage(m) < ChildStage.Teen) continue; // Only teens/adults can feed

                // ONLY if automation is enabled!
                if (!m.automation) continue;

                if (m.job_queue.is_empty) return m;
            }
            return null;
        }

        private bool IsValidNursingCandidate(FamilyMember member)
        {
            if (member == null || member.isDead) return false;
            if (member.IsUnconscious || member.isCatatonic) return false;

            // Hands-off expedition members during departure/away windows.
            if (ExpeditionStateHelper.IsDepartingOrAway(member)) return false;

            return true;
        }

        private void CleanupDialogueTimes(HashSet<int> activeMemberIds)
        {
            if (_lastDialogueTime.Count == 0 || activeMemberIds == null) return;

            List<int> staleIds = null;
            foreach (var kvp in _lastDialogueTime)
            {
                if (!activeMemberIds.Contains(kvp.Key))
                {
                    if (staleIds == null) staleIds = new List<int>();
                    staleIds.Add(kvp.Key);
                }
            }

            if (staleIds == null) return;
            foreach (int id in staleIds)
            {
                _lastDialogueTime.Remove(id);
            }
        }
    }
}
