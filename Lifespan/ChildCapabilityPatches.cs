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
        }

        // ====================================================================
        // MOVEMENT RESTRICTIONS
        // ====================================================================

        [HarmonyPatch(typeof(BaseCharacter), "GetWalkSpeed")]
        public static class WalkSpeedPatch
        {
            public static void Postfix(BaseCharacter __instance, ref float __result)
            {
                if (Manager == null || __instance == null) return;
                
                if (__instance is FamilyMember member)
                {
                    if (!Manager.CanMove(member))
                    {
                        // Newborns move very slowly instead of being hard-immobile to reduce soft-lock risk.
                        float slowSpeed = __result * 0.25f;
                        __result = Mathf.Max(0.2f, slowSpeed);
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
            private static bool IsSystemMovementJob(Job job, string jobType)
            {
                if (job == null) return false;

                // Never block engine-driven movement/state-transition jobs.
                if (jobType == "Job_GoToLocation" ||
                    jobType == "Job_LeaveShelter" ||
                    jobType == "Job_TakeStasisHazmat")
                    return true;

                // Base Job type is used for some internal transitions (e.g. hazmat return).
                if (jobType == "Job")
                {
                    string type = job.type ?? string.Empty;
                    if (type == "go_to_location" || type == "return_hazmat_suit")
                        return true;
                }

                return false;
            }

            public static bool Prefix(JobQueue __instance, Job new_job)
            {
                if (Manager == null || new_job == null || new_job.character == null) return true;

                if (new_job.character is FamilyMember member)
                {
                    string jobType = new_job.GetJobType();

                    // Always permit system movement jobs to avoid soft-locking expedition leave/return states.
                    if (IsSystemMovementJob(new_job, jobType))
                        return true;

                    // Never allow non-system jobs to be inserted during expedition departure transit.
                    // This closes the race between FinaliseExpedition and BeginExploring.
                    if (IsMemberDeparting(member))
                        return false;

                    // Allow critical self-preservation jobs regardless (Eating, Sleeping)
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

            private static bool IsMemberDeparting(FamilyMember member)
            {
                if (member == null) return false;

                // Once away, this transit guard is no longer relevant.
                if (member.isAway) return false;

                return ExpeditionStateHelper.HasDepartureJobQueued(member);
            }
        }

        // ====================================================================
        // EXPEDITION RESTRICTIONS
        // ====================================================================

        // ====================================================================
        // EXPEDITION ELIGIBILITY FILTERING
        // ====================================================================

        [HarmonyPatch(typeof(ExpeditionMainPanelNew), "OnShow")]
        public static class ExpeditionOnShowPatch
        {
            public static void Postfix(ExpeditionMainPanelNew __instance)
            {
                if (Manager == null || __instance == null) return;

                // Get the private list m_eligiblePeople
                var field = typeof(ExpeditionMainPanelNew).GetField("m_eligiblePeople", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) return;

                List<FamilyMember> eligible = (List<FamilyMember>)field.GetValue(__instance);
                if (eligible == null) return;

                // Remove anyone who is not allowed to go on expeditions
                // We keep PreTeens and above for now (or as per Config)
                eligible.RemoveAll(m => !Manager.CanGoOnExpedition(m, true)); // Assume true for now to see them, or false if strictly solo
                
                // If we want to be more specific:
                // eligible.RemoveAll(m => Manager.GetStage(m) < ChildStage.PreTeen);

                Log.Debug($"[Lifespan] Filtered expedition list. Remaining: {eligible.Count}");
            }
        }

        // ====================================================================
        // MANUAL INTERACTION: FEED CHILD
        // ====================================================================

        [HarmonyPatch(typeof(InteractionManager), "Update_Standard")]
        public static class ManualFeedInteractionPatch
        {
            public static void Postfix(InteractionManager __instance)
            {
                if (Manager == null) return;

                // Check for Interact (Right Click)
                if (PlatformInput.GetButtonUp(PlatformInput.InputButton.Interact))
                {
                    FamilyMember hovered = __instance.hoveredMember;
                    FamilyMember selected = __instance.GetSelectedFamilyMember();

                    if (hovered != null && selected != null && hovered != selected)
                    {
                        // Is it a Newborn?
                        if (Manager.NeedsFeeding(hovered))
                        {
                            // Can the selected member feed them?
                            if (Manager.GetStage(selected) >= ChildStage.Teen)
                            {
                                ShowFeedMenu(__instance, selected, hovered);
                            }
                        }
                    }
                }
            }

            private static void ShowFeedMenu(InteractionManager im, FamilyMember feeder, FamilyMember child)
            {
                ContextMenuPanel menu = im.GetInteractionMenu();
                if (menu == null || child.stats == null) return;

                List<string> options = new List<string>();
                Dictionary<string, ChildCareNeedDefinition> optionNeeds = new Dictionary<string, ChildCareNeedDefinition>();

                foreach (ChildCareNeedDefinition need in ChildCareNeeds.GetManualMenuNeeds())
                {
                    if (ChildCareNeedStatus.IsNeeded(child, need))
                    {
                        options.Add(need.MenuLabel);
                        optionNeeds[need.MenuLabel] = need;
                    }
                }

                if (options.Count == 0) return; // Nothing needed right now
                
                Camera worldCam = Traverse.Create(im).Field("m_WorldCamera").GetValue<Camera>();
                Camera uiCam = Traverse.Create(im).Field("m_UICamera").GetValue<Camera>();

                Vector2 pos = child.transform.position;
                Vector3 screenPos = uiCam.ViewportToWorldPoint(worldCam.WorldToViewportPoint(pos));

                // Passing empty prefix "" so labels show exactly as written in the list
                menu.ShowContextMenu((Vector2)screenPos, "", options, (choice) => 
                {
                    if (feeder.job_queue != null)
                    {
                        ChildCareNeedDefinition selectedNeed;
                        if (!optionNeeds.TryGetValue(choice, out selectedNeed))
                        {
                            return;
                        }

                        Job job = ChildCareJobFactory.Create(selectedNeed, feeder, child);

                        if (job != null)
                        {
                            feeder.AddPlayerJob(job);
                            Log.Info($"[Interaction] Player ordered {feeder.firstName} to {choice} {child.firstName}.");
                        }
                    }
                });
            }
        }

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
                     Traverse.Create(__instance).Field("m_isReadyToGo").SetValue(false);
                     if (__instance.mapScreenConfirmButton != null) __instance.mapScreenConfirmButton.SetEnabled(false);
                     var legend = Traverse.Create(__instance).Field("m_mapScreenLegend").GetValue<LegendContainer>();
                     if (legend != null) legend.SetButtonEnabled(LegendContainer.ButtonEnum.XButton, false);
                }
            }

            private static bool IsPartyValid(FamilyMember m1, FamilyMember m2)
            {
                 if (m1 != null)
                 {
                     // If m1 needs a chaperone, m2 must be capable of going solo (13+).
                     bool hasCapableEscort = (m2 != null && Manager.IsSoloExpeditionCapable(m2)); 
                     if (!Manager.CanGoOnExpedition(m1, hasCapableEscort)) return false;
                 }
                 if (m2 != null)
                 {
                     bool hasCapableEscort = (m1 != null && Manager.IsSoloExpeditionCapable(m1));
                     if (!Manager.CanGoOnExpedition(m2, hasCapableEscort)) return false;
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

        [HarmonyPatch(typeof(BehaviourStat), "Update")]
        public static class FatigueUpdatePatch
        {
            public static void Prefix(BehaviourStat __instance, ref float stat_change)
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
