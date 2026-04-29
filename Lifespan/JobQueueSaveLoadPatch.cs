using HarmonyLib;
using System.Collections;
using System.Collections.Generic;

namespace Lifespan
{
    [HarmonyPatch(typeof(JobQueue), "SaveLoadJobQueue")]
    public static class JobQueueSaveLoadPatch
    {
        public static bool Prefix(JobQueue __instance, SaveData data, string groupName)
        {
            List<Job> jobs = Traverse.Create(__instance).Field("jobs").GetValue<List<Job>>();
            if (jobs == null)
            {
                return true;
            }

            data.GroupStart(groupName);
            data.SaveLoadList(
                "jobs",
                (IList)jobs,
                i => jobs[i].SaveLoadJob(data),
                i =>
                {
                    string jobType = string.Empty;
                    data.SaveLoad("jobType", ref jobType);
                    if (string.IsNullOrEmpty(jobType))
                    {
                        return;
                    }

                    Job job = CreateJob(jobType);
                    if (job == null)
                    {
                        return;
                    }

                    job.SaveLoadJob(data);
                    jobs.Add(job);
                });
            data.GroupEnd();

            return false;
        }

        private static Job CreateJob(string jobType)
        {
            switch (jobType)
            {
                case "Job":
                    return new Job();
                case "Job_Craft":
                    return new Job_Craft();
                case "Job_CraftRoom":
                    return new Job_CraftRoom();
                case "Job_GoToLocation":
                    return new Job_GoToLocation();
                case "Job_MoveCorpse":
                    return new Job_MoveCorpse();
                case "Job_EatFood":
                    return new Job_EatFood();
                case "Job_Clean":
                    return new Job_Clean();
                case "Job_Feed":
                    return new Job_Feed();
                case "Job_Murder":
                    return new Job_Murder();
                case "Job_LeaveShelter":
                    return new Job_LeaveShelter();
                case "Job_ExtinguishFires":
                    return new Job_ExtinguishFires();
                case "Job_Revive":
                    return new Job_Revive();
                case "Job_FeedChild":
                    return new Job_FeedChild();
                case "Job_GiveWaterChild":
                    return new Job_GiveWaterChild();
                case "Job_ChangeDiaper":
                    return new Job_ChangeDiaper();
                case "Job_CleanChild":
                    return new Job_CleanChild();
                case "Job_HelpSleepChild":
                    return new Job_HelpSleepChild();
                default:
                    return new Job();
            }
        }
    }
}
