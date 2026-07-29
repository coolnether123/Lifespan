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
            if (member == null) return false;

            if (member.isAway || member.finishedLeavingShelter)
                return true;

            return HasDepartureJobQueued(member);
        }

        public static bool HasDepartureJobQueued(FamilyMember member)
        {
            if (member == null || member.job_queue == null)
                return false;

            int count = member.job_queue.size;
            for (int i = 0; i < count; i++)
            {
                Job job = member.job_queue.GetAt(i);
                if (job == null) continue;

                string jt = job.GetJobType();
                if (jt == "Job_GoToLocation" || jt == "Job_LeaveShelter")
                    return true;

                // Some engine transitions surface as base Job with type tag.
                if (jt == "Job" && job.type == "go_to_location")
                    return true;
            }

            return false;
        }
    }
}
