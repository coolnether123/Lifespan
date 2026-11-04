using HarmonyLib;
using System.Collections;
using System.Collections.Generic;

namespace Lifespan
{
    [HarmonyPatch(typeof(JobQueue), "SaveLoadJobQueue")]
    public static class JobQueueSaveLoadPatch
    {
        public static void Postfix(JobQueue __instance, SaveData data, string groupName)
        {
            if (!data.isLoading) return;
            List<Job> jobs = Traverse.Create(__instance).Field("jobs").GetValue<List<Job>>();
            if (jobs == null) return;

            // Re-read only our entries after the owning loader has restored the queue.
            var savedJobs = new List<Job>();
            data.GroupStart(groupName);
            data.SaveLoadList(
                "jobs",
                (IList)savedJobs,
                i => { },
                i =>
                {
                    string jobType = string.Empty;
                    data.SaveLoad("jobType", ref jobType);
                    Job job = CreateJob(jobType);
                    if (job == null) return;
                    job.SaveLoadJob(data);
                    if (i < jobs.Count) jobs[i] = job;
                    else jobs.Add(job);
                });
            data.GroupEnd();
        }

        internal static Job CreateJob(string jobType)
        {
            switch (jobType)
            {
                case ChildCareJobTypes.FeedChild:
                    return new Job_FeedChild();
                case ChildCareJobTypes.GiveWaterChild:
                    return new Job_GiveWaterChild();
                case ChildCareJobTypes.ChangeDiaper:
                    return new Job_ChangeDiaper();
                case ChildCareJobTypes.CleanChild:
                    return new Job_CleanChild();
                case ChildCareJobTypes.HelpSleepChild:
                    return new Job_HelpSleepChild();
                default:
                    return null;
            }
        }
    }
}
