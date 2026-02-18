using ModAPI.Core;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Lifespan
{
    public class NurseJobGiver
    {
        private readonly IPluginContext _ctx;
        private readonly ChildDevelopmentManager _devManager;
        private readonly DialogueScheduler _dialogue;
        private float _timer;
        private const float CHECK_INTERVAL = 2.0f; // Seconds
        private readonly Dictionary<int, float> _lastDialogueTime = new Dictionary<int, float>();

        public NurseJobGiver(IPluginContext ctx, ChildDevelopmentManager devManager, DialogueScheduler dialogue)
        {
            _ctx = ctx;
            _devManager = devManager;
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

            foreach (var child in members)
            {
                if (child == null || child.isDead || ExpeditionStateHelper.IsDepartingOrAway(child)) continue;

                if (_devManager.NeedsFeeding(child))
                {
                    // Check hunger (0..100)
                    float hunger = child.stats.hunger.Value;
                    if (hunger >= 40f) // Threshold to start caring
                    {
                        // Handle Dialogue / "Baby Talk"
                        ProcessBabyDialogue(child, hunger);

                        // Check if already being fed
                        if (IsBeingFed(child)) continue;

                        // Find nurse
                        FamilyMember nurse = FindNurse();
                        if (nurse != null)
                        {
                            Job_FeedChild job = new Job_FeedChild(nurse, child);
                            nurse.job_queue.AddJob(job);
                            _ctx.Log.Info($"[Nurse] Assigned {nurse.firstName} to feed {child.firstName} (Hunger: {hunger:F0}).");
                        }
                    }
                }
            }
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

        private bool IsBeingFed(FamilyMember child)
        {
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            foreach (var m in members)
            {
                if (m.job_queue == null) continue;

                foreach(var job in GetJobs(m))
                {
                    if (job is Job_FeedChild feedJob)
                    {
                        // Use Traverse to peek at the private _child field to see if it matches
                        FamilyMember target = HarmonyLib.Traverse.Create(feedJob).Field("_child").GetValue<FamilyMember>();
                        if (target == child)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private IEnumerable<Job> GetJobs(FamilyMember m)
        {
            if (m.job_queue == null) yield break;
            
            // Check current active job
            Job current = m.job_queue.GetCurrent();
            if (current != null) yield return current;

            // Check queued jobs
            // m.job_queue.jobs is private, so we use reflection to iterate the rest of the queue
            List<Job> queueParams = HarmonyLib.Traverse.Create(m.job_queue).Field("jobs").GetValue<List<Job>>();
            if (queueParams != null)
            {
                // Skip the first one if it's the same as 'current', otherwise just yield all
                foreach(var j in queueParams)
                {
                   if (j != current) yield return j;
                }
            }
        }

        private FamilyMember FindNurse()
        {
            // Find an adult who is idle (no current job or idle job)
            var members = FamilyManager.Instance.GetAllFamilyMembers();
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
    }
}
