using HarmonyLib;
using ModAPI.Core;
using ModAPI.Spine;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace Lifespan
{
    public static class ChildCapabilityPatches
    {
        public static ChildDevelopmentManager Manager;
        private static IModLogger Log;

        public static void Initialize(IPluginContext ctx, ChildDevelopmentManager manager)
        {
            Manager = manager;
            Log = ctx.Log;
            Harmony harmony = new Harmony("com.lifespan.childpatches");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        // ====================================================================
        // MOVEMENT RESTRICTIONS
        // ====================================================================

        [HarmonyPatch(typeof(BaseCharacter), "get_walkSpeedMultiplier")]
        public static class WalkSpeedPatch
        {
            public static void Postfix(BaseCharacter __instance, ref float __result)
            {
                if (Manager == null || __instance == null) return;
                
                if (__instance is FamilyMember member)
                {
                    if (!Manager.CanMove(member))
                    {
                        // Newborns are immobile
                        __result = 0f;
                    }
                    else
                    {
                         // Toddlers move slower? (Optional feature, implementing simple 0.5x)
                         ChildStage stage = Manager.GetStage(member);
                         if (stage == ChildStage.Child) // Using "Child" as Toddler/Mobile stage for now
                         {
                             // __result *= 0.6f; // Optional: Slower toddlers
                         }
                    }
                }
            }
        }

        // ====================================================================
        // JOB RESTRICTIONS
        // ====================================================================

        [HarmonyPatch(typeof(JobQueue), "AddJob")]
        public static class JobQueuePatch
        {
            public static bool Prefix(JobQueue __instance, Job new_job)
            {
                if (Manager == null || new_job == null || new_job.character == null) return true;

                if (new_job.character is FamilyMember member)
                {
                    // Allow critical self-preservation jobs regardless (Eating, Sleeping)
                    string jobType = new_job.GetJobType();
                    if (jobType == "Job_EatFood" || jobType == "Job_Sleep" || jobType == "feed_job") 
                    {
                        // Newborns cannot feed themselves
                        if (Manager.NeedsFeeding(member) && jobType == "Job_EatFood")
                        {
                            return false; // Prevent self-feeding for newborns
                        }
                        return true; 
                    }

                    if (!Manager.CanDoJobs(member))
                    {
                        // Log.Debug($"Blocked job {jobType} for child {member.firstName}.");
                        return false; // Prevent assignment
                    }
                }
                return true;
            }
        }

        // ====================================================================
        // EXPEDITION RESTRICTIONS
        // ====================================================================

        // Hook into the validation logic in ExpeditionMainPanelNew.Update
        // But since we can't easily inject a variable into the local scope of Update, 
        // we'll patch the result of a method used in Update, OR Postfix the Update method to disable the button.
        
        // Better approach: Patch CheckForLoyalty or similar method called when clicking "Accept"? 
        // Actually, preventing the button from being enabled is best.
        // Update() sets m_isReadyToGo. We can Postfix Update to set it to false if invalid.

        [HarmonyPatch(typeof(ExpeditionMainPanelNew), "Update")]
        public static class ExpeditionUpdatePatch
        {
            public static void Postfix(ExpeditionMainPanelNew __instance)
            {
                if (Manager == null || __instance == null) return;
                if (!__instance.MapScreen.activeInHierarchy && !__instance.PartySetup.activeInHierarchy) return;

                // Check party composition
                int p1Idx = __instance.currentPerson1Index;
                int p2Idx = __instance.currentPerson2Index;

                if (p1Idx == -1 && p2Idx == -1) return; // Empty party

                var eligible = __instance.eligiblePeople;
                FamilyMember m1 = (p1Idx != -1 && p1Idx < eligible.Count) ? eligible[p1Idx] : null;
                FamilyMember m2 = (p2Idx != -1 && p2Idx < eligible.Count) ? eligible[p2Idx] : null;
                
                bool valid = IsPartyValid(m1, m2);

                if (!valid)
                {
                     // Force disable the button
                     // Reflection to access private field m_isReadyToGo
                     Traverse.Create(__instance).Field("m_isReadyToGo").SetValue(false);
                     
                     // Also disable the confirm button visual
                     if (__instance.mapScreenConfirmButton != null)
                        __instance.mapScreenConfirmButton.SetEnabled(false);
                     
                     // Legend button
                     var legend = Traverse.Create(__instance).Field("m_mapScreenLegend").GetValue<LegendContainer>();
                     if (legend != null)
                        legend.SetButtonEnabled(LegendContainer.ButtonEnum.XButton, false);
                }
            }

            private static bool IsPartyValid(FamilyMember m1, FamilyMember m2)
            {
                 // Check m1
                 if (m1 != null)
                 {
                     bool hasAdult = (m2 != null && Manager.GetStage(m2) >= ChildStage.Adult); 
                     if (!Manager.CanGoOnExpedition(m1, hasAdult)) return false;
                 }
                 
                 // Check m2
                 if (m2 != null)
                 {
                     bool hasAdult = (m1 != null && Manager.GetStage(m1) >= ChildStage.Adult);
                     if (!Manager.CanGoOnExpedition(m2, hasAdult)) return false;
                 }

                 return true;
            }
        }
        
        // Optional: Patch a method that triggers a MessageBox to explain WHY it's disabled?
        // Maybe on clicking the Disabled button? ModAPI doesn't easily support that.
        // Instead, we can let them click it (if we didn't disable it) and then intercept the transition.
        // But Update() continuously disables it.
        // User feedback is missing here ("Why can't I send them?").
        // We could use a "Warning" popup if they TRY to select an invalid member? No, selection logic is separate.
        
        // ====================================================================
        // FLOOR SLEEPING / FATIGUE
        // ====================================================================

        [HarmonyPatch(typeof(BehaviourStat), "Update", new System.Type[] { typeof(float) }, new ArgumentType[] { ArgumentType.Out })]
        public static class FatigueUpdatePatch
        {
            public static void Prefix(BehaviourStat __instance, ref float change)
            {
                 // We need to know WHICH stat this is and WHO it belongs to.
                 // BehaviourStat doesn't store a reference to its owner or its type easily accessible here?
                 // Wait, BehaviourStats (plural) has m_member. BehaviourStat (singular) is just a value container.
                 // But BehaviourStats.Initialize sets the stats.
                 
                 // We can traverse up? No.
                 // We might need to patch BehaviourStats.UpdateStats instead.
            }
        }
        
        [HarmonyPatch(typeof(BehaviourStats), "UpdateStats")]
        public static class StatsUpdatePatch
        {
            public static void Postfix(BehaviourStats __instance)
            {
                if (Manager == null || __instance == null) return;
                
                // Get the member
                FamilyMember member = Traverse.Create(__instance).Field("m_member").GetValue<FamilyMember>();
                if (member == null) return;

                if (Manager.NeedsFeeding(member)) // Immobile / Newborn
                {
                    // Auto-manage Fatigue to prevent passing out
                    if (__instance.fatigue.Value >= 90f)
                    {
                        // Recover fatigue fast
                        __instance.fatigue.Modify(-10f * Time.deltaTime); 
                        // Note: Value is 0..100 usually.
                    }
                    else if (__instance.fatigue.Value > 0f && __instance.fatigue.Value < 90f)
                    {
                        // If they are strictly immobile, maybe we just keep them at low fatigue?
                        // Or allow them to get tired and then sleep?
                        // "Sleep where they are".
                        // Logic: If tired (>90), SLEEP (reduce fatigue). If awake (<10), wake up.
                        // Implemented by the check above.
                    }
                }
            }
        }
    }
}
