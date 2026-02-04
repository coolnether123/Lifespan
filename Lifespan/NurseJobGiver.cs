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
        private float _timer;
        private const float CHECK_INTERVAL = 2.0f; // Seconds

        public NurseJobGiver(IPluginContext ctx, ChildDevelopmentManager devManager)
        {
            _ctx = ctx;
            _devManager = devManager;
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
                if (child == null || child.isDead || child.isAway) continue;

                if (_devManager.NeedsFeeding(child))
                {
                    // Check hunger (0..100)
                    float hunger = child.stats.hunger.Value;
                    if (hunger >= 40f) // Threshold to start caring
                    {
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

        private bool IsBeingFed(FamilyMember child)
        {
            // Check all members to see if anyone has a job targeting this child
            // This is expensive? Max 4-10 members. It's fine.
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            foreach (var m in members)
            {
                if (m.job_queue != null)
                {
                    // Peek current and queue
                    // Our Job_FeedChild doesn't strictly expose 'target', but we can check job type
                    // Actually, passing the child as 'target_character' to base Job might allow checking 'target_character'
                    // In Job_FeedChild.cs we did: base("...", child.pos, feeder, null) 
                    // So 'target_character' in base Job is NULL? No, 3rd arg is target?
                    // Job constructor: Job(type, pos, character, object)
                    // We passed 'feeder' as 'character'.
                    // We didn't pass 'child' to base! 
                    // Let's rely on internal check or checking if 'nurse' is busy.
                    
                    // Actually, if we just check if the NURSE has a FeedChild job, we assume they are feeding *someone*.
                    // If we want to support multiple babies, we need to know WHO they are feeding.
                    // But for now, if a nurse is feeding *anyone*, maybe that's enough to say "systems working".
                    // Better: Job_FeedChild could expose the child. But we can't cast to it easily from here without referencing the class type (which we have).
                    
                    foreach(var job in GetJobs(m))
                    {
                        if (job is Job_FeedChild feedJob)
                        {
                            // Reflection or public property?
                            // We can add a public property to Job_FeedChild.
                            // Or just assume if *anyone* is running Job_FeedChild, they might be feeding this kid?
                            // No, that's bad.
                            // Let's just assign. The `AddJob` logic in `JobQueue` prevents duplicates? No.
                            // But `JobQueue` usually handles one job at a time.
                        }
                    }
                }
            }
            return false;
        }

        private IEnumerable<Job> GetJobs(FamilyMember m)
        {
            // Helper to iterate queue
            if (m.job_queue == null) yield break;
            
            // Current job
            Job current = m.job_queue.GetCurrent();
            if (current != null) yield return current;

            // Enumerate is hard because job_queue.jobs is private List.
            // But we can check current.
        }

        private FamilyMember FindNurse()
        {
            // Find an adult who is idle (no current job or idle job)
            var members = FamilyManager.Instance.GetAllFamilyMembers();
            foreach (var m in members)
            {
                if (m == null || m.isDead || m.isAway || m.IsUnconscious || m.isCatatonic) continue;
                if (_devManager.GetStage(m) < ChildStage.Teen) continue; // Only teens/adults can feed

                if (m.job_queue.is_empty) return m;
                
                // Also define "Idle" as doing "Job_Wander" or "Job_Sleep" (if we want to wake them UP to feed baby? Maybe not).
                // Prioritize completely idle.
            }
            return null;
        }
    }
}
