using HarmonyLib;

namespace Lifespan
{
    /// <summary>
    /// Shared expedition state checks used by multiple patches.
    /// Treats "departing" as the pre-isAway window where leave jobs are queued.
    /// </summary>
    public static class ExpeditionStateHelper
    {
        public static bool IsDepartingOrAway(FamilyMember member)
        {
            string reason;
            return IsExpeditionTransitionOrAway(member, out reason);
        }

        public static bool IsExpeditionTransitionOrAway(FamilyMember member, out string reason)
        {
            reason = null;
            if (object.ReferenceEquals(member, null)) return false;

            if (member.isAway)
            {
                reason = "away";
                return true;
            }

            if (member.finishedLeavingShelter)
            {
                reason = "finished-leaving-shelter";
                return true;
            }

            if (HasDepartureJobQueued(member))
            {
                reason = "departing";
                return true;
            }

            if (HasReturnJobQueued(member))
            {
                reason = "returning";
                return true;
            }

            reason = null;
            return false;
        }

        public static bool HasDepartureJobQueued(FamilyMember member)
        {
            return HasExpeditionJobQueued(member, Job_GoToLocation.LocationReachedAction.LeftForExpedition, includeLegacyLeaveJobs: true);
        }

        public static bool HasReturnJobQueued(FamilyMember member)
        {
            return HasExpeditionJobQueued(member, Job_GoToLocation.LocationReachedAction.ReturnedFromExpedition, includeLegacyLeaveJobs: false);
        }

        private static bool HasExpeditionJobQueued(
            FamilyMember member,
            Job_GoToLocation.LocationReachedAction action,
            bool includeLegacyLeaveJobs)
        {
            if (object.ReferenceEquals(member, null))
                return false;

            return HasExpeditionJobQueued(member.job_queue, action, includeLegacyLeaveJobs) ||
                HasExpeditionJobQueued(member.ai_queue, action, includeLegacyLeaveJobs);
        }

        private static bool HasExpeditionJobQueued(
            JobQueue queue,
            Job_GoToLocation.LocationReachedAction action,
            bool includeLegacyLeaveJobs)
        {
            if (queue == null)
                return false;

            int count = queue.size;
            for (int i = 0; i < count; i++)
            {
                Job job = queue.GetAt(i);
                if (job == null) continue;

                string jt = job.GetJobType();
                if (jt == "Job_GoToLocation" && HasGoToLocationAction(job, action))
                    return true;

                if (includeLegacyLeaveJobs && jt == "Job_LeaveShelter")
                    return true;

                // Some engine transitions surface as base Job with type tag.
                if (includeLegacyLeaveJobs && jt == "Job" && job.type == "go_to_location")
                    return true;
            }

            return false;
        }

        private static bool HasGoToLocationAction(Job job, Job_GoToLocation.LocationReachedAction action)
        {
            if (job == null) return false;

            try
            {
                object value = AccessTools.Field(typeof(Job_GoToLocation), "m_callbackAction")?.GetValue(job);
                return value is Job_GoToLocation.LocationReachedAction && (Job_GoToLocation.LocationReachedAction)value == action;
            }
            catch
            {
                return false;
            }
        }
    }
}
